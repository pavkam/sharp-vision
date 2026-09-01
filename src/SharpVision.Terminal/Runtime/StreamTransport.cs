// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>
/// Adapts readable and writable streams to serialized complete terminal writes.
/// </summary>
[PublicAPI]
[MustDisposeResource]
public sealed class StreamTransport: ITransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly bool _leaveInputOpen;
    private readonly bool _leaveOutputOpen;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Lock _lifecycleGate = new();
    private readonly TaskCompletionSource _operationsDrained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Reads are drained separately from writes/flushes: a read can legitimately block
    // forever waiting on terminal input, so DisposeAsync must not join it unconditionally the
    // way it joins _operationsDrained. This source lets DisposeAsync ask a live read to
    // cancel, and _readsDrained is only ever waited on with a bound (see DisposeAsync).
    private readonly CancellationTokenSource _disposalCancellation = new();
    private readonly TaskCompletionSource _readsDrained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeSpan _readDrainTimeout;
    private readonly TimeSpan _writeDrainTimeout;

    // POSIX EIO. .NET maps a failing Unix read to IOException carrying the raw errno in
    // HResult, so this compares against the errno value rather than an HRESULT.
    private const int _inputOutputErrorNumber = 5;

    private int _disposed;
    private int _activeOperations;
    private int _activeReads;

    /// <summary>Initializes a validated stream transport with one shared ownership decision.</summary>
    /// <remarks>
    /// Use this overload when the caller's relationship to both streams is the same. A host that
    /// opens its own input device but borrows a process-owned output stream must instead use the
    /// per-stream overload, because a single flag would either leak the opened device or close a
    /// stream the transport never owned.
    /// </remarks>
    /// <param name="input">The readable input stream.</param>
    /// <param name="output">The writable output stream.</param>
    /// <param name="leaveOpen">Whether disposal leaves both streams open.</param>
    /// <exception cref="ArgumentNullException">A stream is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is unreadable or <paramref name="output"/> is unwritable.
    /// </exception>
    public StreamTransport(Stream input, Stream output, bool leaveOpen = false) : this(
        input,
        output,
        leaveOpen,
        leaveOpen)
    {
    }

    /// <summary>Initializes a validated stream transport with independent per-stream ownership.</summary>
    /// <remarks>
    /// Ownership is decided separately for each stream so a transport can own the device it was
    /// handed while borrowing another. When both parameters reference the same stream instance and
    /// either flag claims ownership, disposal closes that single stream exactly once.
    /// </remarks>
    /// <param name="input">The readable input stream.</param>
    /// <param name="output">The writable output stream.</param>
    /// <param name="leaveInputOpen">Whether disposal leaves <paramref name="input"/> open.</param>
    /// <param name="leaveOutputOpen">Whether disposal leaves <paramref name="output"/> open.</param>
    /// <param name="readDrainTimeout">
    /// How long <see cref="DisposeAsync"/> waits for an in-flight <see cref="ReadAsync"/> to
    /// leave before abandoning it and disposing the streams anyway. Defaults to one second when
    /// null.
    /// </param>
    /// <param name="writeDrainTimeout">
    /// How long <see cref="DisposeAsync"/> waits for in-flight <see cref="WriteAsync"/> and
    /// <see cref="FlushAsync"/> calls to leave before abandoning them and disposing the streams
    /// anyway. Defaults to one second when null.
    /// </param>
    /// <exception cref="ArgumentNullException">A stream is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is unreadable or <paramref name="output"/> is unwritable.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="readDrainTimeout"/> or <paramref name="writeDrainTimeout"/> is not
    /// positive and finite.
    /// </exception>
    public StreamTransport(
        Stream input,
        Stream output,
        bool leaveInputOpen,
        bool leaveOutputOpen,
        TimeSpan? readDrainTimeout = null,
        TimeSpan? writeDrainTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        if (readDrainTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(readDrainTimeout),
                readDrainTimeout,
                "The read drain timeout must be positive and finite.");
        }

        if (writeDrainTimeout is { } writeTimeout &&
            (writeTimeout <= TimeSpan.Zero || writeTimeout == Timeout.InfiniteTimeSpan))
        {
            throw new ArgumentOutOfRangeException(
                nameof(writeDrainTimeout),
                writeDrainTimeout,
                "The write drain timeout must be positive and finite.");
        }

        _input = input;
        _output = output;
        _leaveInputOpen = leaveInputOpen;
        _leaveOutputOpen = leaveOutputOpen;
        _readDrainTimeout = readDrainTimeout ?? TimeSpan.FromSeconds(1);
        _writeDrainTimeout = writeDrainTimeout ?? TimeSpan.FromSeconds(1);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On Unix a terminal hang-up is reported as <c>EIO</c> rather than as a zero-length read, and
    /// it is translated here into the orderly closure the interface documents. A controlling
    /// terminal that disappears - the emulator exits, the SSH connection drops, the multiplexer
    /// pane closes - is the ordinary end of input, not a fault the session can act on, and the
    /// alternative is an exception escaping the read loop instead of the documented shutdown path.
    /// <para>
    /// Whether a hang-up surfaces as <c>EIO</c> or as end-of-file depends on how the close and the
    /// read interleave, so the same disconnect is reported inconsistently. Measured directly on
    /// Linux x64 by reading a pseudoterminal slave whose master had just closed, 400 times: 391
    /// reads returned zero and 9 threw <c>EIO</c>. macOS returned zero on every attempt. Hiding
    /// that difference is exactly what this layer exists for.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        EnterRead();

        try
        {
            // Linked so a concurrent DisposeAsync can nudge a live read to cancel cooperatively
            // before the streams underneath it go away, rather than leaving it to run against
            // them unconditionally. See the abandon-on-timeout comment in DisposeAsync for what
            // happens when the read does not honor this.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposalCancellation.Token);

            // Windows never reports a hang-up this way, and HResult 5 means something unrelated
            // there, so it keeps the untranslated read.
            return OperatingSystem.IsWindows()
                ? await _input.ReadAsync(destination, linked.Token).ConfigureAwait(false)
                : await ReadUnixAsync(destination, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            ExitRead();
        }
    }

    private async ValueTask<int> ReadUnixAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _input.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception) when (exception.HResult == _inputOutputErrorNumber)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            // The gate wait is linked to disposal cancellation, not just the caller's token: a
            // write still queued behind another has not started any I/O yet, but if the holder is
            // genuinely blocked in a non-cooperative underlying stream, the holder never reaches
            // ReleaseWriteGate, so a queued waiter would otherwise sit on WaitAsync forever -
            // SemaphoreSlim.Dispose() does not release or fault a pending wait, so DisposeAsync
            // disposing the gate after its own abandon-on-timeout budget would strand this waiter
            // permanently rather than surface ObjectDisposedException. Linking disposal
            // cancellation here lets a queued waiter unblock as soon as disposal begins, instead
            // of waiting on a gate that may never open.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposalCancellation.Token);

            await _writeGate.WaitAsync(linked.Token).ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();

                await _output.WriteAsync(source, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                ReleaseWriteGate();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            // See the identical comment in WriteAsync for why the gate wait and the underlying
            // I/O call below both observe disposal cancellation.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposalCancellation.Token);

            await _writeGate.WaitAsync(linked.Token).ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();

                await _output.FlushAsync(linked.Token).ConfigureAwait(false);
            }
            finally
            {
                ReleaseWriteGate();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Releases the write gate, tolerating a gate already disposed by <see cref="DisposeAsync"/>.
    /// </summary>
    /// <remarks>
    /// The gate is released here, after the underlying I/O call returns or throws - not before -
    /// so a write abandoned past the write drain timeout can still be running when
    /// <see cref="DisposeAsync"/> disposes the gate. When that abandoned write eventually
    /// completes on its own, this would otherwise call <see cref="SemaphoreSlim.Release()"/> on an
    /// already-disposed gate from an unobserved background task. The transport is already being
    /// torn down at that point, so there is nothing meaningful left to release into.
    /// </remarks>
    private void ReleaseWriteGate()
    {
        try
        {
            _ = _writeGate.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Disposes owned streams after pending serialized writes leave the gate.</summary>
    /// <remarks>
    /// Each stream is released according to its own ownership flag. A borrowed stream is never
    /// closed, and a single stream supplied as both input and output is closed at most once.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_lifecycleGate)
        {
            if (_activeOperations == 0)
            {
                _ = _operationsDrained.TrySetResult();
            }

            if (_activeReads == 0)
            {
                _ = _readsDrained.TrySetResult();
            }
        }

        // Ask any in-flight write, flush, or read to cancel, then give each its own bounded
        // chance to leave before disposing the streams out from under it. A blocked write or
        // flush - flow control pausing output, a stalled or backpressured PTY/SSH session - can
        // hang exactly like a blocked read, and if the underlying stream's cancellation support
        // is imperfect, joining either drain unconditionally would trade a rare use-after-dispose
        // race for a much more common indefinite hang here. An operation that outlives its budget
        // is abandoned instead: it keeps running against already-disposed streams and is left to
        // fault or complete on its own, mirroring the same abandon-on-timeout tradeoff
        // Session.DrainAsync makes for its own read loop.
        await _disposalCancellation.CancelAsync().ConfigureAwait(false);

        await DrainWithBudgetAsync(_operationsDrained.Task, _writeDrainTimeout).ConfigureAwait(false);
        await DrainWithBudgetAsync(_readsDrained.Task, _readDrainTimeout).ConfigureAwait(false);

        try
        {
            var shared = ReferenceEquals(_input, _output);

            // Both owned streams are attempted exactly once even when the first throws. Letting
            // an input failure skip the output would leak a handle and its buffered output for a
            // reason that has nothing to do with the output stream.
            Exception? primary = null;

            if (!_leaveInputOpen)
            {
                try
                {
                    await _input.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    primary = exception;
                }
            }

            // A stream passed as both input and output is one resource. Skip the second release
            // when the input branch already closed it, but still honor output ownership when only
            // the output side claims it.
            if (!_leaveOutputOpen && (!shared || _leaveInputOpen))
            {
                try
                {
                    await _output.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    primary ??= exception;
                }
            }

            if (primary is not null)
            {
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
        }
        finally
        {
            _writeGate.Dispose();
            _disposalCancellation.Dispose();
        }
    }

    /// <summary>
    /// Waits for <paramref name="drained"/> up to <paramref name="budget"/>, then returns without
    /// waiting further, abandoning whatever admitted operations have not yet exited.
    /// </summary>
    private static async ValueTask DrainWithBudgetAsync(Task drained, TimeSpan budget)
    {
        using var expiry = new CancellationTokenSource();
        var timeout = Task.Delay(budget, expiry.Token);
        _ = await Task.WhenAny(drained, timeout).ConfigureAwait(false);
        await expiry.CancelAsync().ConfigureAwait(false);

        // A canceled Task.Delay is never reported as unobserved, but the exception is touched
        // anyway to mirror Session.DrainAsync's own budget cleanup exactly.
        if (timeout.IsCompleted)
        {
            _ = timeout.Exception;
        }
        else
        {
            _ = timeout.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void EnterOperation()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            _activeOperations = checked(_activeOperations + 1);
        }
    }

    private void ExitOperation()
    {
        lock (_lifecycleGate)
        {
            _activeOperations--;
            Debug.Assert(_activeOperations >= 0, "Every admitted transport operation exits exactly once.");

            if (_activeOperations == 0 && Volatile.Read(ref _disposed) != 0)
            {
                _ = _operationsDrained.TrySetResult();
            }
        }
    }

    private void EnterRead()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            _activeReads = checked(_activeReads + 1);
        }
    }

    private void ExitRead()
    {
        lock (_lifecycleGate)
        {
            _activeReads--;
            Debug.Assert(_activeReads >= 0, "Every admitted read exits exactly once.");

            if (_activeReads == 0 && Volatile.Read(ref _disposed) != 0)
            {
                _ = _readsDrained.TrySetResult();
            }
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
