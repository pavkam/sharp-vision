// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Provides deterministic terminal session input, output, and failures.</summary>
internal sealed class SessionTransport: ITransport
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _writes = [];
    private int _writeCount;

    /// <summary>Gets the one-based write call that should fail.</summary>
    internal int FailWriteAt { get; init; }

    /// <summary>Gets the one-based write call that should be cancelled.</summary>
    internal int CancelWriteAt { get; init; }

    /// <summary>Gets the prefix byte count recorded before an injected write failure or cancellation.</summary>
    internal int PartialWriteBytes { get; init; }

    /// <summary>Gets the one-based flush call that should fail.</summary>
    internal int FailFlushAt { get; init; }

    /// <summary>Gets the exact injected write failure.</summary>
    internal IOException WriteFailure { get; } = new("write failed");

    /// <summary>Gets the exact injected flush failure.</summary>
    internal IOException FlushFailure { get; } = new("flush failed");

    /// <summary>Gets an optional exact read failure.</summary>
    internal IOException? ReadFailure { get; init; }

    /// <summary>Gets completion for the first read attempt.</summary>
    internal TaskCompletionSource FirstRead { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the number of completed reads, including empty EOF reads.</summary>
    internal int ReadCount { get; private set; }

    /// <summary>Gets ASCII-decoded concatenated writes.</summary>
    internal string JoinedWrites => string.Concat(_writes.Select(Encoding.ASCII.GetString));

    /// <summary>Gets the number of flush attempts.</summary>
    internal int FlushCount { get; private set; }

    /// <summary>
    /// Gets the number of times the owner disposed this transport. Session disposal is idempotent,
    /// so concurrent callers must still produce exactly one teardown.
    /// </summary>
    internal int DisposeCount { get; private set; }

    /// <summary>
    /// Gets <see cref="JoinedWrites"/> captured at the instant of the first disposal, or null when
    /// the transport was never disposed. Reverse mode restoration writes through this transport, so
    /// this snapshot is the evidence that cleanup completed before the owner tore it down.
    /// </summary>
    internal string? WritesAtDisposal { get; private set; }

    /// <summary>Queues one owned input chunk.</summary>
    /// <param name="value">The input bytes.</param>
    internal void Input(byte[] value) => _input.Writer.TryWrite(value);

    /// <summary>Completes input as an orderly closure.</summary>
    internal void Close() => _input.Writer.TryComplete();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        _ = FirstRead.TrySetResult();

        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);
            value.AsMemory().CopyTo(destination);
            ReadCount++;
            return value.Length;
        }
        catch (ChannelClosedException)
        {
            ReadCount++;
            return 0;
        }
    }

    /// <summary>Gets or sets whether the next write stalls until its own token is cancelled,
    /// modelling a wedged tty - XOFF, a full pipe, a suspended multiplexer - rather than a write
    /// that fails outright. Stalling is the only way to expire a caller's budget from inside a
    /// write, which is what distinguishes budget exhaustion from an ordinary write failure. Armed
    /// by the test rather than fixed to an index, so a change in the enable-write count cannot
    /// silently move the stall onto a startup write and deadlock instead.</summary>
    internal bool StallNextWrite { get; set; }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        // StreamTransport rejects post-disposal writes, so the fake must too. Otherwise a session
        // that restores terminal modes through an already-disposed transport still looks correct.
        ObjectDisposedException.ThrowIf(DisposeCount != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        _writeCount++;

        if (StallNextWrite)
        {
            StallNextWrite = false;
            // Bounded rather than infinite: a stall that outlives its caller's budget expires it, but
            // a stall that lands outside cleanup must still complete rather than deadlock the test.
            return new ValueTask(Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
        }

        if (_writeCount == FailWriteAt || _writeCount == CancelWriteAt)
        {
            var partialLength = Math.Min(PartialWriteBytes, source.Length);

            if (partialLength > 0)
            {
                _writes.Add(source[..partialLength].ToArray());
            }

            return _writeCount == CancelWriteAt
                ? ValueTask.FromException(new OperationCanceledException("write cancelled"))
                : ValueTask.FromException(WriteFailure);
        }

        _writes.Add(source.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(DisposeCount != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        FlushCount++;

        return FlushCount == FailFlushAt
            ? ValueTask.FromException(FlushFailure)
            : ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        WritesAtDisposal ??= JoinedWrites;
        DisposeCount++;
        _ = _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
