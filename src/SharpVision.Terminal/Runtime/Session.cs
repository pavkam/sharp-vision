// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

using Xterm;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>
/// Owns terminal mode leases and serializes input, resize, closure, and cleanup.
/// </summary>
[DebuggerDisplay("Session {_options}")]
[PublicAPI]
[MustDisposeResource]
public sealed class Session: IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly IResizeSource _resize;
    private readonly ISink _sink;
    private readonly TerminalOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Interpreter _programInterpreter;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Lease> _leases = [];
    private readonly Lock _lifecycle = new();
    // Identity-bearing, not a boolean. The flag is necessarily static - AsyncLocal must be shared
    // to be observed across the await chain - so a bool cannot say *which* session is running, and
    // every Session in the process saw every other Session's run.
    private static readonly AsyncLocal<Session?> _dispatchingFromRun = new();
    private TerminalContext _context;
    private TaskCompletionSource? _completion;
    private Task? _disposal;
    private bool _disposed;
    private bool _running;

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
        TerminalOptions? options = null,
        TimeProvider? timeProvider = null) : this(
        transport,
        resize,
        sink,
        options ?? new TerminalOptions(),
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
        TerminalOptions options,
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
        TerminalOptions resolvedOptions,
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
        _programInterpreter = new Interpreter(resolvedOptions.Input.ProgramLimits);
        Diagnostics = CreateDiagnostics(resolvedOptions, _context);
    }

    /// <summary>Gets the first cleanup failure without replacing the primary failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets the fixed terminal backend identity selected when this session was created.</summary>
    internal TerminalBackend Backend => _context.Backend;

    /// <summary>Gets the latest immutable, typed, and redacted terminal diagnostic snapshot.</summary>
    public TerminalDiagnostics Diagnostics { get; private set; }

    /// <summary>Runs startup, ordered event delivery, and guaranteed reverse cleanup.</summary>
    /// <remarks>
    /// Claiming the run slot and observing disposal are one atomic step. A run that starts
    /// therefore always reaches reverse cleanup with a live transport, and a run requested after
    /// <see cref="DisposeAsync"/> has begun is rejected from the guard rather than failing later
    /// from inside the loop against already-disposed lifetime state.
    /// </remarks>
    /// <param name="cancellationToken">Cancels pending reads and resize waits.</param>
    /// <returns>The complete session operation.</returns>
    /// <exception cref="InvalidOperationException">The session is already running.</exception>
    /// <exception cref="ObjectDisposedException">The session is disposed or disposal has begun.</exception>
    /// <exception cref="TerminalDiagnosticException">A configured lease or cleanup diagnostic is promoted.</exception>
    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource completion;

        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_running)
            {
                throw new InvalidOperationException("The terminal session is already running.");
            }

            // Publishing the completion source under the same lock that disposal takes is what
            // makes reverse cleanup ordered: a concurrent DisposeAsync either sees this run and
            // waits for it, or wins the lock and this call never starts.
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _running = true;
        }

        Exception? primary = null;
        LastCleanupException = null;

        // Set for the duration of the run so DisposeAsync can detect - and refuse - being awaited
        // from inside a sink callback this same run raised, which would otherwise deadlock waiting
        // for the run to complete from inside itself. AsyncLocal flows only to this call's own
        // descendants, but the field itself is shared by every Session in the process, so it
        // records the running session rather than merely that *a* session is running - otherwise a
        // sink callback raised by one session cannot dispose an unrelated one, and that session's
        // transport, resize source, and lifetime all leak behind a rejection whose message is
        // factually untrue of it.
        //
        // The previous value is saved and restored rather than cleared, so a run nested inside
        // another session's callback leaves the outer session's guard intact on the way out.
        var enclosing = _dispatchingFromRun.Value;
        _dispatchingFromRun.Value = this;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            var modes = await StartAsync(linked.Token).ConfigureAwait(false);
            Diagnostics = Diagnostics.WithModes(modes);
            await EventsAsync(modes, linked.Token).ConfigureAwait(false);
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
                    // A fault notification that itself throws is preserved alongside the original
                    // failure rather than replacing it, mirroring Dispatcher.Report. Writing it into
                    // LastCleanupException instead would be wrong on two counts: that property is
                    // reserved for CleanupAsync's own lease-restoration failures below, and doing so
                    // would make CleanupAsync's `LastCleanupException ??= exception` a guaranteed
                    // no-op, silently discarding a genuine restoration failure whenever the
                    // notification callback also throws.
                    primary = new AggregateException(exception, notificationException);
                }
            }
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);

            lock (_lifecycle)
            {
                _completion = null;
                _running = false;
            }

            _dispatchingFromRun.Value = enclosing;

            // Signalled only after every reverse-mode byte was written and flushed, so a waiting
            // DisposeAsync can safely tear down the transport from here on.
            _ = completion.TrySetResult();
        }

        var cleanupPromotion = LastCleanupException is not null &&
                               (_options.DiagnosticPromotions & DiagnosticPromotion.CleanupFailure) != 0
            ? new TerminalDiagnosticException(
                DiagnosticPromotion.CleanupFailure,
                LastCleanupException)
            : null;

        if (primary is not null && cleanupPromotion is not null)
        {
            throw new AggregateException(primary, cleanupPromotion);
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }

        if (cleanupPromotion is not null)
        {
            throw cleanupPromotion;
        }

        if (LastCleanupException is not null)
        {
            ExceptionDispatchInfo.Capture(LastCleanupException).Throw();
        }
    }

    /// <summary>
    /// Cancels active work, awaits reverse-mode cleanup, and disposes the owned resize source,
    /// transport, and lifetime state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reverse mode restoration writes through the transport, so disposal cannot tear the
    /// transport down while a run is still unwinding its mode leases. This call cancels the
    /// session lifetime, waits for the active run to finish writing and flushing every disable
    /// sequence, and only then releases owned resources. A caller that never started a run, or
    /// whose run already completed, observes no additional wait.
    /// </para>
    /// <para>
    /// Disposal is idempotent and safe to call concurrently. Every caller awaits the same
    /// underlying teardown and returns only after it finished, so no caller observes a
    /// half-disposed session. Reverse cleanup runs exactly once.
    /// </para>
    /// <para>
    /// This call must not be awaited from an <see cref="ISink"/> callback raised by its own run.
    /// Doing so asks the run to complete from inside itself, which would otherwise deadlock;
    /// this is detected and rejected with <see cref="InvalidOperationException"/> instead.
    /// Dispose the session from the code that owns <see cref="RunAsync"/> instead.
    /// </para>
    /// <para>
    /// The restriction is scoped to <em>this</em> session's run. Disposing a different session
    /// from inside a callback - one session tearing another down from a fault notification, a
    /// multiplexer host, a harness driving two pseudoterminals - is legal and unaffected, because
    /// no run of that session is waiting on this call stack.
    /// </para>
    /// </remarks>
    /// <returns>The complete teardown operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Called from inside an <see cref="ISink"/> callback raised by this session's own run.
    /// </exception>
    public async ValueTask DisposeAsync()
    {
        if (ReferenceEquals(_dispatchingFromRun.Value, this))
        {
            throw new InvalidOperationException(
                "DisposeAsync must not be awaited from an ISink callback raised by this session's " +
                "own run - that asks the run to complete from inside itself. Dispose the session " +
                "from the code that owns RunAsync instead.");
        }

        TaskCompletionSource? owner = null;
        Task disposal;

        lock (_lifecycle)
        {
            // Marking disposal before releasing the lock closes the window in which a new run
            // could start against lifetime state this call is about to dispose.
            _disposed = true;

            if (_disposal is null)
            {
                owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = owner.Task;
            }

            disposal = _disposal;
        }

        if (owner is null)
        {
            // A joining caller still waits for the single teardown to finish, but disposal is
            // idempotent: only the caller that performed it reports the failure, so this
            // continuation waits without rethrowing.
            await disposal.ContinueWith(
                static _ =>
                {
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).ConfigureAwait(false);

            return;
        }

        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            owner.SetResult();
        }
        catch (Exception exception)
        {
            owner.SetException(exception);

            // No joiner may ever await the shared task. The owner reports the failure by
            // rethrowing, so observe the copy that task retains.
            _ = owner.Task.Exception;

            throw;
        }
    }

    private async ValueTask DisposeCoreAsync()
    {
        // Every owned resource is attempted exactly once in the documented order even when an
        // earlier one throws. Abandoning the rest would leak the transport streams, handles, and
        // buffered output that the failing resource has nothing to do with.
        Exception? primary = null;

        try
        {
            _lifetime.Cancel();
        }
        catch (Exception exception)
        {
            primary = exception;
        }

        Task? completion;

        lock (_lifecycle)
        {
            completion = _completion?.Task;
        }

        if (completion is not null)
        {
            // RunAsync fulfils this only after CleanupAsync wrote and flushed every disable
            // sequence, which is the boundary that keeps the terminal restorable. The wait is
            // already bounded: the event loop drains its read within the cleanup budget and
            // CleanupAsync writes under its own finite timeout.
            await completion.ConfigureAwait(false);
        }

        try
        {
            await _resize.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        try
        {
            _lifetime.Dispose();
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    #endregion

    #region Mode startup

    private async ValueTask<TerminalModeDiagnostics> StartAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_leases.Count == 0, "A new session run starts without retained terminal-mode leases.");
        var alternateScreenActive = false;
        var cursorHiddenActive = false;

        if (_options.AlternateScreen &&
            TryCreateDescriptionLease(CapabilityNames.Smcup, CapabilityNames.Rmcup, out var alternateScreen))
        {
            await EnableAsync(alternateScreen, cancellationToken).ConfigureAwait(false);
            alternateScreenActive = true;
            PublishModes(Diagnostics.Modes.WithBaseActivation(alternateScreenActive, cursorHiddenActive));
        }
        else if (_options.AlternateScreen)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        if (_options.HideCursor &&
            TryCreateDescriptionLease(CapabilityNames.Civis, CapabilityNames.Cnorm, out var cursor))
        {
            await EnableAsync(cursor, cancellationToken).ConfigureAwait(false);
            cursorHiddenActive = true;
            PublishModes(Diagnostics.Modes.WithBaseActivation(alternateScreenActive, cursorHiddenActive));
        }
        else if (_options.HideCursor)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        if (_context.Profile.KeyMap.RequiresApplicationMode &&
            TryCreateDescriptionLease(CapabilityNames.Smkx, CapabilityNames.Rmkx, out var keypad))
        {
            await EnableAsync(keypad, cancellationToken).ConfigureAwait(false);
        }

        return Diagnostics.Modes.WithBaseActivation(alternateScreenActive, cursorHiddenActive);
    }

    private async ValueTask<TerminalModeDiagnostics> EnableOptionalAsync(
        TerminalCapabilities capabilities,
        ProtocolRouter router,
        TerminalModeDiagnostics modes,
        CancellationToken cancellationToken)
    {
        Debug.Assert(capabilities is not null, "Session options always supply an immutable capability profile.");
        var focusActive = false;
        var pasteActive = false;
        var clipboardPasteEventsActive = false;
        var mouseActive = false;
        var kittyKeyboardActive = false;
        var modifyOtherKeysActive = false;

        if (_options.Focus && IsPermitted(capabilities.FocusReporting))
        {
            await EnableAsync(CreateFocusLease(), cancellationToken).ConfigureAwait(false);
            focusActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));
        }
        else if (_options.Focus)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        if (_options.Paste && IsPermitted(capabilities.BracketedPaste))
        {
            await EnableAsync(CreatePasteLease(), cancellationToken).ConfigureAwait(false);
            pasteActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));
        }
        else if (_options.Paste)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        if (_options.ClipboardPasteEvents &&
            IsPermitted(capabilities.KittyClipboard) &&
            TryCreateClipboardPasteEventsLease(out var clipboardPasteEvents))
        {
            await EnableAsync(clipboardPasteEvents, cancellationToken).ConfigureAwait(false);
            clipboardPasteEventsActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));
        }
        else if (_options.ClipboardPasteEvents)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        var mouse = _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse
            : capabilities.CellMouse;

        if (_options.Tracking.HasValue && MouseSupported(capabilities) && IsPermitted(mouse))
        {
            await EnableAsync(CreateMouseLease(), cancellationToken).ConfigureAwait(false);
            mouseActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));
        }
        else if (_options.Tracking.HasValue)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        if (_options.Keyboard.HasValue && IsPermitted(capabilities.KittyKeyboard))
        {
            await EnableAsync(CreateKeyboardLease(), cancellationToken).ConfigureAwait(false);
            kittyKeyboardActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));

            if ((_options.Keyboard.Value &
                 (Kitty.Keyboard.KittyKeyboardEnhancement.Disambiguate |
                  Kitty.Keyboard.KittyKeyboardEnhancement.AllKeys)) != 0)
            {
                router.EnableKittyKeyboardDisambiguation();
            }
        }
        else if (_options.ModifyOtherKeys.HasValue && IsPermitted(capabilities.XtermKeyboard))
        {
            if (_options.Keyboard.HasValue)
            {
                ReportAndPromote(DiagnosticPromotion.Fallback, DiagnosticCode.Fallback);
            }

            await EnableAsync(CreateModifyOtherKeysLease(), cancellationToken).ConfigureAwait(false);
            modifyOtherKeysActive = true;
            PublishModes(Diagnostics.Modes.WithOptionalActivation(
                focusActive,
                pasteActive,
                mouseActive,
                kittyKeyboardActive,
                modifyOtherKeysActive,
                clipboardPasteEventsActive));
        }
        else if (_options.Keyboard.HasValue || _options.ModifyOtherKeys.HasValue)
        {
            ReportAndPromote(DiagnosticPromotion.UnsupportedFeature, DiagnosticCode.Unsupported);
        }

        return modes.WithOptionalActivation(
            focusActive,
            pasteActive,
            mouseActive,
            kittyKeyboardActive,
            modifyOtherKeysActive,
            clipboardPasteEventsActive);
    }

    private void ReportAndPromote(DiagnosticPromotion promotion, DiagnosticCode code)
    {
        // When the sink is an Application, _sink.Input enqueues the same diagnostic record for
        // its own dispatcher-thread promotion classifier (ApplicationDiagnosticPromotionClassifier
        // .ThrowIfConfigured) in addition to the throw below - a pre-existing double-fault
        // exposure for every promoted family, not one this method's own report-then-throw shape
        // creates or changes. Reporting was already unconditional for every promoted family before
        // this method stopped gating it: the old early-return only ever suppressed the unpromoted
        // case, which never threw either way. Resolving which of the two sites should defer is a
        // separate, cross-cutting design decision outside this method's own scope.
        var diagnostic = new Diagnostic(code, SequenceKind.None, offset: 0, discardedBytes: 0);
        _sink.Input(in diagnostic);

        if ((_options.DiagnosticPromotions & promotion) != 0)
        {
            throw new TerminalDiagnosticException(promotion);
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

    private static bool IsPermitted(Feature feature) => feature.Authoritative;

    private static Lease CreateFocusLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.FocusReporting(new ProtocolWriter(enable), enabled: true);
        ProtocolModes.FocusReporting(new ProtocolWriter(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private static Lease CreatePasteLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.BracketedPaste(new ProtocolWriter(enable), enabled: true);
        ProtocolModes.BracketedPaste(new ProtocolWriter(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private bool TryCreateClipboardPasteEventsLease(out Lease lease)
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.ClipboardPasteEvents(new ProtocolWriter(enable), enabled: true);
        ProtocolModes.ClipboardPasteEvents(new ProtocolWriter(disable), enabled: false);
        var policy = _options.Multiplexing ?? _options.Negotiation?.Multiplexing;

        if (policy is not { Layers.Count: > 0 })
        {
            lease = new Lease(enable.WrittenSpan, disable.WrittenSpan);
            return true;
        }

        var route = new MultiplexerRoute(policy);
        var routedEnable = new ArrayBufferWriter<byte>();
        var routedDisable = new ArrayBufferWriter<byte>();

        if (!route.TryWriteClipboard(routedEnable, enable.WrittenSpan) ||
            !route.TryWriteClipboard(routedDisable, disable.WrittenSpan))
        {
            lease = default;
            return false;
        }

        lease = new Lease(routedEnable.WrittenSpan, routedDisable.WrittenSpan);
        return true;
    }

    private Lease CreateMouseLease()
    {
        Debug.Assert(_options.Tracking.HasValue, "A mouse lease requires configured tracking.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.Mouse(
            new ProtocolWriter(enable),
            _options.Tracking.Value,
            _options.Coordinates,
            enabled: true);
        ProtocolModes.Mouse(
            new ProtocolWriter(disable),
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
        Kitty.Keyboard.KittyKeyboard.Push(new ProtocolWriter(enable), _options.Keyboard.Value);
        Kitty.Keyboard.KittyKeyboard.Pop(new ProtocolWriter(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateModifyOtherKeysLease()
    {
        Debug.Assert(_options.ModifyOtherKeys.HasValue, "An xterm keyboard lease requires a configured level.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        XtermModifyOtherKeys.Set(new ProtocolWriter(enable), _options.ModifyOtherKeys.Value);
        XtermModifyOtherKeys.Restore(new ProtocolWriter(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    #endregion

    #region Event loop

    private async ValueTask EventsAsync(
        TerminalModeDiagnostics modes,
        CancellationToken cancellationToken)
    {
        var inputOptions = (_options.Input with
        {
            PixelMouse = _options.Coordinates == MouseCoordinates.Pixel,
            MouseCoordinates = _options.Coordinates
        }).WithKeyMap(
            _context.Profile.KeyMap,
            _context.Profile.UsesAnsiKeyGrammar);
        var policy = _options.Multiplexing ?? _options.Negotiation?.Multiplexing;
        var candidateRoute = policy is { Layers.Count: > 0 }
            ? new MultiplexerRoute(policy)
            : null;
        var queryRoute = candidateRoute?.CanRouteCapabilityQueries == true
            ? candidateRoute
            : null;
        var inputRoute = candidateRoute is not null &&
                         (candidateRoute.CanRouteCapabilityQueries || candidateRoute.CanRouteClipboard)
            ? candidateRoute
            : null;
        var negotiationBaseline = queryRoute?.Policy.OuterProfile?.Capabilities ??
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
        using var router = inputRoute is null
            ? new ProtocolRouter(routeSink, inputOptions, _timeProvider)
            : new ProtocolRouter(routeSink, inputRoute, inputOptions, _timeProvider);
        Dimensions? localDimensions = null;

        if (_resize.TryReadCurrent(out var currentDimensions))
        {
            localDimensions = currentDimensions;
            router.SetGeometry(currentDimensions.Cells, currentDimensions.Pixels);
        }

        if (negotiator is null)
        {
            modes = await EnableOptionalAsync(
                    _context.Profile.Capabilities,
                    router,
                    modes,
                    cancellationToken)
                .ConfigureAwait(false);
            PublishModes(modes);
        }
        else
        {
            var queries = new ArrayBufferWriter<byte>();
            var started = negotiator.TryStart(
                queries,
                localDimensions?.Cells,
                localDimensions?.Pixels,
                queryRoute,
                _context.Profile.Description.Name);

            if (started)
            {
                // A low caller-supplied query budget can crowd the CSI 6n cursor-position fence
                // out of the written batch entirely (it is registered last, after every other
                // family). Gating only on FenceQueried - not on the whole negotiation window -
                // matters: a modified F3 keystroke arriving while the flag is set is classified
                // as a query reply instead of a key, so enabling this when no fence was actually
                // sent would misclassify and silently swallow the keystroke for no reason, one
                // the fix this flag exists for was written to eliminate.
                if (negotiator.FenceQueried)
                {
                    router.EnableCursorPositionQuery();
                }

                await _transport.WriteAsync(queries.WrittenMemory, cancellationToken)
                    .ConfigureAwait(false);
                await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _ = PublishNegotiation(negotiator, router);
            }
        }

        var buffer = ArrayPool<byte>.Shared.Rent(_options.ReadBufferSize);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var deadline = negotiator is null || negotiator.Completed
            ? null
            : DelayUntilAsync(negotiator.Deadline, linked.Token);
        var ready = negotiator is null || negotiator.Completed;
        var hasPendingResize = false;
        var pendingResize = default(Dimensions);

        // A source that only ever reports changes from ReadAsync (never seeding an initial
        // observation itself) must still unblock Application's readiness gate, which is driven
        // exclusively by ISink.Resize. Route the synchronous TryReadCurrent snapshot through the
        // same path as an ordinary resize event instead of leaving it feeding the router alone.
        if (localDimensions is { } snapshot)
        {
            if (ready)
            {
                _sink.Resize(in snapshot);
            }
            else
            {
                pendingResize = snapshot;
                hasPendingResize = true;
            }
        }

        // The loop tasks are declared outside the try so cleanup can drain the read
        // that still borrows the rented buffer and observe every abandoned task.
        Task<int>? read = null;
        Task<Dimensions>? resize = null;
        Task? escapeExpiry = null;
        Task? keyMatcherExpiry = null;
        Task? ss3Expiry = null;
        Task? mouseExpiry = null;
        var preferResize = false;

        // Capability publication is the startup barrier. Input may refine the
        // negotiator immediately, while only the newest pre-publication resize
        // is retained so controls never observe geometry under a stale profile.
        try
        {
            read = _transport.ReadAsync(
                buffer.AsMemory(0, _options.ReadBufferSize),
                linked.Token).AsTask();
            resize = _resize.ReadAsync(linked.Token).AsTask();

            while (true)
            {
                // Task.WhenAny returns whichever awaited task the runtime observes complete
                // first; when read and resize are both already complete at the moment it is
                // called (a transport or resize source that completes synchronously), it always
                // resolved to whichever was passed first — read here — regardless of the other's
                // readiness. A synchronous read burst could then monopolize the loop forever,
                // starving resize and an elapsed negotiation deadline. Re-checking the
                // ready set explicitly on every iteration, instead of trusting WhenAny's pick
                // among already-completed tasks, restores bounded fairness: an elapsed deadline
                // always wins outright, and read/resize alternate when both are simultaneously
                // ready, so neither can starve the other for more than one iteration.
                var deadlineReady = deadline is not null && deadline.IsCompleted;
                var escapeReady = escapeExpiry is not null && escapeExpiry.IsCompleted;
                var keyMatcherReady = keyMatcherExpiry is not null && keyMatcherExpiry.IsCompleted;
                var ss3Ready = ss3Expiry is not null && ss3Expiry.IsCompleted;
                var mouseReady = mouseExpiry is not null && mouseExpiry.IsCompleted;
                Task completed;

                if (!deadlineReady && !escapeReady && !keyMatcherReady && !ss3Ready && !mouseReady && !read.IsCompleted && !resize.IsCompleted)
                {
                    completed = (deadline, escapeExpiry, keyMatcherExpiry, ss3Expiry, mouseExpiry) switch
                    {
                        (null, null, null, null, null) => await Task.WhenAny(read, resize).ConfigureAwait(false),
                        ({ } negotiation, null, null, null, null) => await Task.WhenAny(read, resize, negotiation).ConfigureAwait(false),
                        (null, { } escape, null, null, null) => await Task.WhenAny(read, resize, escape).ConfigureAwait(false),
                        (null, null, { } keyMatcher, null, null) => await Task.WhenAny(read, resize, keyMatcher).ConfigureAwait(false),
                        (null, null, null, { } ss3, null) => await Task.WhenAny(read, resize, ss3).ConfigureAwait(false),
                        (null, null, null, null, { } mouse) => await Task.WhenAny(read, resize, mouse).ConfigureAwait(false),
                        ({ } negotiation, { } escape, null, null, null) => await Task.WhenAny(read, resize, negotiation, escape).ConfigureAwait(false),
                        ({ } negotiation, null, { } keyMatcher, null, null) => await Task.WhenAny(read, resize, negotiation, keyMatcher).ConfigureAwait(false),
                        ({ } negotiation, null, null, { } ss3, null) => await Task.WhenAny(read, resize, negotiation, ss3).ConfigureAwait(false),
                        ({ } negotiation, null, null, null, { } mouse) => await Task.WhenAny(read, resize, negotiation, mouse).ConfigureAwait(false),
                        (null, { } escape, { } keyMatcher, null, null) => await Task.WhenAny(read, resize, escape, keyMatcher).ConfigureAwait(false),
                        (null, { } escape, null, { } ss3, null) => await Task.WhenAny(read, resize, escape, ss3).ConfigureAwait(false),
                        (null, { } escape, null, null, { } mouse) => await Task.WhenAny(read, resize, escape, mouse).ConfigureAwait(false),
                        (null, null, { } keyMatcher, { } ss3, null) => await Task.WhenAny(read, resize, keyMatcher, ss3).ConfigureAwait(false),
                        (null, null, { } keyMatcher, null, { } mouse) => await Task.WhenAny(read, resize, keyMatcher, mouse).ConfigureAwait(false),
                        (null, null, null, { } ss3, { } mouse) => await Task.WhenAny(read, resize, ss3, mouse).ConfigureAwait(false),
                        ({ } negotiation, { } escape, { } keyMatcher, null, null) => await Task.WhenAny(read, resize, negotiation, escape, keyMatcher).ConfigureAwait(false),
                        ({ } negotiation, { } escape, null, { } ss3, null) => await Task.WhenAny(read, resize, negotiation, escape, ss3).ConfigureAwait(false),
                        ({ } negotiation, { } escape, null, null, { } mouse) => await Task.WhenAny(read, resize, negotiation, escape, mouse).ConfigureAwait(false),
                        ({ } negotiation, null, { } keyMatcher, { } ss3, null) => await Task.WhenAny(read, resize, negotiation, keyMatcher, ss3).ConfigureAwait(false),
                        ({ } negotiation, null, { } keyMatcher, null, { } mouse) => await Task.WhenAny(read, resize, negotiation, keyMatcher, mouse).ConfigureAwait(false),
                        ({ } negotiation, null, null, { } ss3, { } mouse) => await Task.WhenAny(read, resize, negotiation, ss3, mouse).ConfigureAwait(false),
                        (null, { } escape, { } keyMatcher, { } ss3, null) => await Task.WhenAny(read, resize, escape, keyMatcher, ss3).ConfigureAwait(false),
                        (null, { } escape, { } keyMatcher, null, { } mouse) => await Task.WhenAny(read, resize, escape, keyMatcher, mouse).ConfigureAwait(false),
                        (null, { } escape, null, { } ss3, { } mouse) => await Task.WhenAny(read, resize, escape, ss3, mouse).ConfigureAwait(false),
                        (null, null, { } keyMatcher, { } ss3, { } mouse) => await Task.WhenAny(read, resize, keyMatcher, ss3, mouse).ConfigureAwait(false),
                        ({ } negotiation, { } escape, { } keyMatcher, { } ss3, null) => await Task.WhenAny(read, resize, negotiation, escape, keyMatcher, ss3).ConfigureAwait(false),
                        ({ } negotiation, { } escape, { } keyMatcher, null, { } mouse) => await Task.WhenAny(read, resize, negotiation, escape, keyMatcher, mouse).ConfigureAwait(false),
                        ({ } negotiation, { } escape, null, { } ss3, { } mouse) => await Task.WhenAny(read, resize, negotiation, escape, ss3, mouse).ConfigureAwait(false),
                        ({ } negotiation, null, { } keyMatcher, { } ss3, { } mouse) => await Task.WhenAny(read, resize, negotiation, keyMatcher, ss3, mouse).ConfigureAwait(false),
                        (null, { } escape, { } keyMatcher, { } ss3, { } mouse) => await Task.WhenAny(read, resize, escape, keyMatcher, ss3, mouse).ConfigureAwait(false),
                        var (negotiation, escape, keyMatcher, ss3, mouse) => await Task.WhenAny(read, resize, negotiation!, escape!, keyMatcher!, ss3!, mouse!).ConfigureAwait(false)
                    };
                }
                else
                {
                    completed = deadlineReady switch
                    {
                        true => deadline!,
                        false when escapeReady => escapeExpiry!,
                        false when keyMatcherReady => keyMatcherExpiry!,
                        false when ss3Ready => ss3Expiry!,
                        false when mouseReady => mouseExpiry!,
                        false when read.IsCompleted && resize.IsCompleted => preferResize ? resize : read,
                        false when read.IsCompleted => read,
                        _ => resize
                    };

                    if (read.IsCompleted && resize.IsCompleted)
                    {
                        preferResize = !preferResize;
                    }
                }

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

                    var capabilities = PublishNegotiation(negotiator, router);
                    modes = await EnableOptionalAsync(
                            capabilities,
                            router,
                            Diagnostics.Modes,
                            linked.Token)
                        .ConfigureAwait(false);
                    PublishModes(modes);
                    ready = true;
                    deadline = null;

                    Dimensions? forwardedDimensions;
                    bool forwardedIsLive;
                    (forwardedDimensions, forwardedIsLive, resize) = await ResolveReadyResizeAsync(
                        resize,
                        hasPendingResize,
                        pendingResize,
                        linked.Token).ConfigureAwait(false);

                    if (forwardedDimensions is { } dimensions)
                    {
                        if (forwardedIsLive)
                        {
                            router.SetGeometry(dimensions.Cells, dimensions.Pixels);
                        }

                        _sink.Resize(in dimensions);
                        hasPendingResize = false;
                    }

                    continue;
                }

                if (escapeExpiry is not null && ReferenceEquals(completed, escapeExpiry))
                {
                    await escapeExpiry.ConfigureAwait(false);

                    // A timer callback may fire before the provider's wall clock reaches the
                    // ambiguity deadline - the same slack the negotiation deadline tolerates
                    // above. ExpireEscape then emits nothing and the pending deadline is simply
                    // re-armed; when it does emit, the pending deadline is gone and the wake-up
                    // is retired until the next lone Escape byte.
                    _ = router.ExpireEscape();
                    escapeExpiry = router.PendingEscapeDeadline is { } pendingEscape
                        ? DelayUntilAsync(pendingEscape, linked.Token)
                        : null;
                    continue;
                }

                if (keyMatcherExpiry is not null && ReferenceEquals(completed, keyMatcherExpiry))
                {
                    await keyMatcherExpiry.ConfigureAwait(false);

                    // A timer callback may fire before the provider's wall clock reaches the
                    // ambiguity deadline - the same slack the Escape deadline tolerates above.
                    // ExpireKeyMatcher then resolves nothing and the pending deadline is simply
                    // re-armed; when it does resolve, the pending deadline is gone and the
                    // wake-up is retired until the next ambiguous fallback byte.
                    _ = router.ExpireKeyMatcher();
                    keyMatcherExpiry = router.PendingKeyMatcherDeadline is { } pendingKeyMatcher
                        ? DelayUntilAsync(pendingKeyMatcher, linked.Token)
                        : null;
                    continue;
                }

                if (ss3Expiry is not null && ReferenceEquals(completed, ss3Expiry))
                {
                    await ss3Expiry.ConfigureAwait(false);

                    // A timer callback may fire before the provider's wall clock reaches the
                    // ambiguity deadline - the same slack the Escape and key-matcher deadlines
                    // tolerate above. ExpireSs3 then resolves nothing and the pending deadline is
                    // simply re-armed; when it does resolve, the pending deadline is gone and the
                    // wake-up is retired until the next ambiguous SS3 continuation.
                    _ = router.ExpireSs3();
                    ss3Expiry = router.PendingSs3Deadline is { } pendingSs3
                        ? DelayUntilAsync(pendingSs3, linked.Token)
                        : null;
                    continue;
                }

                if (mouseExpiry is not null && ReferenceEquals(completed, mouseExpiry))
                {
                    await mouseExpiry.ConfigureAwait(false);

                    // A timer callback may fire before the provider's wall clock reaches the
                    // ambiguity deadline - the same slack the Escape, key-matcher, and SS3
                    // deadlines tolerate above. ExpireMouse then resolves nothing and the pending
                    // deadline is simply re-armed; when it does resolve, the pending deadline is
                    // gone and the wake-up is retired until the next pending X10 mouse report.
                    _ = router.ExpireMouse();
                    mouseExpiry = router.PendingMouseDeadline is { } pendingMouse
                        ? DelayUntilAsync(pendingMouse, linked.Token)
                        : null;
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
                        _ = PublishNegotiation(negotiator, router);
                        ready = true;
                        deadline = null;

                        if (resize.IsCompleted)
                        {
                            // A resize that becomes ready in the same tick as this EOF read
                            // must still be observed, even while negotiation was still
                            // pending. Newest wins: this same-tick resize supersedes any
                            // earlier one already buffered into pendingResize above.
                            var dimensions = await resize.ConfigureAwait(false);
                            router.SetGeometry(dimensions.Cells, dimensions.Pixels);
                            _sink.Resize(in dimensions);
                            hasPendingResize = false;
                        }
                        else if (hasPendingResize)
                        {
                            _sink.Resize(in pendingResize);
                            hasPendingResize = false;
                        }
                    }
                    else if (resize.IsCompleted)
                    {
                        // A resize that becomes ready in the same tick as this EOF read must
                        // still be observed. Once ready, resize events forward immediately
                        // rather than buffering into hasPendingResize/pendingResize above, so
                        // without this check a resize that only ever wins a tie against a
                        // closing read would be silently dropped by the early return below.
                        var dimensions = await resize.ConfigureAwait(false);
                        router.SetGeometry(dimensions.Cells, dimensions.Pixels);
                        _sink.Resize(in dimensions);
                    }

                    _sink.Closed();
                    return;
                }

                router.Route(buffer.AsSpan(0, count));

                // Routing may have begun, refined, or consumed an ambiguous lone Escape. Mirror
                // the decoder's pending deadline into a wake-up so the Escape is emitted even
                // when no further byte ever arrives - an Escape-to-dismiss keypress must not
                // wait for the next keystroke to be delivered.
                escapeExpiry = router.PendingEscapeDeadline is { } escapeDeadline
                    ? DelayUntilAsync(escapeDeadline, linked.Token)
                    : null;

                // Routing may have begun, refined, or resolved an ambiguous fallback key match.
                // Mirror the decoder's pending deadline into a wake-up so the longest completed
                // binding is emitted even when no further byte ever arrives - a keystroke whose
                // sequence happens to prefix another described key must not wait for the next
                // keystroke to be delivered.
                keyMatcherExpiry = router.PendingKeyMatcherDeadline is { } keyMatcherDeadline
                    ? DelayUntilAsync(keyMatcherDeadline, linked.Token)
                    : null;

                // Routing may have begun, refined, or resolved an ambiguous SS3 continuation.
                // Mirror the decoder's pending deadline into a wake-up so the underlying key is
                // emitted even when no further byte ever arrives - an F1-F4 press or a cursor key
                // in application-cursor-keys mode must not wait for the next keystroke to be
                // delivered.
                ss3Expiry = router.PendingSs3Deadline is { } ss3Deadline
                    ? DelayUntilAsync(ss3Deadline, linked.Token)
                    : null;

                // Routing may have begun a pending X10 mouse report. Mirror the decoder's pending
                // deadline into a wake-up so the pending report is resolved even when no further
                // byte ever arrives - a stalled terminal must not leave later real keystrokes
                // silently consumed as the missing coordinate bytes.
                mouseExpiry = router.PendingMouseDeadline is { } mouseDeadline
                    ? DelayUntilAsync(mouseDeadline, linked.Token)
                    : null;

                Debug.Assert(ready || negotiator is not null, "Incomplete startup always owns a negotiator.");

                if (!ready && negotiator!.Completed)
                {
                    var capabilities = PublishNegotiation(negotiator, router);
                    modes = await EnableOptionalAsync(
                            capabilities,
                            router,
                            Diagnostics.Modes,
                            linked.Token)
                        .ConfigureAwait(false);
                    PublishModes(modes);
                    ready = true;
                    deadline = null;

                    Dimensions? forwardedDimensions;
                    bool forwardedIsLive;
                    (forwardedDimensions, forwardedIsLive, resize) = await ResolveReadyResizeAsync(
                        resize,
                        hasPendingResize,
                        pendingResize,
                        linked.Token).ConfigureAwait(false);

                    if (forwardedDimensions is { } dimensions)
                    {
                        if (forwardedIsLive)
                        {
                            router.SetGeometry(dimensions.Cells, dimensions.Pixels);
                        }

                        _sink.Resize(in dimensions);
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

            // ITransport borrows the read destination until the returned operation completes,
            // and cancellation is only a request. A sink failure, an optional-mode failure, or
            // a transport whose cancellation completes asynchronously can therefore reach this
            // point with the read still owning the rental. Returning it here would let the pool
            // reissue storage a live read can still fill, and clearArray would write zeroes into
            // storage that read is still filling. Drain first, and permanently abandon the array
            // when a non-cooperative transport outlives the bounded shutdown budget.
            Observe(resize);
            Observe(deadline);
            Observe(escapeExpiry);
            Observe(keyMatcherExpiry);
            Observe(ss3Expiry);
            Observe(mouseExpiry);

            if (await DrainAsync(read).ConfigureAwait(false))
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Resolves which resize to forward when a not-yet-ready startup transitions to ready.
    /// </summary>
    /// <remarks>
    /// A resize that already completed live, while the caller was awaiting the work that flips
    /// readiness, supersedes anything buffered earlier: newest wins. When the live task is the
    /// one consumed, its read is reissued here so the ordinary resize branch does not see the
    /// same already-completed task a second time on the next loop iteration and double-process
    /// it via <c>ReferenceEquals(completed, resize)</c>.
    /// </remarks>
    private async ValueTask<(Dimensions? Dimensions, bool IsLive, Task<Dimensions> Resize)> ResolveReadyResizeAsync(
        Task<Dimensions> resize,
        bool hasPendingResize,
        Dimensions pendingResize,
        CancellationToken cancellationToken)
    {
        if (resize.IsCompleted)
        {
            var dimensions = await resize.ConfigureAwait(false);
            return (dimensions, true, _resize.ReadAsync(cancellationToken).AsTask());
        }

        return hasPendingResize ? (pendingResize, false, resize) : (null, false, resize);
    }

    private async ValueTask<bool> DrainAsync(Task<int>? read)
    {
        // Awaits the outstanding read to terminal completion and reports whether the rented
        // input buffer is free again.
        if (read is null)
        {
            return true;
        }

        if (!read.IsCompleted)
        {
            // Shutdown stays bounded by the cleanup timeout. A transport that neither completes
            // nor honors cancellation within that budget permanently costs one pooled array,
            // which is strictly cheaper than publishing storage it can still write into.
            using var expiry = new CancellationTokenSource();
            var budget = Task.Delay(_options.CleanupTimeout, _timeProvider, expiry.Token);
            var completed = await Task.WhenAny(read, budget).ConfigureAwait(false);
            await expiry.CancelAsync().ConfigureAwait(false);
            Observe(budget);

            if (!ReferenceEquals(completed, read))
            {
                Observe(read);
                return false;
            }
        }

        Observe(read);
        return true;
    }

    private static void Observe(Task? task)
    {
        // The event loop drops its read, resize, and deadline tasks on every exit path. An
        // unobserved faulted task would otherwise surface much later as a process-wide
        // TaskScheduler.UnobservedTaskException unrelated to the real failure.
        if (task is null)
        {
            return;
        }

        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed =>
            {
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
        var timeout = new CancellationTokenSource(
            _options.CleanupTimeout,
            _timeProvider);
        var renewed = false;

        try
        {
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

                    // An ordinary write failure affects only its own lease, so the walk continues
                    // against the same budget. Cancellation is different: the shared token stays
                    // cancelled, so every remaining write would throw without emitting a byte.
                    // That silently abandons the tail of a reverse walk - and because alternate
                    // screen and cursor policy are leased first, they unwind last, so the two
                    // restores whose loss a user actually sees are the first two lost. Renew once
                    // for the remainder, which keeps shutdown bounded at twice the configured
                    // budget rather than letting one stalled write strand the terminal on the
                    // alternate screen with a hidden cursor.
                    if (!renewed && index > 0 && timeout.IsCancellationRequested)
                    {
                        renewed = true;
                        timeout.Dispose();
                        timeout = new CancellationTokenSource(_options.CleanupTimeout, _timeProvider);
                    }
                }
            }
        }
        finally
        {
            timeout.Dispose();
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
            ? capabilities.PixelMouse.Supported
            : capabilities.CellMouse.Supported;
    }

    private TerminalCapabilities PublishNegotiation(Negotiator negotiator, ProtocolRouter router)
    {
        Debug.Assert(negotiator.Completed, "Only a completed negotiation can publish diagnostics.");

        // Every completion path funnels through here, so this is the one place that reliably
        // closes the cursor-position query window regardless of which of the four routes above
        // (route-encoding failure, deadline expiry, transport EOF, or an in-band match) actually
        // finished negotiation.
        router.DisableCursorPositionQuery();
        var capabilities = negotiator.Capabilities;
        _context = _context.WithCapabilities(capabilities);
        Diagnostics = Diagnostics.WithNegotiation(
            TerminalNegotiationState.Completed,
            negotiator.Results,
            capabilities);
        _sink.Diagnostics(Diagnostics);
        _sink.Profile(capabilities);
        return capabilities;
    }

    private void PublishModes(TerminalModeDiagnostics modes)
    {
        if (ReferenceEquals(Diagnostics.Modes, modes))
        {
            return;
        }

        Diagnostics = Diagnostics.WithModes(modes);
        _sink.Diagnostics(Diagnostics);
    }

    private static TerminalDiagnostics CreateDiagnostics(
        TerminalOptions options,
        TerminalContext context)
    {
        var family = context.Backend.Kind switch
        {
            TerminalBackendKind.Vt => TerminalBackendFamily.Vt,
            TerminalBackendKind.Xterm => TerminalBackendFamily.Xterm,
            TerminalBackendKind.Kitty => TerminalBackendFamily.Kitty,
            TerminalBackendKind.Iterm2 => TerminalBackendFamily.Iterm2,
            _ => throw new UnreachableException("Backend resolution validates every terminal family.")
        };
        var evidence = context.BackendEvidence.Select(static item => new TerminalBackendEvidence(
            item.Kind switch
            {
                TerminalBackendKind.Vt => TerminalBackendFamily.Vt,
                TerminalBackendKind.Xterm => TerminalBackendFamily.Xterm,
                TerminalBackendKind.Kitty => TerminalBackendFamily.Kitty,
                TerminalBackendKind.Iterm2 => TerminalBackendFamily.Iterm2,
                _ => throw new UnreachableException("Backend evidence validates every terminal family.")
            },
            item.Origin switch
            {
                BackendEvidenceOrigin.Description => TerminalBackendEvidenceSource.Description,
                BackendEvidenceOrigin.Environment => TerminalBackendEvidenceSource.Environment,
                _ => throw new UnreachableException("Backend evidence validates every source.")
            })).ToArray();
        var extensions = context.Backend.Extensions.Select(static extension => extension.Kind switch
        {
            ProtocolExtensionKind.Vt => TerminalProtocolExtension.Vt,
            ProtocolExtensionKind.Xterm => TerminalProtocolExtension.Xterm,
            ProtocolExtensionKind.Kitty => TerminalProtocolExtension.Kitty,
            ProtocolExtensionKind.Iterm2 => TerminalProtocolExtension.Iterm2,
            _ => throw new UnreachableException("Backend composition validates every extension.")
        }).ToArray();
        var policy = options.Multiplexing ?? options.Negotiation?.Multiplexing;

        return new TerminalDiagnostics(
            family,
            context.Backend.Name,
            evidence,
            extensions,
            options.Negotiation is null
                ? TerminalNegotiationState.Disabled
                : TerminalNegotiationState.Pending,
            queryResults: null,
            new TerminalRouteDiagnostics(policy),
            new TerminalModeDiagnostics(options, context.Profile.Capabilities),
            TerminalGraphicsBackend.CellFallback);
    }

    private static TerminalOptions RequireOptions(TerminalOptions options)
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
