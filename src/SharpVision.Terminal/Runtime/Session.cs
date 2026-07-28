// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

using MultiplexerRoute = Multiplexing.Route;

/// <summary>
/// Owns terminal mode leases and serializes input, resize, closure, and cleanup.
/// </summary>
[DebuggerDisplay("Session {_options}")]
[PublicAPI]
public sealed class Session: IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly IResizeSource _resize;
    private readonly ISink _sink;
    private readonly Options _options;
    private readonly TimeProvider _timeProvider;
    private readonly Interpreter _programInterpreter;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Lease> _leases = [];
    private TerminalContext _context;
    private int _disposed;
    private int _running;

    #region Construction and lifecycle

    /// <summary>Initializes a session that owns transport and resize-source disposal.</summary>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="sink">The non-null ordered event sink.</param>
    /// <param name="options">Validated policy, or null for defaults.</param>
    /// <param name="timeProvider">Cleanup clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="NotSupportedException">The terminal profile is not suitable for full-screen use.</exception>
    public Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options? options = null,
        TimeProvider? timeProvider = null) : this(
        transport,
        resize,
        sink,
        options ?? new Options(),
        timeProvider,
        context: null)
    {
    }

    /// <summary>
    /// Initializes a session with the terminal context already resolved by its owning application.
    /// </summary>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="sink">The non-null ordered event sink.</param>
    /// <param name="options">The non-null validated policy.</param>
    /// <param name="context">The non-null application-owned terminal context.</param>
    /// <param name="timeProvider">Cleanup clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="context"/> does not contain the exact profile from <paramref name="options"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">The terminal profile is not suitable for full-screen use.</exception>
    internal Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options options,
        TerminalContext context,
        TimeProvider? timeProvider = null) : this(
        transport,
        resize,
        sink,
        RequireOptions(options),
        timeProvider,
        RequireContext(context))
    {
    }

    private Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options resolvedOptions,
        TimeProvider? timeProvider,
        TerminalContext? context)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(sink);

        if (resolvedOptions.Profile.Description.Suitability != Suitability.Usable)
        {
            throw new NotSupportedException(
                $"Terminal description '{resolvedOptions.Profile.Description.Name}' is not suitable for full-screen use.");
        }

        if (context is not null && !ReferenceEquals(context.Profile, resolvedOptions.Profile))
        {
            throw new ArgumentException(
                "The terminal context must contain the exact profile from the session options.",
                nameof(context));
        }

        _transport = transport;
        _resize = resize;
        _sink = sink;
        _options = resolvedOptions;
        _context = context ?? resolvedOptions.CreateContext();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _programInterpreter = new Interpreter(resolvedOptions.Input.Limits);
    }

    /// <summary>Gets the first cleanup failure without replacing the primary failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets the fixed terminal backend identity selected when this session was created.</summary>
    internal TerminalBackend Backend => _context.Backend;

    /// <summary>Runs startup, ordered event delivery, and guaranteed reverse cleanup.</summary>
    /// <param name="cancellationToken">Cancels pending reads and resize waits.</param>
    /// <returns>The complete session operation.</returns>
    /// <exception cref="InvalidOperationException">The session is already running.</exception>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("The terminal session is already running.");
        }

        Exception? primary = null;
        LastCleanupException = null;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            await StartAsync(linked.Token).ConfigureAwait(false);
            await EventsAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary = exception;

            if (exception is not OperationCanceledException)
            {
                try
                {
                    _sink.Fault(exception);
                }
                catch (Exception notificationException)
                {
                    LastCleanupException = notificationException;
                }
            }
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
            Volatile.Write(ref _running, 0);
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }

        if (LastCleanupException is not null)
        {
            ExceptionDispatchInfo.Capture(LastCleanupException).Throw();
        }
    }

    /// <summary>Cancels active work and disposes owned resize and transport resources.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        await _resize.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    #endregion

    #region Mode startup

    private async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_leases.Count == 0, "A new session run starts without retained terminal-mode leases.");

        if (_options.AlternateScreen &&
            TryCreateDescriptionLease("smcup", "rmcup", out var alternateScreen))
        {
            await EnableAsync(alternateScreen, cancellationToken).ConfigureAwait(false);
        }

        if (_options.HideCursor &&
            TryCreateDescriptionLease("civis", "cnorm", out var cursor))
        {
            await EnableAsync(cursor, cancellationToken).ConfigureAwait(false);
        }

        if (_context.Profile.KeyMap.RequiresApplicationMode &&
            TryCreateDescriptionLease("smkx", "rmkx", out var keypad))
        {
            await EnableAsync(keypad, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnableOptionalAsync(
        TerminalCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        Debug.Assert(capabilities is not null, "Session options always supply an immutable capability profile.");

        if (_options.Focus && IsPermitted(capabilities.FocusReporting))
        {
            await EnableAsync(CreateFocusLease(), cancellationToken).ConfigureAwait(false);
        }

        if (_options.Paste && IsPermitted(capabilities.BracketedPaste))
        {
            await EnableAsync(CreatePasteLease(), cancellationToken).ConfigureAwait(false);
        }

        var mouse = _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse
            : capabilities.CellMouse;

        if (_options.Tracking.HasValue && MouseSupported(capabilities) && IsPermitted(mouse))
        {
            await EnableAsync(CreateMouseLease(), cancellationToken).ConfigureAwait(false);
        }

        if (_options.Keyboard.HasValue && IsPermitted(capabilities.KittyKeyboard))
        {
            await EnableAsync(CreateKeyboardLease(), cancellationToken).ConfigureAwait(false);
        }
        else if (_options.ModifyOtherKeys.HasValue && IsPermitted(capabilities.XtermKeyboard))
        {
            await EnableAsync(CreateModifyOtherKeysLease(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnableAsync(Lease lease, CancellationToken cancellationToken)
    {
        Debug.Assert(!lease.Enable.IsEmpty, "A terminal-mode lease always owns enable bytes.");
        Debug.Assert(!lease.Disable.IsEmpty, "A terminal-mode lease always owns cleanup bytes.");

        // A transport may write a prefix before reporting failure or cancellation.
        // Record the exact reverse action first so cleanup conservatively repairs
        // every attempted acquisition without guessing what reached the terminal.
        _leases.Add(lease);
        await WriteAsync(lease.Enable, cancellationToken).ConfigureAwait(false);
    }

    private bool TryCreateDescriptionLease(
        string enableName,
        string disableName,
        out Lease lease)
    {
        var programs = _context.Profile.Programs;

        if (!programs.TryExpandPair(
                enableName,
                disableName,
                _programInterpreter,
                out var enable,
                out var disable))
        {
            lease = default;
            return false;
        }

        lease = new Lease(enable.Span, disable.Span);
        return true;
    }

    private static bool IsPermitted(Feature feature)
    {
        return feature.IsSupported &&
            feature.Origin is Origin.Database or Origin.Query or Origin.Override;
    }

    private static Lease CreateFocusLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        Modes.FocusReporting(new Writer(enable), enabled: true);
        Modes.FocusReporting(new Writer(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private static Lease CreatePasteLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        Modes.BracketedPaste(new Writer(enable), enabled: true);
        Modes.BracketedPaste(new Writer(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateMouseLease()
    {
        Debug.Assert(_options.Tracking.HasValue, "A mouse lease requires configured tracking.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        Modes.Mouse(
            new Writer(enable),
            _options.Tracking.Value,
            _options.Coordinates,
            enabled: true);
        Modes.Mouse(
            new Writer(disable),
            _options.Tracking.Value,
            _options.Coordinates,
            enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateKeyboardLease()
    {
        Debug.Assert(_options.Keyboard.HasValue, "A keyboard lease requires configured enhancement flags.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        Kitty.KittyKeyboard.Push(new Writer(enable), _options.Keyboard.Value);
        Kitty.KittyKeyboard.Pop(new Writer(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateModifyOtherKeysLease()
    {
        Debug.Assert(_options.ModifyOtherKeys.HasValue, "An xterm keyboard lease requires a configured level.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        XtermModifyOtherKeys.Set(new Writer(enable), _options.ModifyOtherKeys.Value);
        XtermModifyOtherKeys.Restore(new Writer(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    #endregion

    #region Event loop

    private async ValueTask EventsAsync(CancellationToken cancellationToken)
    {
        var inputOptions = (_options.Input with
        {
            PixelMouse = _options.Coordinates == MouseCoordinates.Pixel
        }).WithKeyMap(
            _context.Profile.KeyMap,
            _context.Profile.UsesAnsiKeyGrammar);
        var candidateRoute = _options.Negotiation?.Multiplexing.Allows(
            Multiplexing.Operation.CapabilityQueries) == true
            ? new MultiplexerRoute(_options.Negotiation.Multiplexing)
            : null;
        var multiplexerRoute = candidateRoute?.CanRouteCapabilityQueries == true
            ? candidateRoute
            : null;
        var negotiationBaseline = multiplexerRoute?.Policy.OuterProfile?.Capabilities ??
                                  _context.Profile.Capabilities;
        var negotiator = _options.Negotiation is null
            ? null
            : new Negotiator(
                _options.Negotiation,
                negotiationBaseline,
                _timeProvider);
        IProtocolSink routeSink = negotiator is null
            ? _sink
            : new NegotiationSink(_sink, negotiator);
        using var router = multiplexerRoute is null
            ? new ProtocolRouter(routeSink, inputOptions, _timeProvider)
            : new ProtocolRouter(routeSink, multiplexerRoute, inputOptions, _timeProvider);
        Dimensions? localDimensions = null;

        if (_resize.TryReadCurrent(out var currentDimensions))
        {
            localDimensions = currentDimensions;
            router.SetGeometry(currentDimensions.Cells, currentDimensions.Pixels);
        }

        if (negotiator is null)
        {
            await EnableOptionalAsync(_context.Profile.Capabilities, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var queries = new ArrayBufferWriter<byte>();
            var started = negotiator.TryStart(
                queries,
                localDimensions?.Cells,
                localDimensions?.Pixels,
                multiplexerRoute);

            if (started)
            {
                await _transport.WriteAsync(queries.WrittenMemory, cancellationToken)
                    .ConfigureAwait(false);
                await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var capabilities = negotiator.Capabilities;
                _context = _context.WithCapabilities(capabilities);
                _sink.Profile(capabilities);
            }
        }

        var buffer = ArrayPool<byte>.Shared.Rent(_options.ReadBufferSize);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var deadline = negotiator is null || negotiator.IsComplete
            ? null
            : DelayUntilAsync(negotiator.Deadline, linked.Token);
        var ready = negotiator is null || negotiator.IsComplete;
        var hasPendingResize = false;
        var pendingResize = default(Dimensions);

        // Capability publication is the startup barrier. Input may refine the
        // negotiator immediately, while only the newest pre-publication resize
        // is retained so controls never observe geometry under a stale profile.
        try
        {
            var read = _transport.ReadAsync(
                buffer.AsMemory(0, _options.ReadBufferSize),
                linked.Token).AsTask();
            var resize = _resize.ReadAsync(linked.Token).AsTask();

            while (true)
            {
                var completed = deadline is null
                    ? await Task.WhenAny(read, resize).ConfigureAwait(false)
                    : await Task.WhenAny(read, resize, deadline).ConfigureAwait(false);

                if (deadline is not null && ReferenceEquals(completed, deadline))
                {
                    await deadline.ConfigureAwait(false);

                    // A timer callback may arrive before its provider's wall clock
                    // reaches the requested deadline. Wait out that remainder before
                    // reading the profile that Expire publishes.
                    if (!negotiator!.Expire())
                    {
                        deadline = DelayUntilAsync(negotiator.Deadline, linked.Token);
                        continue;
                    }

                    var capabilities = negotiator.Capabilities;
                    _context = _context.WithCapabilities(capabilities);
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }

                    continue;
                }

                if (ReferenceEquals(completed, resize))
                {
                    var dimensions = await resize.ConfigureAwait(false);
                    router.SetGeometry(dimensions.Cells, dimensions.Pixels);

                    if (ready)
                    {
                        _sink.Resize(in dimensions);
                    }
                    else
                    {
                        pendingResize = dimensions;
                        hasPendingResize = true;
                    }

                    resize = _resize.ReadAsync(linked.Token).AsTask();
                    continue;
                }

                var count = await read.ConfigureAwait(false);
                Debug.Assert((uint) count <= (uint) _options.ReadBufferSize,
                    "Transport reads fit the rented session buffer.");

                if (count == 0)
                {
                    router.Complete();

                    if (!ready)
                    {
                        _ = negotiator!.Complete();
                        var capabilities = negotiator.Capabilities;
                        _context = _context.WithCapabilities(capabilities);
                        _sink.Profile(capabilities);
                        ready = true;
                        deadline = null;

                        if (hasPendingResize)
                        {
                            _sink.Resize(in pendingResize);
                            hasPendingResize = false;
                        }
                    }

                    _sink.Closed();
                    return;
                }

                router.Route(buffer.AsSpan(0, count));

                Debug.Assert(ready || negotiator is not null, "Incomplete startup always owns a negotiator.");

                if (!ready && negotiator!.IsComplete)
                {
                    var capabilities = negotiator.Capabilities;
                    _context = _context.WithCapabilities(capabilities);
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }
                }

                read = _transport.ReadAsync(
                    buffer.AsMemory(0, _options.ReadBufferSize),
                    linked.Token).AsTask();
            }
        }
        finally
        {
            linked.Cancel();
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    #endregion

    #region Cleanup and mode encoding

    private Task DelayUntilAsync(DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var delay = deadline - _timeProvider.GetUtcNow();

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        return Task.Delay(delay, _timeProvider, cancellationToken);
    }

    private async ValueTask CleanupAsync()
    {
        using var timeout = new CancellationTokenSource(
            _options.CleanupTimeout,
            _timeProvider);

        // Terminal modes form a stack: unwind in reverse enable order and keep
        // attempting later restores even when one terminal write fails.
        for (var index = _leases.Count - 1; index >= 0; index--)
        {
            try
            {
                await WriteAsync(_leases[index].Disable, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LastCleanupException ??= exception;
            }
        }

        _leases.Clear();
    }

    private async ValueTask WriteAsync(
        ReadOnlyMemory<byte> command,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!command.IsEmpty, "Mode output always contains exact bytes.");
        await _transport.WriteAsync(command, cancellationToken)
            .ConfigureAwait(false);
        await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool MouseSupported(TerminalCapabilities capabilities)
    {
        Debug.Assert(capabilities is not null, "Mouse capability checks require the active immutable profile.");

        return _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse.IsSupported
            : capabilities.CellMouse.IsSupported;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private static Options RequireOptions(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options;
    }

    private static TerminalContext RequireContext(TerminalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context;
    }

    #endregion
}
