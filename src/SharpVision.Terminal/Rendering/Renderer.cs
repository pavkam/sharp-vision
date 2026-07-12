using System.Buffers;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Transport;

using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Encodes semantic frame changes and commits state only after complete output.
/// </summary>
/// <remarks>
/// Calls are intentionally serialized by the caller. A concurrent call throws
/// instead of creating an implicit, potentially unbounded output queue.
/// </remarks>
public sealed class Renderer: IDisposable
{
    private static readonly byte[] _synchronizedBegin = "\u001b[?2026h"u8.ToArray();
    private static readonly byte[] _synchronizedEnd = "\u001b[?2026l"u8.ToArray();

    private readonly Buffer _buffer;
    private readonly TimeSpan _cleanupTimeout;
    private readonly TimeProvider _timeProvider;
    private Frame? _front;
    private TerminalCapabilities? _capabilities;
    private int _disposed;
    private int _rendering;
    private bool _invalidated = true;

    /// <summary>Initializes a renderer with finite reusable output storage.</summary>
    /// <param name="maxOutputBytes">The positive maximum encoded batch size.</param>
    /// <param name="cleanupTimeout">The positive synchronized-mode recovery timeout.</param>
    /// <param name="timeProvider">The clock used to enforce recovery timeout.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A size is not positive or the timeout is not positive and finite.
    /// </exception>
    public Renderer(
        int maxOutputBytes = 16 * 1024 * 1024,
        TimeSpan? cleanupTimeout = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);
        var timeout = cleanupTimeout ?? TimeSpan.FromSeconds(1);

        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cleanupTimeout),
                timeout,
                "The cleanup timeout must be positive and finite.");
        }

        _buffer = new Buffer(maxOutputBytes);
        _cleanupTimeout = timeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets a synchronized-output cleanup failure from the last render.</summary>
    /// <remarks>
    /// This diagnostic never replaces the original rendering exception.
    /// </remarks>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Forces the next render to redraw the complete target frame.</summary>
    /// <exception cref="ObjectDisposedException">The renderer is disposed.</exception>
    public void Invalidate()
    {
        ThrowIfDisposed();
        _invalidated = true;
    }

    /// <summary>Renders and commits one target frame through direct awaited I/O.</summary>
    /// <param name="back">The active target frame borrowed until completion.</param>
    /// <param name="transport">The transport borrowed until completion.</param>
    /// <param name="capabilities">The immutable terminal capability snapshot.</param>
    /// <param name="cancellationToken">Cancels encoding output and flush.</param>
    /// <returns>Metrics for the completed frame operation.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">Another render is in progress.</exception>
    /// <exception cref="ObjectDisposedException">A supplied owner is disposed.</exception>
    public ValueTask<Metrics> RenderAsync(
        Frame back,
        ITransport transport,
        TerminalCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(capabilities);
        ThrowIfDisposed();
        back.ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
        {
            throw new InvalidOperationException("A frame render is already in progress.");
        }

        Frame? replacement = null;
        var synchronized = capabilities.SynchronizedOutput.IsSupported;
        var started = Stopwatch.GetTimestamp();
        LastCleanupException = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var forceFull = _invalidated ||
                _front is null ||
                !Equals(_capabilities, capabilities);

            // Prepare every potentially allocating front-frame operation before I/O.
            if (_front is null ||
                _front.Size != back.Size ||
                _front.AmbiguousWidth != back.AmbiguousWidth ||
                _front.MaxTextBytes < back.TextLength)
            {
                replacement = back.Clone();
            }
            else
            {
                _front.PrepareCopyFrom(back);
            }

            _buffer.Reset();
            var encoded = Encoder.Encode(_front, back, _buffer, capabilities, forceFull);

            if (_buffer.WrittenCount == 0)
            {
                replacement?.Dispose();
                replacement = null;
                _capabilities = capabilities;
                _invalidated = false;
                Volatile.Write(ref _rendering, 0);
                return ValueTask.FromResult(new Metrics(
                    0,
                    0,
                    encoded.Spans,
                    encoded.Full,
                    Stopwatch.GetElapsedTime(started)));
            }

            if (synchronized)
            {
                _buffer.Prepend(_synchronizedBegin);
                _buffer.Write(_synchronizedEnd);
            }

            return WriteAsync(
                back,
                transport,
                capabilities,
                replacement,
                encoded,
                synchronized,
                started,
                cancellationToken);
        }
        catch
        {
            replacement?.Dispose();
            Volatile.Write(ref _rendering, 0);
            throw;
        }
    }

    /// <summary>Clears and returns all renderer-owned pooled storage.</summary>
    /// <exception cref="InvalidOperationException">A render is in progress.</exception>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
        {
            throw new InvalidOperationException("The renderer cannot be disposed during a render.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            Volatile.Write(ref _rendering, 0);
            return;
        }

        _front?.Dispose();
        _front = null;
        _buffer.Dispose();
        Volatile.Write(ref _rendering, 0);
    }

    private async ValueTask<Metrics> WriteAsync(
        Frame back,
        ITransport transport,
        TerminalCapabilities capabilities,
        Frame? replacement,
        EncodeResult encoded,
        bool synchronized,
        long started,
        CancellationToken cancellationToken)
    {
        try
        {
            await transport.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Only fully transferred and flushed terminal state becomes the new front.
            if (replacement is not null)
            {
                var previous = _front;
                _front = replacement;
                replacement = null;
                previous?.Dispose();
            }
            else
            {
                Debug.Assert(_front is not null, "A reusable front frame must exist.");
                _front.CopyFrom(back);
            }

            _capabilities = capabilities;
            _invalidated = false;
            return new Metrics(
                _buffer.WrittenCount,
                1,
                encoded.Spans,
                encoded.Full,
                Stopwatch.GetElapsedTime(started));
        }
        catch (Exception exception)
        {
            _invalidated = true;

            if (synchronized)
            {
                await TryEndSynchronizedOutputAsync(transport).ConfigureAwait(false);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
        finally
        {
            replacement?.Dispose();
            Volatile.Write(ref _rendering, 0);
        }
    }

    private async ValueTask TryEndSynchronizedOutputAsync(ITransport transport)
    {
        try
        {
            using var timeout = new CancellationTokenSource(_cleanupTimeout, _timeProvider);
            await transport.WriteAsync(_synchronizedEnd, timeout.Token).ConfigureAwait(false);
            await transport.FlushAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            LastCleanupException = cleanupException;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
