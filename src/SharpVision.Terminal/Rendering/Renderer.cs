// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Capabilities;

using Graphics;
using Graphics.Backends;

/// <summary>
/// Encodes semantic frame changes and commits state only after complete output.
/// </summary>
/// <remarks>
/// Calls are intentionally serialized by the caller. A concurrent call throws
/// instead of creating an implicit, potentially unbounded output queue.
/// </remarks>
[PublicAPI]
public sealed class Renderer: IDisposable
{
    private static readonly byte[] _synchronizedBegin = "\u001b[?2026h"u8.ToArray();
    private static readonly byte[] _synchronizedEnd = "\u001b[?2026l"u8.ToArray();

    private readonly Buffer _buffer;
    private readonly IGraphicsBackend? _backend;
    private Interpreter _interpreter;
    private readonly TimeSpan _cleanupTimeout;
    private readonly TimeProvider _timeProvider;
    private Geometry.Metrics? _cellMetrics;
    private Frame? _front;
    private TerminalProfile? _profile;
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
        : this(maxOutputBytes, cleanupTimeout, timeProvider, null)
    {
    }

    /// <summary>
    /// Initializes a renderer that selects and owns the graphics backend authorized by the given
    /// capability evidence.
    /// </summary>
    /// <remarks>
    /// This is the supported way for a host to obtain graphics support without naming a protocol.
    /// Kitty, sixel, and iTerm2 backends are chosen only from authoritative evidence, and a route
    /// that cannot carry graphics suppresses all of them. When nothing is proven safe the renderer
    /// silently degrades to cell-only output, matching the parameterless overload.
    /// </remarks>
    /// <param name="capabilities">The non-null negotiated capability evidence.</param>
    /// <param name="route">An optional multiplexer route the graphics protocol must survive.</param>
    /// <param name="maxOutputBytes">The positive maximum encoded batch size.</param>
    /// <param name="cleanupTimeout">The positive synchronized-mode recovery timeout.</param>
    /// <param name="timeProvider">The clock used to enforce recovery timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or timeout is invalid.</exception>
    public Renderer(
        TerminalCapabilities capabilities,
        Multiplexing.Route? route = null,
        int maxOutputBytes = 16 * 1024 * 1024,
        TimeSpan? cleanupTimeout = null,
        TimeProvider? timeProvider = null)
        : this(
            maxOutputBytes,
            cleanupTimeout,
            timeProvider,
            GraphicsBackendSelector.Create(
                capabilities ?? throw new ArgumentNullException(nameof(capabilities)),
                route))
    {
    }

    /// <summary>Initializes a renderer owning one protocol-specific graphics backend.</summary>
    /// <param name="backend">The backend owned until renderer disposal.</param>
    /// <param name="maxOutputBytes">The positive maximum encoded batch size.</param>
    /// <param name="cleanupTimeout">The positive synchronized-mode recovery timeout.</param>
    /// <param name="timeProvider">The clock used to enforce recovery timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or timeout is invalid.</exception>
    internal Renderer(
        IGraphicsBackend backend,
        int maxOutputBytes = 16 * 1024 * 1024,
        TimeSpan? cleanupTimeout = null,
        TimeProvider? timeProvider = null)
        : this(
            maxOutputBytes,
            cleanupTimeout,
            timeProvider,
            backend ?? throw new ArgumentNullException(nameof(backend)))
    {
    }

    private Renderer(
        int maxOutputBytes,
        TimeSpan? cleanupTimeout,
        TimeProvider? timeProvider,
        IGraphicsBackend? backend)
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
        _backend = backend;
        _interpreter = new Interpreter(Terminfo.Limits.Default);
        _cleanupTimeout = timeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the first secondary cleanup failure observed during this renderer lifetime.</summary>
    /// <remarks>
    /// This diagnostic never replaces the original rendering or shutdown exception and is never
    /// cleared or replaced by later successful work or secondary cleanup failures.
    /// </remarks>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets the last committed front buffer, or null before the first successful render.</summary>
    /// <remarks>
    /// Renderer-owned and therefore not public. Damage tracking compares every target against this
    /// baseline, so external mutation would silently desynchronize the frame from the physical
    /// terminal, and external disposal would permanently break a live renderer.
    /// </remarks>
    internal Frame? FrontFrame
    {
        get
        {
            ThrowIfDisposed();
            return _front;
        }
    }

    /// <summary>
    /// Lets a target frame copy render-clean regions from the last committed frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces handing out the committed frame itself. The baseline stays renderer-owned and
    /// is reachable only through <see cref="Canvas.HasPreviousFrame"/> and
    /// <see cref="Canvas.CopyFromPrevious"/>, which read cells without exposing the frame object,
    /// so a caller can neither mutate nor dispose the state damage tracking depends on.
    /// </para>
    /// <para>
    /// Attach only when cell positions did not move. After a layout pass the previous cells belong
    /// to different coordinates and copying them would render stale content.
    /// </para>
    /// <para>
    /// The link is borrowed for one render. The caller must not start a concurrent render, and the
    /// attachment lapses when the next completed render replaces the committed frame.
    /// </para>
    /// </remarks>
    /// <param name="back">The non-null target frame that will consume clean regions.</param>
    /// <returns>
    /// <see langword="true"/> when a committed frame was attached; <see langword="false"/> before
    /// the first successful render, when there is nothing to copy.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="back"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The renderer or the target frame is disposed.</exception>
    public bool AttachCommittedFrame(Frame back)
    {
        ArgumentNullException.ThrowIfNull(back);
        ThrowIfDisposed();
        back.ThrowIfDisposed();
        back.PreviousFrame = _front;

        return _front is not null;
    }

    /// <summary>Forces the next render to redraw the complete target frame.</summary>
    /// <exception cref="ObjectDisposedException">The renderer is disposed.</exception>
    public void Invalidate()
    {
        ThrowIfDisposed();
        _invalidated = true;
        _backend?.Invalidate();
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
        ArgumentNullException.ThrowIfNull(capabilities);

        var profile = _profile;

        if (profile is null || !profile.IsAnsiCompatibility || !Equals(profile.Capabilities, capabilities))
        {
            profile = TerminalProfile.CreateAnsi(capabilities);
        }

        return RenderAsync(
            back,
            transport,
            profile,
            cancellationToken);
    }

    /// <summary>Renders and commits one target frame through the active terminal profile.</summary>
    /// <param name="back">The active target frame borrowed until completion.</param>
    /// <param name="transport">The transport borrowed until completion.</param>
    /// <param name="profile">The immutable description, programs, and semantic capabilities.</param>
    /// <param name="cancellationToken">Cancels encoding output and flush.</param>
    /// <returns>Metrics for the completed frame operation.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">Another render is in progress or a program cannot expand.</exception>
    /// <exception cref="ObjectDisposedException">A supplied owner is disposed.</exception>
    public ValueTask<Metrics> RenderAsync(
        Frame back,
        ITransport transport,
        TerminalProfile profile,
        CancellationToken cancellationToken) => RenderAsync(
        back,
        transport,
        profile,
        cellMetrics: null,
        cancellationToken);

    /// <summary>Renders through the active profile and exact measured cell-pixel geometry.</summary>
    /// <remarks>
    /// A host that has established real cell-pixel geometry passes it here so graphics placement
    /// and pixel-accurate pointer inference use measured values instead of derived estimates.
    /// Passing null keeps the behavior of the profile-only overload.
    /// </remarks>
    /// <param name="back">The active target frame borrowed until completion.</param>
    /// <param name="transport">The transport borrowed until completion.</param>
    /// <param name="profile">The immutable description, programs, and semantic capabilities.</param>
    /// <param name="cellMetrics">Exact measured cell-pixel geometry, or null when unavailable.</param>
    /// <param name="cancellationToken">Cancels encoding output and flush.</param>
    /// <returns>Metrics for the completed frame operation.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">Another render is in progress or a program cannot expand.</exception>
    /// <exception cref="ObjectDisposedException">A supplied owner is disposed.</exception>
    public ValueTask<Metrics> RenderAsync(
        Frame back,
        ITransport transport,
        TerminalProfile profile,
        Geometry.Metrics? cellMetrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(profile);
        ThrowIfDisposed();
        back.ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
        {
            throw new InvalidOperationException("A frame render is already in progress.");
        }

        Frame? replacement = null;
        Interpreter? transactionInterpreter = null;
        GraphicsBackendResult backendResult = default;
        var backendPrepared = false;
        var synchronized = profile.Capabilities.SynchronizedOutput.IsAuthoritative;
        var started = Stopwatch.GetTimestamp();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profileChanged = _profile is null || !_profile.IsRenderingEquivalentTo(profile);
            var cellMetricsChanged = _cellMetrics != cellMetrics;
            var geometryChanged = _front is { } current &&
                                  (current.Size != back.Size ||
                                   current.AmbiguousWidth != back.AmbiguousWidth);
            var forceFull = _invalidated ||
                            _front is null ||
                            profileChanged ||
                            cellMetricsChanged ||
                            geometryChanged;

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

            var graphicsChanged = Damage.PlacementsChanged(_front, back, forceFull);

            if (_backend is not null)
            {
                // Preparation is the only backend phase permitted to allocate. It runs before
                // the shared byte batch is exposed to transport I/O.
                backendPrepared = true;
                backendResult = _backend.Prepare(
                    _front,
                    back,
                    forceFull,
                    new GraphicsContext(profile, cellMetrics));
            }

            _buffer.Reset();
            transactionInterpreter = profileChanged
                ? new Interpreter(Terminfo.Limits.Default)
                : _interpreter;
            transactionInterpreter.BeginTransaction();

            if (backendResult.Changed && backendResult.Uploads != 0)
            {
                _backend!.WriteUploads(_buffer);
            }

            var encoded = Encoder.Encode(
                _front,
                back,
                _buffer,
                profile,
                transactionInterpreter,
                forceFull || backendResult.FullCellRedraw);

            if (backendResult.Changed && backendResult.Placements != 0)
            {
                _backend!.WritePlacements(_buffer);
            }

            // Old remote content is removed only after replacement cells and placements have
            // been encoded, preventing visible holes during an otherwise successful update.
            if (backendResult.Changed && backendResult.Removals != 0)
            {
                _backend!.WriteRemovals(_buffer);
            }

            if (_buffer.WrittenCount == 0)
            {
                if (backendPrepared)
                {
                    _backend!.Commit();
                }

                if (graphicsChanged)
                {
                    CommitFront(back, ref replacement);
                }
                else
                {
                    replacement?.Dispose();
                    replacement = null;
                }

                transactionInterpreter.CommitTransaction();
                _interpreter = transactionInterpreter;
                transactionInterpreter = null;
                _profile = profile;
                _cellMetrics = cellMetrics;
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
                profile,
                cellMetrics,
                transactionInterpreter,
                replacement,
                encoded,
                backendPrepared,
                synchronized,
                started,
                cancellationToken);
        }
        catch
        {
            if (transactionInterpreter is { } activeInterpreter)
            {
                activeInterpreter.RollbackTransaction();
                _invalidated = true;
            }

            if (backendPrepared)
            {
                _invalidated = true;
                _backend!.Invalidate();
            }

            replacement?.Dispose();
            Volatile.Write(ref _rendering, 0);
            throw;
        }
    }

    /// <summary>Clears and returns all renderer-owned pooled storage.</summary>
    /// <exception cref="InvalidOperationException">A render is in progress.</exception>
    /// <exception cref="Exception">
    /// Graphics-backend cleanup fails. Local front-frame and batch ownership is still released,
    /// the renderer remains disposed, and the original backend exception is propagated.
    /// </exception>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            if (Volatile.Read(ref _rendering) != 0)
            {
                throw new InvalidOperationException("The renderer shutdown is already in progress.");
            }

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

        try
        {
            DisposeOwnedState();
        }
        finally
        {
            Volatile.Write(ref _rendering, 0);
        }
    }

    /// <summary>
    /// Flushes bounded graphics-release commands through a borrowed transport, then releases all
    /// local renderer ownership. Hosts call this before disposing the transport.
    /// </summary>
    /// <param name="transport">The non-null transport borrowed only until completion.</param>
    /// <param name="cancellationToken">Cancels remote cleanup write or flush.</param>
    /// <returns>A task completed after remote cleanup and unconditional local release.</returns>
    /// <remarks>
    /// A cancellation, write failure, or flush failure invalidates remote graphics state but still
    /// releases local ownership. The original I/O exception is preserved. A secondary backend
    /// disposal failure is recorded only when no earlier <see cref="LastCleanupException"/> exists;
    /// the first lifetime diagnostic is never replaced. Repeated calls after a completed or failed
    /// shutdown are byte-quiet.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A render or shutdown is already in progress.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="Exception">Remote graphics cleanup or local backend disposal fails.</exception>
    public async ValueTask ShutdownAsync(
        ITransport transport,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (Volatile.Read(ref _disposed) != 0)
        {
            if (Volatile.Read(ref _rendering) != 0)
            {
                throw new InvalidOperationException("The renderer shutdown is already in progress.");
            }

            return;
        }

        if (Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
        {
            throw new InvalidOperationException("The renderer cannot shut down during a render.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            Volatile.Write(ref _rendering, 0);
            return;
        }

        Exception? original = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _buffer.Reset();
            var cleanupCount = _backend?.PrepareCleanup() ?? 0;

            if (cleanupCount != 0)
            {
                _backend!.WriteCleanup(_buffer);
                await transport.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
                await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            _backend?.CommitCleanup();
        }
        catch (Exception exception)
        {
            original = exception;
            _backend?.Invalidate();
            throw;
        }
        finally
        {
            try
            {
                DisposeOwnedState();
            }
            catch (Exception cleanupException) when (original is not null)
            {
                LastCleanupException ??= cleanupException;
            }
            finally
            {
                Volatile.Write(ref _rendering, 0);
            }
        }
    }

    private async ValueTask<Metrics> WriteAsync(
        Frame back,
        ITransport transport,
        TerminalProfile profile,
        Geometry.Metrics? cellMetrics,
        Interpreter transactionInterpreter,
        Frame? replacement,
        EncodeResult encoded,
        bool backendPrepared,
        bool synchronized,
        long started,
        CancellationToken cancellationToken)
    {
        try
        {
            await transport.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Only fully transferred and flushed terminal state becomes the new front. Backend
            // commit is non-throwing by contract; frame copy was preallocated before I/O.
            if (backendPrepared)
            {
                _backend!.Commit();
            }

            CommitFront(back, ref replacement);

            _profile = profile;
            _cellMetrics = cellMetrics;
            transactionInterpreter.CommitTransaction();
            _interpreter = transactionInterpreter;
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
            transactionInterpreter.RollbackTransaction();
            _invalidated = true;
            _backend?.Invalidate();

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

    private void CommitFront(Frame back, ref Frame? replacement)
    {
        if (replacement is not null)
        {
            var previous = _front;
            _front = replacement;
            replacement = null;
            previous?.Dispose();
            return;
        }

        Debug.Assert(_front is not null, "A reusable front frame must exist.");
        _front.CopyFrom(back);
    }

    private void DisposeOwnedState()
    {
        _front?.Dispose();
        _front = null;

        try
        {
            _backend?.Dispose();
        }
        finally
        {
            _buffer.Dispose();
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
            LastCleanupException ??= cleanupException;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
