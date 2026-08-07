// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Threading.Channels;

/// <summary>Provides deterministic real transport and resize boundaries for host tests.</summary>
internal sealed class FakeTerminal: ITransport, IResizeSource
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<Dimensions> _resize = Channel.CreateUnbounded<Dimensions>();
    private readonly Lock _gate = new();
    private readonly List<byte[]> _writes = [];
    private TaskCompletionSource? _flushGate;
    private int _disposed;

    /// <summary>Raised after one complete write is copied.</summary>
    internal event Action<ReadOnlyMemory<byte>>? Written;

    /// <summary>Raised after one queued input block is copied into the session buffer.</summary>
    internal event Action<ReadOnlyMemory<byte>>? InputRead;

    /// <summary>Raised after one transport flush completes.</summary>
    internal event Action? Flushed;

    /// <summary>Raised when transport disposal first commits.</summary>
    internal event Action? Disposed;

    /// <summary>Gets or sets the one-based write attempt that should fail.</summary>
    internal int FailWriteNumber { get; set; }

    /// <summary>Gets or sets the exact injected write failure.</summary>
    internal Exception? WriteFailure { get; set; }

    /// <summary>Gets isolated copies of every complete write.</summary>
    internal IReadOnlyList<byte[]> Writes
    {
        get
        {
            lock (_gate)
            {
                return _writes.Select(static value => value.ToArray()).ToArray();
            }
        }
    }

    /// <summary>Gets the number of committed transport disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Queues exact terminal input bytes.</summary>
    internal void QueueInput(ReadOnlySpan<byte> value) =>
        _input.Writer.TryWrite(value.ToArray()).ShouldBeTrue();

    /// <summary>Queues one immutable resize record.</summary>
    internal void QueueResize(Dimensions value) =>
        _resize.Writer.TryWrite(value).ShouldBeTrue();

    /// <summary>Completes terminal input as an orderly closure.</summary>
    internal void CloseInput() => _input.Writer.TryComplete().ShouldBeTrue();

    /// <summary>Pauses later flush completion until <see cref="ReleaseFlush"/>.</summary>
    internal void PauseFlush() => _flushGate = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Allows a paused flush to complete.</summary>
    internal void ReleaseFlush() => _flushGate?.TrySetResult();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            value.AsSpan().CopyTo(destination.Span);
            InputRead?.Invoke(destination[..value.Length]);
            return value.Length;
        }
        catch (ChannelClosedException)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copy = source.ToArray();
        int count;

        lock (_gate)
        {
            _writes.Add(copy);
            count = _writes.Count;
        }

        if (count == FailWriteNumber && WriteFailure is { } failure)
        {
            throw failure;
        }

        Written?.Invoke(copy);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        var gate = _flushGate;

        if (gate is not null)
        {
            // ConfigureAwait(false) matters here: a caller running on a dispatcher thread has a
            // DispatcherSynchronizationContext installed, and capturing it would resume this
            // continuation via that dispatcher's own Post - which hangs indefinitely once the
            // dispatcher under test has been disposed before ReleaseFlush is called.
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Flushed?.Invoke();
    }

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
        await _resize.Reader.ReadAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Disposals++;
            _ = _input.Writer.TryComplete();
            _ = _resize.Writer.TryComplete();
            _ = _flushGate?.TrySetCanceled();
            Disposed?.Invoke();
        }

        return ValueTask.CompletedTask;
    }
}
