// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Provides a transport whose read keeps borrowing the session rental until the test releases it.
/// </summary>
/// <remarks>
/// The channel-backed <see cref="SessionTransport"/> completes its read promptly on cancellation,
/// so it cannot exercise the ownership boundary that <see cref="ITransport"/> defines. This fake
/// deliberately models a non-cooperative endpoint: it captures the array behind the destination,
/// fills the whole destination with <see cref="Sentinel"/> the moment the read begins, and only
/// completes when the test says so. A test can therefore prove that the session neither wrote
/// zeroes into storage a live read still owns nor handed that storage back to the shared pool.
/// </remarks>
internal sealed class PendingReadTransport: ITransport
{
    /// <summary>
    /// The byte written across the whole borrowed destination when a read starts. Any zero in
    /// <see cref="Borrowed"/> afterwards means the session cleared storage it did not own.
    /// </summary>
    internal const byte Sentinel = 0xA5;

    private readonly TaskCompletionSource<int> _read =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte[] _borrowed = [];
    private int _borrowedLength;

    /// <summary>
    /// Gets completion signalled once the transport has taken the session rental. Awaiting it
    /// removes the race between starting the session and injecting the failure under test.
    /// </summary>
    internal TaskCompletionSource ReadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets completion signalled once the pending read has observed a cancellation request but
    /// before it acts on it. Awaiting it proves the session requested cancellation while the read
    /// was still borrowing, which is the exact window the drain must cover.
    /// </summary>
    internal TaskCompletionSource CancellationObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets whether the transport still borrows the session rental, meaning the read has not
    /// reached terminal completion and the array must not be back in the shared pool.
    /// </summary>
    internal bool IsReadPending => !_read.Task.IsCompleted;

    /// <summary>
    /// Gets the contents of the destination the read borrowed, over the exact length the session
    /// passed in. The span aliases live storage on purpose so a test can detect a pooled return
    /// that cleared the array while this transport still owned it.
    /// </summary>
    internal ReadOnlySpan<byte> Borrowed => _borrowed.AsSpan(0, _borrowedLength);

    /// <summary>
    /// Completes the pending read as cancelled, modelling a transport whose cancellation finishes
    /// asynchronously well after the token was signalled, and releases the borrowed rental.
    /// </summary>
    internal void ReleaseCancelledRead() => _read.TrySetCanceled();

    /// <inheritdoc/>
    public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray<byte>(destination, out var segment) && segment.Array is not null)
        {
            _borrowed = segment.Array;
            _borrowedLength = destination.Length;
        }

        destination.Span.Fill(Sentinel);
        _ = cancellationToken.Register(
            static state => ((PendingReadTransport) state!).CancellationObserved.TrySetResult(),
            this);
        _ = ReadStarted.TrySetResult();

        return new ValueTask<int>(_read.Task);
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        _ = source;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = _read.TrySetCanceled();
        return ValueTask.CompletedTask;
    }
}
