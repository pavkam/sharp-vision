// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Provides a transport whose one-based Nth write pauses until the test releases it, modelling a
/// reverse-lease walk observed genuinely mid-flight by a concurrent job-control call - the
/// scenario a fixed write index or an immediately-resolved fake cannot represent deterministically.
/// </summary>
internal sealed class GatedWriteTransport: ITransport
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _writes = [];
    private readonly TaskCompletionSource _gateEntered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gateRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _writeCount;
    private int _disposeCount;

    /// <summary>Gets the one-based write call that pauses until <see cref="Release"/> is called.</summary>
    internal int PauseWriteAt { get; init; }

    /// <summary>Gets completion for the first read attempt.</summary>
    internal TaskCompletionSource FirstRead { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes once the paused write is reached and blocked, so a test
    /// can await proof that the walk is genuinely mid-flight before acting concurrently.</summary>
    internal Task GateEntered => _gateEntered.Task;

    /// <summary>Gets ASCII-decoded concatenated writes completed so far.</summary>
    internal string JoinedWrites => string.Concat(_writes.Select(Encoding.ASCII.GetString));

    /// <summary>Completes input as an orderly closure, ending a session's read loop.</summary>
    internal void Close() => _input.Writer.TryComplete();

    /// <summary>Releases the write paused at <see cref="PauseWriteAt"/>, letting it complete.</summary>
    internal void Release() => _gateRelease.TrySetResult();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        _ = FirstRead.TrySetResult();

        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);
            value.AsMemory().CopyTo(destination);
            return value.Length;
        }
        catch (ChannelClosedException)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposeCount != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        _writeCount++;

        if (_writeCount == PauseWriteAt)
        {
            _ = _gateEntered.TrySetResult();
            await _gateRelease.Task.ConfigureAwait(false);
        }

        _writes.Add(source.ToArray());
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposeCount != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposeCount++;
        _ = _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
