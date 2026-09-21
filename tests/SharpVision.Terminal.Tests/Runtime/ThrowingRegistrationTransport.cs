// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Provides a transport whose read registers a callback that throws the moment its
/// cancellation token is cancelled, modelling a non-cooperative registration such as the shipped
/// Windows input stream's <c>CancelIoEx</c> descendant registration.
/// </summary>
/// <remarks>
/// Like <see cref="PendingReadTransport"/>, the read borrows the destination array and fills it
/// with <see cref="PendingReadTransport.Sentinel"/> until the test releases it, so a test can
/// prove the session drained and returned the rental instead of losing it to the registration
/// exception the session's cleanup would otherwise let escape unguarded. Writes follow the same
/// one-based <see cref="FailWriteAt"/> pattern as <see cref="SessionTransport"/> so a test can
/// combine the throwing registration with a genuine write failure already in flight.
/// </remarks>
internal sealed class ThrowingRegistrationTransport: ITransport
{
    private readonly TaskCompletionSource<int> _read =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte[] _borrowed = [];
    private int _borrowedLength;
    private int _writeCount;

    /// <summary>Gets the exception the cancellation registration throws.</summary>
    internal InvalidOperationException RegistrationException { get; } =
        new("read cancellation registration failed");

    /// <summary>Gets the one-based write call that should fail.</summary>
    internal int FailWriteAt { get; init; }

    /// <summary>Gets the exact injected write failure.</summary>
    internal IOException WriteFailure { get; } = new("write failed");

    /// <summary>
    /// Gets completion signalled once the transport has taken the session rental. Awaiting it
    /// removes the race between starting the session and cancelling the run under test.
    /// </summary>
    internal TaskCompletionSource ReadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets whether the read still borrows the session rental, meaning it has not reached
    /// terminal completion and the array must not be back in the shared pool.
    /// </summary>
    internal bool IsReadPending => !_read.Task.IsCompleted;

    /// <summary>
    /// Gets the contents of the destination the read borrowed, over the exact length the session
    /// passed in, the same aliasing idiom <see cref="PendingReadTransport.Borrowed"/> uses to
    /// detect a pooled return that cleared the array while this transport still owned it.
    /// </summary>
    internal ReadOnlySpan<byte> Borrowed => _borrowed.AsSpan(0, _borrowedLength);

    /// <summary>
    /// Completes the pending read as cancelled, modelling a transport whose own cancellation
    /// finishes asynchronously after the throwing registration has already run.
    /// </summary>
    internal void ReleaseCancelledRead() => _read.TrySetCanceled();

    /// <summary>Completes the pending read as an orderly transport closure.</summary>
    internal void CompleteReadAsClosed() => _read.TrySetResult(0);

    /// <inheritdoc/>
    public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray<byte>(destination, out var segment) && segment.Array is not null)
        {
            _borrowed = segment.Array;
            _borrowedLength = destination.Length;
        }

        destination.Span.Fill(PendingReadTransport.Sentinel);
        _ = cancellationToken.Register(
            static state => throw ((ThrowingRegistrationTransport) state!).RegistrationException,
            this);
        _ = ReadStarted.TrySetResult();

        return new ValueTask<int>(_read.Task);
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        _ = source;
        cancellationToken.ThrowIfCancellationRequested();
        _writeCount++;

        return _writeCount == FailWriteAt
            ? ValueTask.FromException(WriteFailure)
            : ValueTask.CompletedTask;
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
