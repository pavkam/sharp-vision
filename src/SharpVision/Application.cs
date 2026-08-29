// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Input;
using SharpVision.Runtime;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Runtime;
using SharpVision.Windows;

using Terminal.Capabilities;
using Terminal.Clipboard;
using Terminal.Rendering;

using MultiplexerRoute = Terminal.Multiplexing.MultiplexerRoute;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;
using TerminalCellMetrics = CellMetrics;
using TerminalDiagnostic = Diagnostic;
using TerminalDiagnosticCode = DiagnosticCode;
using TerminalResponse = XtermCapabilitiesResponse;
using TerminalSequence = ProtocolSequence;
using TerminalSequenceKind = SequenceKind;


/// <summary>Owns the dispatcher-affine UI tree and asynchronous terminal runtime.</summary>
[DebuggerDisplay("Application {_screen?.GetType().Name,nq}")]
[PublicAPI]
public sealed class Application:
    ISink,
    IPaletteResponseSink,
    IMetricsResponseSink,
    IStatusResponseSink,
    ICapabilityResponseSink,
    IKittyGraphicsResponseSink,
    IClipboardReplySink,
    IKittyClipboardPacketSink,
    IAsyncDisposable
{
    private const int _inputCapacity = 4096;
    private const int _keyboardRevealLayoutPassLimit = 3;
    private readonly Lock _gate = new();
    private readonly Queue<Record> _input = new();
    private readonly ITransport _transport;
    private readonly TerminalOptions _options;
    private readonly MultiplexerRoute? _multiplexerRoute;
    private readonly IAsyncDisposable? _hostLease;
    private readonly TimeProvider _timeProvider;
    private Renderer? _renderer;
    private readonly Action<KeyEventArgs> _initializeModalKey;
    private readonly LayoutEngine _engine = new();
    private readonly CancellationTokenSource _lifetime = new();

    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ArrayBufferWriter<byte> _outOfBand = new();
    private IDisposable? _clipboardShortcutRegistration;
    private AccessKeyManager? _accessKeys;
    private ShortcutManager? _shortcuts;
    private KeyEventArgs? _routedKeyArgs;
    private ControlBase? _routedKeyTarget;
    private bool _suppressPairedText;
    private string _clipboardText = string.Empty;
    private readonly TerminalServices _terminalServices;
    private Dimensions _latestResize;
    private TerminalCellMetrics? _cellMetrics;
    private TerminalCapabilities? _pendingProfile;
    private Task _sessionTask = Task.CompletedTask;
    private Task _renderTask = Task.CompletedTask;
    private bool _inputWake;
    private bool _profileWake;
    private bool _resizeWake;
    private bool _outOfBandWake;
    private bool _initialized;
    private bool _renderRequested;
    private bool _layoutSinceLastRender;
    private bool _rendererInvalidationPending;
    private bool _startedRaised;
    private bool _raisingStopping;
    private bool _forcedWhileRaising;

    // Per-raise-cycle signal for a same-thread nested StopAsync call absorbed by the
    // _raisingStopping reentrancy guard in BeginStopping: freshly allocated right before Stopping
    // is raised, and resolved only after that raise's outcome (proceeded to real stopping, or
    // canceled) is final. A nested caller awaits this instead of reading _stopping mid-raise,
    // where it is still guaranteed false regardless of how the outer call concludes. Non-null only
    // while a raise for the current cycle is in flight or its outcome has not yet been observed.
    private TaskCompletionSource? _stoppingRaiseSignal;

    // Monotonic, in this order: _stopped implies _stopping. FinalizeStopped is the sole writer of
    // both once it is reached, so no path can produce "stopped but not stopping" - a state the
    // lifecycle model cannot express and that no single guard can represent.
    private volatile bool _stopping;
    private volatile bool _stopped;
    private int _startState;
    private int _disposeState;
    private int _hostLeaseDisposed;

    // Non-null only when the constructor's observeProcessSignals parameter opted this instance
    // into owning its own Ctrl+C/SIGTERM/SIGHUP lifecycle - see the constructor's remarks and
    // RequestCooperativeStop. _signalRequestedStopBeforeStart is set from an arbitrary
    // signal-handling thread and only ever read once, at the top of StartAsync.
    private readonly CooperativeShutdownSignals? _processSignals;
    private int _processSignalsDisposed;
    private volatile bool _signalRequestedStopBeforeStart;

    // Set synchronously, on the signal-handling thread, at the very top of every
    // RequestCooperativeStop invocation - unlike _signalRequestedStopBeforeStart above, which only
    // latches before StartAsync. A caller such as ConsoleApplication.RunCoreAsync that registers its
    // own independent, unsynchronized CooperativeShutdownSignals scope around this instance's own
    // cannot rely on its own signal callback (e.g. one that cancels a linked token) racing ahead of
    // - or behind - this instance's RequestCooperativeStop/StopAsync chain: the OS invokes both
    // registrations for the same signal with no ordering guarantee between them. Exposing this flag
    // lets such a caller classify the run's outcome as signal-driven regardless of which of the two
    // independent callbacks happens to finish its own side effects first.
    private volatile bool _stopRequestedBySignal;

    private Theme _theme = ThemeCatalog.Dark;

    private FocusManager? FocusValue { get; set; }

    private PointerManager? CaptureValue { get; set; }

    private ModalityManager? ModalityValue { get; set; }

    private WindowActivationManager? ActivationValue { get; set; }

    /// <summary>Initializes an application that owns all supplied terminal resources.</summary>
    /// <param name="root">The non-null detached root control.</param>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null terminal resize source.</param>
    /// <param name="options">Validated terminal options, or null for defaults.</param>
    /// <param name="hostLease">An optional host resource disposed last after cleanup, or null.</param>
    /// <param name="timeProvider">The optional shared clock for application-owned timed services.</param>
    /// <param name="observeProcessSignals">
    /// Whether this application owns cooperative shutdown for Ctrl+C, <c>SIGTERM</c>, and
    /// <c>SIGHUP</c>. Null (the default) registers nothing here, leaving process-lifecycle signals
    /// entirely to the caller - the right choice for direct construction, and for
    /// <see cref="ConsoleApplication"/>'s two static entry points, which already wrap their own
    /// registration around the whole build-and-run sequence. A non-null value registers, right here
    /// in the constructor so the registration is live for the rest of this instance's life
    /// (including a caller's synchronous attach code that runs before <see cref="StartAsync"/> is
    /// ever reached): <c>SIGTERM</c>/<c>SIGHUP</c> unconditionally on every platform, and Ctrl+C
    /// gated by the value itself - true observes it as cooperative shutdown, false leaves it to
    /// reach the screen as decoded input. A signal observed before <see cref="StartAsync"/> is
    /// called only latches a request that call itself resolves; see
    /// <see cref="RequestCooperativeStop"/>.
    /// <see cref="ConsoleApplicationBuilder.Build"/> passes a non-null value derived from
    /// <see cref="ConsoleRunOptions.TreatControlCAsInput"/>, so every application it builds -
    /// including the bare <c>Build()</c> + <c>RunAsync()</c> shape that has no other registration
    /// wrapping it - gets cooperative shutdown.
    /// </param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="root"/> is attached or already owned.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="root"/> is disposed.</exception>
    /// <exception cref="NotSupportedException">
    /// The terminal profile is not suitable for full-screen use.
    /// </exception>
    public Application(
        ControlBase root,
        ITransport transport,
        IResizeSource resize,
        TerminalOptions? options = null,
        IAsyncDisposable? hostLease = null,
        TimeProvider? timeProvider = null,
        bool? observeProcessSignals = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ObjectDisposedException.ThrowIf(root.IsDisposed, root);

        if (root.Dispatcher is not null || root.Parent is not null || root.OwningSlot is not null)
        {
            throw new ArgumentException("The application root must be detached and unowned.", nameof(root));
        }

        var resolvedOptions = options ?? new TerminalOptions();

        if (resolvedOptions.Profile.Description.Suitability != Suitability.Usable)
        {
            throw new NotSupportedException(
                $"Terminal description '{resolvedOptions.Profile.Description.Name}' is not suitable for full-screen use.");
        }

        // The application owns the terminal viewport. Ordinary descendants
        // remain content-sized unless their parent or caller opts into stretch.
        root.HorizontalAlignment = HorizontalAlignment.Stretch;
        root.VerticalAlignment = VerticalAlignment.Stretch;
        Root = root;
        _transport = transport;
        _options = resolvedOptions;
        var multiplexing = _options.Multiplexing ?? _options.Negotiation?.Multiplexing;
        _multiplexerRoute = multiplexing is { Layers.Count: > 0 }
            ? new MultiplexerRoute(multiplexing)
            : null;
        _hostLease = hostLease;
        _timeProvider = timeProvider ?? TimeProvider.System;
        TerminalProfile = _options.Profile;
        Capabilities = TerminalProfile.Capabilities;
        CellPolicy = new UnicodePolicy(Capabilities.AmbiguousWidth);
        Dispatcher = Dispatcher.Start(name: "SharpVision.UI", timeProvider: _timeProvider);
        Pointer = new PointerDevice(() => CaptureValue);
        _terminalServices = new TerminalServices(this, _multiplexerRoute);
        Terminal = _terminalServices;
        Session = new Session(
            transport,
            resize,
            this,
            _options,
            _timeProvider);
        _initializeModalKey = InitializeModalKey;
        Dispatcher.Idle += OnIdle;
        Dispatcher.UnhandledException += OnDispatcherUnhandled;

        if (observeProcessSignals is { } observeCtrlC)
        {
            _processSignals = CooperativeShutdownSignals.Register(observeCtrlC, RequestCooperativeStop);
            // FinalizeStopped is the sole writer of _stopped and the one funnel every terminal
            // path crosses - the normal BeginStopping-driven stop, a forced Closed(), a fault, and
            // the two paths that never raise Stopping at all - so this fires exactly once
            // regardless of how this instance ends up stopped, without this class needing to
            // duplicate that funnel itself.
            Stopped += (_, _) => DisposeProcessSignals();
        }
    }

    /// <summary>Raised on the dispatcher before terminal startup begins.</summary>
    public event EventHandler? Starting;

    /// <summary>Raised after initial layout and the first committed frame or suspension.</summary>
    public event EventHandler? Started;

    /// <summary>Raised once before explicit or forced shutdown begins.</summary>
    public event EventHandler<StoppingEventArgs>? Stopping;

    /// <summary>Raised once after terminal and renderer cleanup completes.</summary>
    public event EventHandler? Stopped;

    /// <summary>Raised after layout commits new terminal dimensions.</summary>
    public event EventHandler<ResizeEventArgs>? Resize;

    /// <summary>Raised after one renderer write and flush commits.</summary>
    public event EventHandler<FrameRenderedEventArgs>? FrameRendered;

    /// <summary>Raised once on each transition to no ready or pending application work.</summary>
    public event EventHandler? Idle;

    /// <summary>Raised for an application callback or asynchronous terminal failure.</summary>
    public event EventHandler<UnhandledEventArgs>? UnhandledException;

    /// <summary>Raised for immutable redacted terminal protocol diagnostics.</summary>
    public event EventHandler<DiagnosticEventArgs>? Diagnostic;

    /// <summary>
    /// Raised on the dispatcher after one committed frame left graphics placements falling back to
    /// ordinary cells. Not raised when every placement encoded successfully, or when no graphics
    /// backend is configured.
    /// </summary>
    public event EventHandler<GraphicsDiagnosticEventArgs>? GraphicsDiagnostic;

    /// <summary>Raised on the dispatcher after the runtime receives one typed terminal protocol response.</summary>
    public event EventHandler<ProtocolResponseEventArgs>? ResponseReceived;

    /// <summary>Raised on the dispatcher after the runtime receives one typed terminal color response.</summary>
    public event EventHandler<PaletteResponseEventArgs>? PaletteResponseReceived;

    /// <summary>Raised on the dispatcher after the runtime receives one typed terminal metrics response.</summary>
    public event EventHandler<MetricsResponseEventArgs>? MetricsResponseReceived;

    /// <summary>Raised on the dispatcher after the runtime receives one typed terminal status-string response.</summary>
    public event EventHandler<StatusResponseEventArgs>? StatusResponseReceived;

    /// <summary>Raised on the dispatcher after the runtime receives one typed terminal capability-string response.</summary>
    public event EventHandler<CapabilityResponseEventArgs>? CapabilityResponseReceived;

    /// <summary>Raised on the dispatcher after one capability profile becomes active.</summary>
    public event EventHandler<CapabilitiesChangedEventArgs>? CapabilitiesChanged;

    /// <summary>Gets the application-owned UI dispatcher.</summary>
    public Dispatcher Dispatcher { get; }

    /// <summary>Gets the application-owned root control.</summary>
    public ControlBase Root { get; }

    /// <summary>Gets or sets the application-wide theme published to attached controls.</summary>
    /// <exception cref="ArgumentNullException">The assigned theme is null.</exception>
    /// <exception cref="InvalidOperationException">The assigned theme is not frozen, or the application is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The application is disposed.</exception>
    public Theme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.IsFrozen)
            {
                throw new InvalidOperationException("Only a frozen Theme can be published to an application.");
            }

            Dispatcher.VerifyAccess();

            if (ReferenceEquals(_theme, value))
            {
                return;
            }

            ApplyThemeChange(value);
        }
    }

    private void ApplyThemeChange(Theme value)
    {
        if (ReferenceEquals(_theme, value))
        {
            return;
        }

        ObjectDisposedException.ThrowIf(_stopping, this);
        _theme = value;
        PublishTheme();

        if (!_initialized)
        {
            return;
        }

        ProcessInvalidation();
    }

    /// <summary>Gets focus ownership after the first resize attaches the tree.</summary>
    public FocusManager Focus => FocusValue ??
                                 throw new InvalidOperationException("Focus is available after the first resize.");

    /// <summary>Gets the active Window, or null when no available Window is active.</summary>
    /// <remarks>Activation follows primary Window presses and committed focus without changing z-order.</remarks>
    public Window? ActiveWindow => ActivationValue?.ActiveWindow;

    /// <summary>Gets pointer ownership after the first resize attaches the tree.</summary>
    public PointerManager Capture => CaptureValue ??
                                     throw new InvalidOperationException(
                                         "Capture is available after the first resize.");

    /// <summary>Gets modality ownership after the first resize attaches the tree.</summary>
    /// <exception cref="InvalidOperationException">The control tree has not received its first resize.</exception>
    public ModalityManager Modality => ModalityValue ??
        throw new InvalidOperationException("Modality is available after the first resize.");

    /// <summary>Enters one modal scope that confines all input to the specified control subtree.</summary>
    /// <param name="root">The non-null attached control whose subtree becomes the modal plane.</param>
    /// <param name="outsideInteraction">The policy applied to input outside the modal plane.</param>
    /// <param name="initialFocus">An optional eligible focus target inside the root.</param>
    /// <returns>The disposable scope; disposing exits the modal plane.</returns>
    [MustDisposeResource]
    public ModalScope EnterModal(
        ControlBase root,
        OutsideInteraction outsideInteraction = OutsideInteraction.Ignore,
        ControlBase? initialFocus = null) =>
        Modality.Enter(root, outsideInteraction, initialFocus);

    /// <summary>Gets the latest committed terminal size.</summary>
    public Size Size { get; private set; }

    /// <summary>Gets the last observed pointer state and current pointer targets.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public PointerDevice Pointer { get; }

    /// <summary>Gets the implemented terminal output services (bell, clipboard, title).</summary>
    public ITerminalServices Terminal { get; }

    /// <summary>Gets whether the terminal window currently has focus.</summary>
    public bool HasFocus { get; private set; } = true;

    /// <summary>Gets the immutable capability profile used by layout and rendering.</summary>
    public TerminalCapabilities Capabilities { get; private set; }

    /// <summary>Gets the active immutable terminal description, programs, key map, and capabilities.</summary>
    public TerminalProfile TerminalProfile { get; private set; }

    /// <summary>Gets the owned session, exposed only for test seams that need its identity.</summary>
    internal Session Session { get; }

    /// <summary>Installs a renderer directly in place of capability-derived selection, for
    /// synchronous-fault test seams that need a backend double.</summary>
    /// <param name="renderer">The renderer used for subsequent render cycles.</param>
    internal void SeedRenderer(Renderer renderer) => _renderer = renderer;

    /// <summary>
    /// Gets whether a frame render or out-of-band flush is currently in flight. The internal
    /// setter is exercised by every render/out-of-band completion path; the getter is exposed so a
    /// test can prove that a failed post-completion continuation (e.g. the owning dispatcher was
    /// disposed while the render's transport write was still pending) still retires this flag
    /// instead of wedging it permanently true and hanging the shutdown drain loop.
    /// </summary>
    internal bool IsRendering { get; private set; }

    /// <summary>Gets the immutable Unicode cell policy used by the active tree and frame.</summary>
    public UnicodePolicy CellPolicy { get; private set; }

    /// <summary>Gets the configured query limits bounding one outstanding clipboard query.</summary>
    /// <remarks>
    /// Honors a caller-supplied <c>QueryLimits</c> from the negotiation options rather than always
    /// using the default, so a host that widened its query budget for a slow terminal - or a test
    /// that needs the deadline out of the way - gets the limits it configured.
    /// </remarks>
    internal QueryLimits ClipboardQueryLimits => _options.Negotiation?.Limits ?? QueryLimits.Default;

    /// <summary>Gets the deadline applied to one outstanding clipboard query.</summary>
    /// <remarks>
    /// Honors a caller-supplied <c>QueryLimits</c> from the negotiation options rather than always
    /// using the default, so a host that widened its query budget for a slow terminal - or a test
    /// that needs the deadline out of the way - gets the timeout it configured.
    /// </remarks>
    internal TimeSpan ClipboardQueryTimeout => ClipboardQueryLimits.QueryTimeout;

    /// <summary>Gets the configured OSC 52 and Kitty OSC 5522 clipboard transfer limits.</summary>
    /// <remarks>
    /// Honors the caller-supplied <c>Input.TransferLimits</c> from the terminal options rather than
    /// a hardcoded default, so a host that widened its transfer budget for larger clipboard payloads
    /// - or a test that needs a tighter one - gets the limit it configured, distinct from any
    /// hardcoded default a protocol encoder falls back to when no limit is threaded through.
    /// </remarks>
    internal TransferLimits TransferLimits => _options.Input.TransferLimits;

    /// <summary>Gets whether this run requested and can authoritatively lease Kitty paste events.</summary>
    internal bool ClipboardPasteEventsEnabled =>
        _options.ClipboardPasteEvents &&
        Capabilities.KittyClipboard.Authoritative &&
        (_multiplexerRoute is null || _multiplexerRoute.CanRouteClipboard);

    /// <summary>Gets the first primary runtime failure.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Gets the first secondary terminal or renderer cleanup failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets completion after stopped callbacks finish.</summary>
    public Task Completion => _completion.Task;

    /// <summary>
    /// Gets whether a process signal ever drove this instance's own <see cref="RequestCooperativeStop"/>
    /// callback, set synchronously on the signal-handling thread. Only meaningful when this instance
    /// was constructed with a non-null <c>observeProcessSignals</c> (see the constructor's remarks);
    /// otherwise this instance never registers that callback and the flag stays false.
    /// </summary>
    /// <remarks>
    /// A caller such as <see cref="ConsoleApplication"/> that registers its own independent
    /// <see cref="CooperativeShutdownSignals"/> scope around this instance cannot rely on the
    /// ordering between its own signal callback and this instance's - the OS invokes both for the
    /// same signal with no guarantee which finishes its side effects first (see
    /// <see cref="RequestCooperativeStop"/>). Checking this flag alongside its own token lets it
    /// classify the run as signal-driven regardless of that race.
    /// </remarks>
    internal bool StopRequestedBySignal => _stopRequestedBySignal;

    /// <summary>Starts terminal modes and waits for initial committed UI state.</summary>
    /// <param name="cancellationToken">Cancels the startup wait.</param>
    /// <exception cref="InvalidOperationException">The application was already started.</exception>
    /// <exception cref="ObjectDisposedException">The application is disposed.</exception>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        if (Interlocked.CompareExchange(ref _startState, 1, 0) != 0)
        {
            throw new InvalidOperationException("The application was already started.");
        }

        if (_signalRequestedStopBeforeStart)
        {
            // The constructor's process-signal registration (see CooperativeShutdownSignals)
            // already requested shutdown before this call even began - e.g. a signal landing
            // during Build()'s synchronous OnAttach, which runs before the caller can ever reach
            // StartAsync. Nothing observable ever started, so this finishes exactly like the
            // already-cancelled-token catch below does, without throwing: there is nothing to
            // report as cancelled to a caller whose own token never requested it.
            await FinishWithoutSessionAsync();
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                () => Starting?.Invoke(this, EventArgs.Empty),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A token already cancelled before this first await (e.g. a signal that fired
            // before Build() even returned) must resolve as a graceful cancellation, not a
            // Failure - Completion has to complete successfully so the caller's own StopAsync
            // (called after catching this rethrow) observes it cleanly instead of rethrowing a
            // stale cancellation out of an unrelated cleanup wait.
            await FinishWithoutSessionAsync();
            throw;
        }
        catch (Exception exception)
        {
            Failure = exception;
            await FinishWithoutSessionAsync();
            throw;
        }

        _sessionTask = Session.RunAsync(_lifetime.Token).AsTask();
        _ = ObserveSessionAsync();

        try
        {
            var completed = await Task.WhenAny(_started.Task, _completion.Task)
                .WaitAsync(cancellationToken);
            await completed.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Unlike the Starting-handler guard above, the session and dispatcher are already
            // live by this point; a cancellation landing here must not leave them running
            // unobserved by the caller.
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Requests idempotent shutdown and waits for complete cleanup.</summary>
    /// <remarks>
    /// The shutdown request is irrevocable and the token bounds only this caller's observation of
    /// it. Cancelling — including passing an already-cancelled token — may end the wait with
    /// <see cref="OperationCanceledException"/>, but shutdown still proceeds: <see cref="Stopping"/>
    /// is raised, cleanup runs, <see cref="Completion"/> finishes, and owned resources are disposed.
    /// </remarks>
    /// <param name="cancellationToken">Cancels only the caller's wait.</param>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _startState) == 0)
        {
            return;
        }

        if (!_stopping)
        {
            // Forwarding the caller token to the dispatcher would let it cancel the request
            // itself, so an already-cancelled token would leave the application running with
            // Completion pending — the opposite of what the caller asked for. The request is
            // always queued unconditionally and the token is applied only to the wait below.
            var request = Dispatcher.InvokeAsync(
                () => BeginStopping(forced: false, exception: null),
                CancellationToken.None).AsTask();

            Task? raiseSignal;

            try
            {
                raiseSignal = await request.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The queued request still runs. Observe it so a lifecycle-handler failure
                // cannot resurface later as an unobserved task exception.
                Observe(request);

                throw;
            }

            if (raiseSignal is not null)
            {
                // A Stopping handler already raising on this same thread absorbed the request
                // (BeginStopping's _raisingStopping reentrancy guard) - _stopping is still
                // guaranteed false here no matter how far that outer raise has progressed on the
                // call stack above this one. Wait for that raise's own per-cycle outcome instead
                // of racing it on a snapshot that is stale by construction; once it settles,
                // _stopping (and, if it proceeded, eventually _completion) reflect the real,
                // finished outcome for the check below.
                await raiseSignal.WaitAsync(cancellationToken);
            }
        }

        if (_stopping)
        {
            await _completion.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The callback registered process signals invoke (see the constructor), returning the task
    /// that represents the requested stop finishing. Runs on an arbitrary signal-handling thread.
    /// </summary>
    /// <remarks>
    /// When nothing has started yet - a signal landing during <c>Build()</c>'s synchronous
    /// <c>OnAttach</c>, or in the gap before the caller reaches <see cref="StartAsync"/> at all -
    /// there is no live session or <see cref="Stopping"/> veto to honor ("the application never
    /// ran"), so this only latches a flag <see cref="StartAsync"/> itself checks first. Once
    /// started, <see cref="StopAsync"/> is the same cooperative path an external caller uses;
    /// <see cref="Observe"/> swallows the resulting task so an abandoned await here never surfaces
    /// as an unobserved-exception crash, and <see cref="StopAsync"/>'s own dispatcher-affine
    /// queuing and <c>_stopping</c> guard make it safe to invoke from this thread. Returning the
    /// same task (rather than only observing it) lets <c>CooperativeShutdownSignals</c> block on it
    /// for the Windows <c>SIGTERM</c>/<c>SIGHUP</c> registration - see
    /// <c>CooperativeShutdownSignals.InvokeTerminationSignal</c> - while every other registration
    /// (Ctrl+C on every platform, and Unix's own <c>SIGTERM</c>/<c>SIGHUP</c>) still fires it and
    /// moves on without waiting, exactly as before.
    /// <para>
    /// <see cref="StopAsync"/> is itself <c>async</c>, so an exception from its own body - including
    /// one <c>Dispatcher.InvokeAsync</c> can throw synchronously for a disposed or full dispatcher -
    /// is captured into the returned <see cref="ValueTask"/> rather than thrown here, and
    /// <see cref="Observe"/> already swallows a pre-faulted task exactly like one that faults later.
    /// The try/catch below is defense in depth against that invariant ever changing: this callback
    /// must never throw before returning a task, and <c>CooperativeShutdownSignals</c> is
    /// responsible for never letting that returned task's own fault escape onto the
    /// signal-handling thread either (it awaits and swallows it for the blocking Windows path; the
    /// non-blocking paths already rely on <see cref="Observe"/> here for the same reason).
    /// </para>
    /// </remarks>
    private Task RequestCooperativeStop()
    {
        _stopRequestedBySignal = true;

        if (Volatile.Read(ref _startState) == 0)
        {
            _signalRequestedStopBeforeStart = true;
        }

        Task task;

        try
        {
            task = StopAsync(CancellationToken.None).AsTask();
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            // See the remarks above: not expected to be reachable given StopAsync's async
            // signature, but this callback must never throw onto the signal-handling thread.
            return Task.CompletedTask;
        }

        Observe(task);

        return task;
    }

    /// <summary>Unregisters this application's process-signal scope at most once.</summary>
    private void DisposeProcessSignals()
    {
        if (Interlocked.Exchange(ref _processSignalsDisposed, 1) == 0)
        {
            _processSignals?.Dispose();
        }
    }

    /// <summary>Starts the application, waits for completion, and stops it.</summary>
    /// <param name="cancellationToken">Requests shutdown.</param>
    /// <returns>The complete run; faults with the primary failure when one occurred.</returns>
    /// <exception cref="InvalidOperationException">The application was already started.</exception>
    /// <exception cref="ObjectDisposedException">The application is disposed.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // StartAsync has already stopped and cleaned up a session that reached the live
            // state before this cancellation landed; nothing further to await here.
            return;
        }

        try
        {
            await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Stops and releases every application-owned resource.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _startState) == 0 && !_stopped)
            {
                await FinishWithoutSessionAsync();
            }
            else if (!_stopped)
            {
                try
                {
                    await StopAsync();
                }
                catch when (_stopped)
                {
                    // Once cleanup has actually completed, this is StopAsync's documented rethrow
                    // of the recorded Failure - swallowing it is DisposeAsync's own contract. A
                    // throw that lands before cleanup completed (e.g. a dead dispatcher) must not
                    // be reported as success, so it is left to propagate instead.
                }
            }
        }
        finally
        {
            await Dispatcher.DisposeAsync();
            _lifetime.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Input(in Stroke value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Input(in TerminalText value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public void Input(in Pointer value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Input(Paste value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Input(in TerminalFocus value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Input(in TerminalDiagnostic value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Response(in TerminalResponse value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public void Response(in PaletteResponse value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(value));
        }

        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public void Response(in MetricsResponse value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(value));
        }

        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public void Response(in StatusResponse value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(value));
        }

        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public void Response(CapabilityResponse value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public void Response(Terminal.Kitty.Graphics.KittyGraphicsResponse value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    public void Response(in ClipboardReply value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public void Response(Terminal.Kitty.Clipboard.KittyClipboardPacket value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Enqueue(Record.From(value));
    }

    /// <inheritdoc/>
    public void Sequence(TerminalSequence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var discardedBytes = checked(
            (long) value.Parameters.Length +
            value.Intermediates.Length +
            value.Payload.Length +
            (value.Kind == TerminalSequenceKind.Dcs ? 1 : 0));
        Input(new TerminalDiagnostic(
            TerminalDiagnosticCode.Unsupported,
            value.Kind,
            offset: 0,
            discardedBytes));
    }

    /// <inheritdoc/>
    public void Profile(TerminalCapabilities value)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _pendingProfile = value;

            if (_profileWake)
            {
                return;
            }

            _profileWake = true;
        }

        PostOrResetWake(DrainProfile, () => _profileWake = false);
    }

    /// <inheritdoc/>
    void ISink.Resize(in Dimensions value)
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _latestResize = value;
            if (_resizeWake)
            {
                return;
            }

            _resizeWake = true;
        }

        PostOrResetWake(DrainResize, () => _resizeWake = false);
    }

    /// <summary>Requests a cooperative, orderly application exit.</summary>
    /// <remarks>
    /// The discoverable, intention-named call for application code - an Exit button, an Escape
    /// handler - as opposed to <see cref="Closed"/>, which is the <see cref="ISink"/> transport
    /// callback for reporting that terminal input closed. Both funnel through the identical stop
    /// path today; <see cref="Closed"/> stays free to evolve its transport-facing semantics
    /// independently of this public lifecycle surface.
    /// </remarks>
    public void Shutdown() => Closed();

    /// <inheritdoc/>
    /// <remarks>Application code that wants to exit should call <see cref="Shutdown"/> instead - this
    /// member exists to satisfy the <see cref="ISink"/> transport contract.</remarks>
    public void Closed() => Enqueue(Record.Closed());

    /// <summary>Forces the renderer to redraw the entire screen from a clean baseline instead of a
    /// differential update against its cached state.</summary>
    /// <remarks>
    /// The supported recovery when something outside the framework's control - another process
    /// writing to the same terminal, a resumed session - has left the display out of sync with
    /// what the renderer believes is on screen: the classic Ctrl-L scenario, which a terminal UI
    /// needs more than a GUI does because it does not own the display exclusively.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The application is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The application is stopping or stopped.</exception>
    public void RefreshScreen()
    {
        Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_stopping, this);

        if (_renderer is not null)
        {
            _rendererInvalidationPending = true;
        }

        if (!_initialized)
        {
            return;
        }

        Root.Invalidate(Invalidation.Render);
        ProcessInvalidation();
    }

    /// <inheritdoc/>
    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Enqueue(Record.Fault(exception));
    }

    private Task? BeginStopping(bool forced, Exception? exception)
    {
        Dispatcher.VerifyAccess();

        if (_stopping)
        {
            Failure ??= exception;
            return null;
        }

        if (_raisingStopping)
        {
            // A Stopping handler requested shutdown again. Dispatcher invocation runs inline on
            // the dispatcher thread, so without this guard the nested request would raise the
            // same cancellable event again and recurse until the stack is exhausted. One stop
            // request raises Stopping exactly once; a nested forced request must still override
            // a handler that cancelled this one. Handing back the in-progress raise's own signal
            // (rather than nothing) lets a nested caller wait for that raise's real outcome
            // instead of reading _stopping mid-raise, where it is still guaranteed false no
            // matter how the outer call concludes.
            _forcedWhileRaising |= forced;
            Failure ??= exception;

            return _stoppingRaiseSignal!.Task;
        }

        var eventArgs = new StoppingEventArgs();
        _raisingStopping = true;
        var raiseSignal = new TaskCompletionSource();
        _stoppingRaiseSignal = raiseSignal;

        try
        {
            Stopping?.Invoke(this, eventArgs);
        }
        catch (Exception stoppingException)
        {
            // A Stopping handler failure must not skip the cleanup chain below - it is recorded
            // rather than propagated, so shutdown proceeds exactly as if the handler had returned
            // without cancelling.
            Failure ??= stoppingException;
            LastCleanupException ??= stoppingException;
        }
        finally
        {
            _raisingStopping = false;
        }

        if (eventArgs.Cancel && !forced && !_forcedWhileRaising)
        {
            _stoppingRaiseSignal = null;
            raiseSignal.SetResult();

            return null;
        }

        _forcedWhileRaising = false;
        _stopping = true;
        Failure ??= exception;
        _lifetime.Cancel();

        _stoppingRaiseSignal = null;
        raiseSignal.SetResult();

        return null;
    }

    private static void Observe(Task task)
    {
        // An abandoned lifecycle task must never surface later as a process-wide
        // TaskScheduler.UnobservedTaskException unrelated to the real failure.
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

    private void CompleteRender(
        Frame frame,
        IDisposable hold,
        TaskCompletionSource completion,
        RenderMetrics? metrics,
        Exception? exception)
    {
        Dispatcher.VerifyAccess();

        try
        {
            frame.Dispose();
            IsRendering = false;

            if (exception is not null)
            {
                _renderer?.Invalidate();
                _rendererInvalidationPending = false;

                if (exception is not OperationCanceledException || !_lifetime.IsCancellationRequested)
                {
                    Report(exception);
                }
                else if (_stopping && HasPendingOutOfBand())
                {
                    // A frame render genuinely in flight when a stop commits does not reach the
                    // ordinary completion path below at all: BeginStopping's _lifetime.Cancel()
                    // (run in the same synchronous step that sets _stopping) cancels this frame's
                    // own still-outstanding transport write/flush, which is bound to the same
                    // token, so it surfaces here as the benign cancellation this branch already
                    // exists to swallow - never as a successful completion. Out-of-band bytes
                    // buffered behind that same in-flight frame (Bell/SetTitle/clipboard OSC
                    // writes) must still be flushed from here for the same reason documented on
                    // FlushOutOfBandOnStop below: nothing downstream of this method ever looks at
                    // _outOfBand again.
                    FlushOutOfBandOnStop();
                }

                return;
            }

            // Isolated so a throwing handler cannot skip what follows. Losing MarkStarted and the
            // pump left _renderRequested and Root.Pending unserviced with IsRendering already
            // cleared, and OnIdle's own !_startedRaised gate then declined to recover it - a plain
            // Invalidate() produced no frame at all until the next keystroke.
            //
            // Reported immediately rather than at the end, so the _stopping guards below stay the
            // single arbiter of whether the application may still advance. A failure left
            // unhandled forces shutdown and they correctly decline; a handled one - the documented
            // way to keep a surviving application running, and the only way this defect is
            // reachable - leaves _stopping false and every transition runs as it must.
            if (RaiseIsolated(FrameRendered, new FrameRenderedEventArgs(metrics!.Value)) is { } frameFailure)
            {
                Report(frameFailure);
            }

            if (metrics!.Value.GraphicsDiagnostics.Count != 0 &&
                RaiseIsolated(() => Enqueue(Record.From(metrics.Value.GraphicsDiagnostics))) is { } enqueueFailure)
            {
                // The bounded input queue can be full here, which is the one trigger for this
                // whole failure shape that needs no consumer handler at all.
                Report(enqueueFailure);
            }

            if (!_stopping)
            {
                MarkStarted();
            }

            if (!_stopping && HasPendingOutOfBand())
            {
                FlushOutOfBand();
            }
            else if (_stopping && HasPendingOutOfBand())
            {
                // A stop can commit while this very frame's write was in flight (BeginStopping
                // runs on the dispatcher thread and can only observe _stopping/_lifetime between
                // dispatcher turns, never mid-write). Nothing downstream of here - FinalizeStopped,
                // ObserveSessionAsync, DisposeTerminalResourcesAsync - ever looks at _outOfBand
                // again, so bytes queued behind this frame (Bell/SetTitle/clipboard OSC writes)
                // would otherwise be silently and permanently dropped. FlushOutOfBand itself is not
                // reusable for this: it writes with _lifetime.Token, which BeginStopping already
                // cancelled in the same synchronous step that set _stopping, so handing it to the
                // transport now would fault immediately with an OperationCanceledException that
                // CompleteOutOfBand's _lifetime.IsCancellationRequested check then treats as
                // unreportable - stranding the bytes through a different silent path instead.
                FlushOutOfBandOnStop();
            }
            else if (_renderRequested || Root.Pending != Invalidation.None)
            {
                _renderRequested = false;
                ProcessInvalidation();
            }
        }
        finally
        {
            hold.Dispose();
            _ = completion.TrySetResult();
        }
    }

    /// <summary>
    /// Flushes out-of-band bytes still buffered when a stop commits between an in-flight write -
    /// a frame render's (<see cref="CompleteRender"/>) or an earlier out-of-band write's
    /// (<see cref="PumpAfterWrite"/>) - completing and that caller deciding what runs next. Mirrors
    /// <see cref="FlushOutOfBand"/>'s <see cref="IsRendering"/>/<c>_renderTask</c> bookkeeping so
    /// <see cref="ObserveSessionAsync"/>'s shutdown drain (<c>do { await _renderTask; } while
    /// (IsRendering);</c>) waits for this write before <see cref="DisposeTerminalResourcesAsync"/>
    /// tears the transport down - which is otherwise still alive and open here, since that same
    /// drain is what let teardown reach this point in the first place.
    /// </summary>
    private void FlushOutOfBandOnStop()
    {
        Dispatcher.VerifyAccess();
        Debug.Assert(!IsRendering, "Out-of-band flush must not overlap a frame render.");

        byte[] payload;

        lock (_gate)
        {
            if (_outOfBand.WrittenCount == 0)
            {
                return;
            }

            payload = _outOfBand.WrittenSpan.ToArray();
            _outOfBand.Clear();
        }

        var hold = Dispatcher.Hold();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        IsRendering = true;
        _ = ObserveOutOfBandOnStopAsync(payload, hold, completion);
    }

    /// <summary>
    /// Writes and observes a <see cref="FlushOutOfBandOnStop"/> payload, then retires it the same
    /// way <see cref="ObserveOutOfBandAsync"/>/<see cref="CompleteOutOfBand"/> retire a normal
    /// out-of-band write - except the write itself uses a fresh, <see
    /// cref="TerminalOptions.CleanupTimeout"/>-bounded token (the same pattern
    /// <see cref="DisposeTerminalResourcesAsync"/> already uses for <c>renderer.ShutdownAsync</c>)
    /// rather than the already-cancelled <c>_lifetime.Token</c>, and any failure is folded into
    /// <see cref="LastCleanupException"/> - this codebase's convention for shutdown-path
    /// diagnostics - rather than routed through <see cref="Report"/>, which exists for a still
    /// running application and no longer applies once <c>_stopping</c> is set.
    /// </summary>
    private async Task ObserveOutOfBandOnStopAsync(byte[] payload, IDisposable hold, TaskCompletionSource completion)
    {
        Exception? failure = null;

        try
        {
            using var timeout = new CancellationTokenSource(_options.CleanupTimeout, _timeProvider);
            await _transport.WriteAsync(payload, timeout.Token).ConfigureAwait(false);
            await _transport.FlushAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        Dispatcher.PostBackgroundCompletion(
            () =>
            {
                Dispatcher.VerifyAccess();

                try
                {
                    IsRendering = false;
                    LastCleanupException ??= failure;
                }
                finally
                {
                    hold.Dispose();
                    _ = completion.TrySetResult();
                }
            },
            () =>
            {
                // ObserveOutOfBandOnStopAsync's own completion never ran, so - as with the matching
                // retirement in ObserveRenderAsync/ObserveOutOfBandAsync - IsRendering must be put
                // back here or ObserveSessionAsync's shutdown drain would spin forever on an
                // IsRendering left permanently true.
                IsRendering = false;
                LastCleanupException ??= failure;
                hold.Dispose();
                _ = completion.TrySetResult();
            });
    }

    private void Dispatch(Record record)
    {
        // Only a new keystroke resets suppression. Resetting on every non-Text
        // record would strand an armed suppression when an unrelated concurrent producer -
        // resize, a diagnostic, a fault - is enqueued between a consumed stroke and its
        // paired text record(s), which are otherwise always adjacent in dispatch order.
        if (record.Kind == RecordKind.Key)
        {
            _suppressPairedText = false;
        }

        switch (record.Kind)
        {
            case RecordKind.Key:
                {
                    if (_shortcuts?.Process(record.Stroke) == true)
                    {
                        SuppressPairedText();
                        break;
                    }

                    var keyTarget = Modality.KeyTarget(Focus.Focused);
                    var isModal = Modality.Active is not null;
                    var eventArgs = new KeyEventArgs(record.Stroke);
                    var previousArgs = _routedKeyArgs;
                    var previousTarget = _routedKeyTarget;
                    RouteResult result;
                    _routedKeyArgs = eventArgs;
                    _routedKeyTarget = keyTarget;

                    try
                    {
                        result = isModal
                            ? Router.RouteInitialized(
                                keyTarget,
                                Events.Key,
                                eventArgs,
                                _initializeModalKey)
                            : Router.Route(keyTarget, Events.Key, eventArgs);
                    }
                    finally
                    {
                        _routedKeyArgs = previousArgs;
                        _routedKeyTarget = previousTarget;
                    }

                    if (result.Command is PostRouteCommand.TabNext or PostRouteCommand.TabPrevious)
                    {
                        // The route continues bubbling after a control requests this command
                        // (Router.cs never truncates the walk), so a handledEventsToo ancestor
                        // handler can legally detach the captured anchor before it runs here.
                        // Fall back to the focused-control overload rather than let a stale
                        // anchor fault the whole application.
                        var anchor = result.Anchor is { } candidate && Focus.IsMember(candidate)
                            ? candidate
                            : null;
                        _ = anchor is not null
                            ? Focus.MoveNext(anchor, result.Command == PostRouteCommand.TabPrevious)
                            : Focus.MoveNext(result.Command == PostRouteCommand.TabPrevious);
                    }

                    // A stroke consumed anywhere on or around its route - a control default
                    // setting IsHandled, a preview-phase clipboard shortcut, or (below) an
                    // access key discovered only after the route itself found no handler -
                    // never delivers its paired associated-text record(s); every one of those
                    // paths previously left this unarmed except the menu shortcut above and
                    // the access-key fallback below.
                    if (result.IsHandled)
                    {
                        SuppressPairedText();
                    }
                    else if (_accessKeys?.Process(record.Stroke) == true)
                    {
                        SuppressPairedText();
                    }
                }

                break;
            case RecordKind.Text:
                if (_suppressPairedText)
                {
                    // A single consumed stroke can be paired with more than one text record -
                    // Kitty associated text emits one record per colon-separated scalar - so
                    // every consecutive text record is dropped, not just the first,
                    // until the next keystroke re-arms or clears suppression.
                    break;
                }

                if (Modality.FocusedTarget(Focus.Focused) is { } textTarget)
                {
                    _ = Router.Route(textTarget, Events.Text, new TextEventArgs(record.Text));
                }

                break;
            case RecordKind.Pointer:
                Pointer.Observe(record.Pointer);
                _ = Capture.Dispatch(record.Pointer);
                break;
            case RecordKind.Paste:
                if (Modality.FocusedTarget(Focus.Focused) is { } pasteTarget)
                {
                    Debug.Assert(record.Paste.HasValue, "A paste record must carry its payload.");
                    _ = Router.Route(
                        pasteTarget,
                        Events.Paste,
                        new PasteEventArgs(record.Paste.GetValueOrDefault()));
                }

                break;
            case RecordKind.Focus:
                HasFocus = record.Focus.Gained;

                if (!record.Focus.Gained)
                {
                    Capture.TerminalFocusLost();
                }

                _ = Router.Route(
                    Modality.KeyTarget(Focus.Focused),
                    Events.TerminalFocusChanged,
                    new TerminalFocusEventArgs(record.Focus));
                break;
            case RecordKind.Diagnostic:
                Diagnostic?.Invoke(this, new DiagnosticEventArgs(record.Diagnostic));
                break;
            case RecordKind.Response:
                ResponseReceived?.Invoke(this, new ProtocolResponseEventArgs(record.Response));
                break;
            case RecordKind.PaletteResponse:
                PaletteResponseReceived?.Invoke(
                    this,
                    new PaletteResponseEventArgs(record.PaletteResponse));
                break;
            case RecordKind.MetricsResponse:
                MetricsResponseReceived?.Invoke(
                    this,
                    new MetricsResponseEventArgs(record.MetricsResponse));
                break;
            case RecordKind.StatusResponse:
                StatusResponseReceived?.Invoke(
                    this,
                    new StatusResponseEventArgs(record.StatusResponse));
                break;
            case RecordKind.CapabilityResponse:
                CapabilityResponseReceived?.Invoke(
                    this,
                    new CapabilityResponseEventArgs(record.CapabilityResponse!));
                break;
            case RecordKind.KittyGraphicsResponse:
                _renderer?.AcceptKittyGraphicsResponse(record.KittyGraphicsResponse!);
                break;
            case RecordKind.ClipboardReply:
                _terminalServices.ReceiveClipboardReply(record.ClipboardReply);
                break;
            case RecordKind.KittyClipboardPacket:
                _terminalServices.ReceiveKittyClipboardPacket(record.KittyClipboardPacket!);
                break;
            case RecordKind.GraphicsDiagnostic:
                GraphicsDiagnostic?.Invoke(this, new GraphicsDiagnosticEventArgs(record.GraphicsDiagnostics));
                break;
            case RecordKind.Closed:
                _ = BeginStopping(forced: true, exception: null);
                break;
            case RecordKind.Fault:
                Report(record.Exception!);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Arms suppression of the current stroke's paired text record(s) - the
    /// associated-text record(s) the terminal reports alongside a key when the decoder emits
    /// both - so they are dropped rather than routed, because the stroke was consumed somewhere
    /// on or around its route (a menu shortcut, an access key, or ordinary routing including a
    /// control default). Unconditional: whether the stroke itself carries a character is
    /// unrelated to whether the terminal also reports Kitty associated text alongside it, so
    /// gating on <c>Stroke.Character</c> keyed on the wrong fact.</summary>
    private void SuppressPairedText() => _suppressPairedText = true;

    private void OnClipboardShortcut(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;
        var target = ReferenceEquals(eventArgs, _routedKeyArgs)
            ? _routedKeyTarget
            : ReferenceEquals(Focus.Focused, eventArgs.OriginalSource)
                ? eventArgs.OriginalSource
                : null;

        if (eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.IsHandled ||
            target is null ||
            !TryProcessClipboardShortcut(target, eventArgs.Stroke))
        {
            return;
        }

        eventArgs.IsHandled = true;
    }

    private void InitializeModalKey(KeyEventArgs eventArgs)
    {
        Debug.Assert(
            ReferenceEquals(eventArgs, _routedKeyArgs),
            "Modal key initialization owns the arguments published for the captured application route.");
        var target = _routedKeyTarget;
        Debug.Assert(target is not null, "Modal key initialization retains its captured target.");

        if (TryProcessClipboardShortcut(target, eventArgs.Stroke))
        {
            eventArgs.IsHandled = true;
        }
    }

    private bool TryProcessClipboardShortcut(ControlBase keyTarget, Stroke stroke)
    {
        if (stroke.Action != KeyAction.Press ||
            stroke.Code != Code.Character ||
            stroke.Character is not { } character ||
            !KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Control))
        {
            return false;
        }

        var command = Rune.ToLowerInvariant(character);

        if (command == new Rune('c'))
        {
            var boundary = Modality.BoundaryFor(keyTarget);
            if (!TryCopySelectedText(keyTarget, boundary, out var copied))
            {
                return false;
            }

            PublishClipboard(copied);
            return true;
        }

        if (keyTarget is not TextInput input)
        {
            return false;
        }

        if (command == new Rune('x'))
        {
            PublishClipboard(input.CutSelection());
        }
        else if (command == new Rune('v'))
        {
            input.PasteClipboard(_clipboardText);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool TryCopySelectedText(
        ControlBase keyTarget,
        ControlBase? boundary,
        [NotNullWhen(true)] out string? copied)
    {
        for (var target = keyTarget; target is not null; target = target.Parent)
        {
            if (target.IsTextSelectionEnabled)
            {
                copied = target.CopySelectedText();
                return true;
            }

            if (target is IClipboardCopySource source)
            {
                copied = source.CopySelection();
                return true;
            }

            if (ReferenceEquals(target, boundary))
            {
                break;
            }
        }

        copied = null;
        return false;
    }

    private void OnContextMenuPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.IsHandled ||
            eventArgs.Pointer.Action != PointerAction.Press ||
            !eventArgs.Pointer.Buttons.HasFlag(Buttons.Secondary))
        {
            return;
        }

        for (var target = eventArgs.OriginalSource; target is not null; target = target.Parent)
        {
            if (target.ContextMenu is IClipboardWriterContextMenu clipboardMenu)
            {
                clipboardMenu.ClipboardWriter = PublishClipboard;
            }

            if (target is TextInput { ContextMenu: TextInputContextMenu contextMenu })
            {
                contextMenu.ClipboardReader = () => _clipboardText;
                return;
            }

            if (target.ContextMenu is IClipboardWriterContextMenu)
            {
                return;
            }
        }
    }

    private void PublishClipboard(string value)
    {
        Debug.Assert(value is not null, "Clipboard operations return owned non-null text.");

        if (value.Length == 0)
        {
            return;
        }

        _clipboardText = value;
        Terminal.Clipboard.Write(value);
    }

    private void DrainInput()
    {
        Dispatcher.VerifyAccess();

        if (!_initialized)
        {
            lock (_gate)
            {
                _inputWake = false;
            }

            return;
        }

        // A throwing Dispatch leaves this loop by exception, so both the latch reset and
        // ProcessInvalidation must run from finally - otherwise _inputWake stays true forever and
        // Enqueue's early-return short-circuits every future record, deafening a still-running
        // application whose UnhandledException handler chose to survive the throw.
        try
        {
            while (true)
            {
                Record record;

                lock (_gate)
                {
                    if (!_input.TryDequeue(out record))
                    {
                        break;
                    }
                }

                if (_stopping)
                {
                    // This record passed Enqueue's admission check while _stopping was still
                    // false, but a Closed/Fault record dispatched earlier in this same pass has
                    // since flipped the flag - most likely both were enqueued by concurrent
                    // producers a moment apart and happened to land in that order. Routing more
                    // input into a tree that is mid-teardown is reasonably avoided, so the skip
                    // itself stays intentional; only its silence was the defect.
                    //
                    // Report(...) is not the right channel to fix that: it sets Failure and
                    // raises UnhandledException, both reserved for a genuine failure that should
                    // force shutdown, and firing them here would misrepresent an ordinary,
                    // successful stop as an application failure - exactly the false alarm a
                    // consumer reacting to UnhandledException would not expect from closing down
                    // cleanly. The Diagnostic/GraphicsDiagnostic events are not a fit either; both
                    // carry unrelated terminal-protocol and render-metrics payloads, not "a record
                    // was dropped because the application is stopping." So, like
                    // DrainInputRaceHookForTests above, this stays a test-only seam rather than a
                    // new public signal.
                    DrainInputSkippedRecordHookForTests?.Invoke(record);
                }
                else
                {
                    Dispatch(record);
                }
            }

            DrainInputRaceHookForTests?.Invoke();
        }
        finally
        {
            // The reset and the "did another record arrive since" check must be one atomic
            // section under _gate, mirroring WakeInput. Resetting the latch and re-checking the
            // queue as two separate lock acquisitions leaves a window between this loop's last
            // TryDequeue (queue empty, lock released) and the reset below where a concurrent
            // Enqueue observes _inputWake still true and skips its repost, believing this drain
            // will still see the record it just added - stranding that record.
            var repost = false;

            lock (_gate)
            {
                _inputWake = false;

                if (_input.Count > 0)
                {
                    _inputWake = true;
                    repost = true;
                }
            }

            ProcessInvalidation();

            if (repost)
            {
                PostOrResetWake(DrainInput, () => _inputWake = false);
            }
        }
    }

    /// <summary>
    /// Test-only synchronization seam. When set, invoked once on <see cref="DrainInput"/>'s normal
    /// (non-throwing) exit path, immediately after the drain loop observes the queue empty and
    /// releases the dequeue lock, but before the latch-reset <c>finally</c> block runs. Lets tests
    /// deterministically land a concurrent <see cref="Enqueue"/> inside the reset window instead of
    /// relying on a race that is a handful of CPU instructions wide.
    /// </summary>
    internal Action? DrainInputRaceHookForTests { get; set; }

    /// <summary>
    /// Test-only observability seam. Invoked once, synchronously, for each record this drain
    /// pass dequeues but declines to dispatch because <c>_stopping</c> was already set by
    /// an earlier record in the same pass (typically <see cref="RecordKind.Closed"/> or
    /// <see cref="RecordKind.Fault"/>). Skipping the record is intentional - see the comment at
    /// the call site - but nothing else in this codebase surfaces it, so tests use this hook to
    /// assert the skip actually happened instead of only asserting its absence of side effects.
    /// </summary>
    internal Action<Record>? DrainInputSkippedRecordHookForTests { get; set; }

    private void DrainProfile()
    {
        Dispatcher.VerifyAccess();
        TerminalCapabilities? value;

        lock (_gate)
        {
            _profileWake = false;

            if (!_initialized)
            {
                return;
            }

            value = _pendingProfile;
            _pendingProfile = null;
        }

        if (_stopping || value is null)
        {
            return;
        }

        ApplyCapabilities(value);
    }

    private void DrainResize()
    {
        Dispatcher.VerifyAccess();
        Dimensions value;
        TerminalCapabilities? profile;

        lock (_gate)
        {
            value = _latestResize;
            _resizeWake = false;
            profile = _pendingProfile;
            _pendingProfile = null;
            _profileWake = false;
        }

        if (_stopping)
        {
            return;
        }

        if (profile is not null)
        {
            ApplyCapabilities(profile);

            // A CapabilitiesChanged handler failing unhandled forces shutdown from inside that
            // call. Nothing below may advance a tearing-down application, and before these
            // callbacks were isolated the throw itself abandoned the rest of this method.
            if (_stopping)
            {
                return;
            }
        }

        Root.SetCellMetrics(value.CellMetrics);

        if (!_initialized)
        {
            Root.SetCapabilities(Capabilities);
            Root.Attach(
                Dispatcher,
                CellPolicy,
                _theme,
                () =>
                {
                    FocusValue = new FocusManager(Root);
                    ActivationValue = new WindowActivationManager(Root);
                    CaptureValue = new PointerManager(Root, _timeProvider, ActivationValue.Activate);
                    ModalityValue = new ModalityManager(
                        Root,
                        FocusValue,
                        CaptureValue,
                        target => _ = ActivationValue.Activate(target));
                    _accessKeys = new AccessKeyManager(Root, FocusValue, ModalityValue);
                    _shortcuts = new ShortcutManager(Root, FocusValue, ModalityValue);
                    FocusValue.Lost += OnFocusChanged;
                    FocusValue.Gained += OnFocusChanged;
                });
            _clipboardShortcutRegistration = Root.AddHandler(Events.Key, OnClipboardShortcut);
            _ = Root.AddHandler(Events.Pointer, OnContextMenuPointer);
            _initialized = true;
            WakeInput();
        }

        Size = value.Cells;
        _cellMetrics = value.CellMetrics;
        PerformLayout();
        if (RaiseIsolated(Resize, new ResizeEventArgs(value)) is { } resizeFailure)
        {
            Report(resizeFailure);
        }

        if (_stopping)
        {
            return;
        }

        // The suspended branch is the startup path a change-only resize source takes, so a
        // throwing Resize handler skipping MarkStarted here reproduced the parked-StartAsync hang
        // without FrameRendered being involved at all.
        if (value.Suspended)
        {
            MarkStarted();
            return;
        }

        ProcessInvalidation();
    }

    /// <summary>
    /// Posts <paramref name="work"/> to the dispatcher on behalf of a "latch, then post" wake site
    /// - <see cref="Profile"/>, the resize sink, <see cref="Enqueue"/>, <see cref="WakeInput"/>,
    /// <see cref="PostOutOfBand"/>, and <see cref="DrainInput"/>'s own repost. Every one of those
    /// sites sets its wake flag <c>true</c> under <c>_gate</c> and then leaves the lock
    /// before attempting the post, so that <see cref="Dispatcher.VerifyAccess"/>-guarded drain
    /// method is the only other code that ever touches the flag while a post is outstanding - it
    /// resets the flag to <c>false</c> once the corresponding drain runs, per callback, on the
    /// single dispatcher thread. That single-flight invariant is exactly what makes the reset
    /// below safe without re-checking anything: since the caller only reaches this method after
    /// observing the flag transition from <c>false</c> to <c>true</c> itself (or, for the
    /// <see cref="DrainInput"/> repost, from inside the same locked section that just reset it),
    /// no concurrent caller can have re-armed the flag in the meantime - a second latch attempt
    /// would have seen it already <c>true</c> and returned without posting, and the drain that
    /// would otherwise clear it never got scheduled, because this very post attempt is the one
    /// that failed.
    /// </summary>
    /// <param name="work">The dispatcher callback to post.</param>
    /// <param name="resetWake">Clears the wake flag guarding this post. Invoked under
    /// <c>_gate</c>, and only when <paramref name="work"/> failed to schedule for a reason
    /// other than the dispatcher already being disposed.</param>
    /// <exception cref="InvalidOperationException">The dispatcher queue is full. A subsequent,
    /// ordinary call into the same wake site can retry once the queue drains, exactly as if this
    /// call had never latched the flag in the first place.</exception>
    private void PostOrResetWake(Action work, Action resetWake)
    {
        try
        {
            Dispatcher.Post(work);
        }
        catch (ObjectDisposedException)
        {
            // A disposed dispatcher is terminal - nothing will ever post again, so leaving the
            // flag latched true is harmless; there is no "later, healthy call" left to un-stick.
        }
        catch (Exception)
        {
            // Anything else (a full queue, most commonly) is the transient, recoverable condition
            // this method exists to un-stick: without this reset the flag would stay true forever,
            // and every later call into this site would return silently, never even attempting
            // another post, no matter how healthy the dispatcher becomes afterward.
            lock (_gate)
            {
                resetWake();
            }

            throw;
        }
    }

    private void Enqueue(Record record)
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            if (_input.Count >= _inputCapacity)
            {
                throw new InvalidOperationException("The terminal input queue is full.");
            }

            _input.Enqueue(record);

            if (_inputWake)
            {
                return;
            }

            _inputWake = true;
        }

        PostOrResetWake(DrainInput, () => _inputWake = false);
    }

    private void ApplyCapabilities(TerminalCapabilities value)
    {
        Dispatcher.VerifyAccess();

        if (ReferenceEquals(Capabilities, value))
        {
            return;
        }

        var previous = Capabilities;
        TerminalProfile = TerminalProfile.WithCapabilities(value);
        Capabilities = TerminalProfile.Capabilities;
        var measure = previous.AmbiguousWidth != Capabilities.AmbiguousWidth;
        CellPolicy = new UnicodePolicy(Capabilities.AmbiguousWidth);
        if (_renderer is not null)
        {
            // A live render may have a backend transaction prepared while transport flush is
            // outstanding. Invalidation belongs to the next render boundary, after that
            // transaction has committed or failed. UpdateGraphicsBackend only ever selects a
            // backend when none exists yet - an existing backend already re-checks authoritative
            // support per frame and is left untouched here - but selecting one for the first time
            // still shares the in-flight-render hazard, so it guards against that itself, returning
            // false as a no-op instead of throwing. A race only defers selection to the next
            // capability republish; it does not lose it, and the pending invalidation flag below
            // still forces a full redraw at the next render boundary either way.
            _ = _renderer.UpdateGraphicsBackend(Capabilities, _multiplexerRoute);
            _rendererInvalidationPending = true;
        }

        if (_initialized)
        {
            Root.SetCapabilities(Capabilities);
            Root.SetCellPolicy(CellPolicy);
        }

        if (RaiseIsolated(CapabilitiesChanged, new CapabilitiesChangedEventArgs(previous, Capabilities))
            is { } callbackFailure)
        {
            Report(callbackFailure);
        }

        if (!_initialized || _stopping)
        {
            return;
        }

        Root.Invalidate(measure ? Invalidation.Measure : Invalidation.Render);
        ProcessInvalidation();
    }

    private async Task FinishWithoutSessionAsync()
    {
        var cleanupFailure = await DisposeTerminalResourcesAsync();

        // A transiently full queue is absorbed by the bounded retry instead of escaping straight
        // out of DisposeAsync and masking whatever cleanupFailure it was about to record. Only if
        // the retry is exhausted (see InvokeWithQueueRetryAsync) does this still propagate, exactly
        // as before this loop existed - FinalizeStopped must run on the dispatcher thread and cannot
        // be skipped in place of a fallback the way the terminal-resource disposal above can.
        var postFailure = await InvokeWithQueueRetryAsync(() =>
        {
            Failure ??= cleanupFailure;
            LastCleanupException ??= cleanupFailure;
            FinalizeStopped();
        });

        if (postFailure is not null)
        {
            ExceptionDispatchInfo.Throw(postFailure);
        }
    }

    private async ValueTask DisposeHostLeaseAsync()
    {
        if (_hostLease is not null && Interlocked.Exchange(ref _hostLeaseDisposed, 1) == 0)
        {
            await _hostLease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void PublishTheme() => Root.PropagateTheme(_theme);

    private void OnFocusChanged(object? sender, FocusChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = ActivationValue?.Activate(eventArgs.Current);
    }

    private void FinalizeStopped()
    {
        Dispatcher.VerifyAccess();

        if (_stopped)
        {
            return;
        }

        // Three callers reach here without ever passing through BeginStopping: StartAsync's
        // Starting-handler catch, StartAsync's own early return when a process signal (see
        // CooperativeShutdownSignals and RequestCooperativeStop) already requested shutdown before
        // this call began, and DisposeAsync on an application that was never started. Latching
        // _stopping here - the one funnel every terminal path crosses, already dispatcher-affine, so
        // no new lock and no user callback under one - keeps the pair monotonic. Without it,
        // StopAsync saw a stopped application as merely not-yet-stopping and queued a fresh
        // BeginStopping, raising Stopping *after* Stopped against an already-disposed tree,
        // cancelling a torn-down lifetime, and letting a handler's Cancel suppress the rethrow
        // StopAsync owed its caller.
        //
        // Stopping is therefore not raised at all on those three paths. That is the intended
        // reading - the application never ran - and lifecycle-events.md states it.
        _stopping = true;

        var priorFailure = Failure;
        ExceptionDispatchInfo? cleanupFailure = null;

        if (_initialized)
        {
            CaptureCleanup(() => _clipboardShortcutRegistration?.Dispose(), ref cleanupFailure);
            _clipboardShortcutRegistration = null;
            _clipboardText = string.Empty;
            _accessKeys = null;
            _shortcuts = null;
            _suppressPairedText = false;
            CaptureCleanup(() => ModalityValue?.Shutdown(), ref cleanupFailure);
            CaptureCleanup(() => CaptureValue?.Dispose(), ref cleanupFailure);

            if (FocusValue is { } focus)
            {
                focus.Lost -= OnFocusChanged;
                focus.Gained -= OnFocusChanged;
            }

            CaptureCleanup(() => ActivationValue?.Dispose(), ref cleanupFailure);
            CaptureCleanup(() => FocusValue?.Dispose(), ref cleanupFailure);
        }

        if (_renderer is { } renderer)
        {
            CaptureCleanup(renderer.Dispose, ref cleanupFailure);
        }

        CaptureCleanup(Root.Dispose, ref cleanupFailure);
        var capturedCleanup = cleanupFailure?.SourceException;
        Failure ??= capturedCleanup;
        LastCleanupException ??= Session.LastCleanupException ??
            _renderer?.LastCleanupException ??
            (priorFailure is not null ? capturedCleanup : null);
        _stopped = true;
        CaptureCleanup(() => Stopped?.Invoke(this, EventArgs.Empty), ref cleanupFailure);
        Failure ??= cleanupFailure?.SourceException;

        _ = Failure is { } failure
            ? _completion.TrySetException(failure)
            : _completion.TrySetResult();
    }

    private static void CaptureCleanup(
        Action action,
        ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    /// <summary>Raises one lifecycle callback and returns its failure instead of letting it
    /// propagate through the state transition the caller must still perform.</summary>
    /// <remarks>
    /// The shutdown path already worked this way through <see cref="CaptureCleanup"/>; the startup
    /// and frame paths raised theirs bare, so a handler that threw skipped the line after it. A
    /// consumer is entitled to that throw - marking the failure handled is the documented way to
    /// keep a surviving application running - so the transition, not the callback, is what has to
    /// be protected. The captured exception goes to <see cref="Report"/>, the primary-failure
    /// channel, rather than the cleanup channel <c>CaptureCleanup</c> feeds.
    /// </remarks>
    /// <param name="handler">The event's current invocation list, or null when unsubscribed.</param>
    /// <param name="eventArgs">The arguments to raise with.</param>
    /// <returns>The handler's failure, or null.</returns>
    private Exception? RaiseIsolated(EventHandler? handler, EventArgs eventArgs)
    {
        if (handler is null)
        {
            return null;
        }

        try
        {
            handler(this, eventArgs);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <inheritdoc cref="RaiseIsolated(EventHandler?, EventArgs)"/>
    /// <typeparam name="TArgs">The event argument type.</typeparam>
    private Exception? RaiseIsolated<TArgs>(EventHandler<TArgs>? handler, TArgs eventArgs)
    {
        if (handler is null)
        {
            return null;
        }

        try
        {
            handler(this, eventArgs);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>Runs one non-callback step that can throw, under the same isolation, so it cannot
    /// skip a following transition either.</summary>
    /// <param name="action">The step to run.</param>
    /// <returns>The step's failure, or null.</returns>
    private static Exception? RaiseIsolated(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private void MarkStarted()
    {
        if (_startedRaised)
        {
            return;
        }

        _startedRaised = true;

        // The latch is set before the callback runs and is never cleared, and TrySetResult has no
        // other caller anywhere in this type - so a handler throwing past this point used to
        // strand _started permanently, leaving StartAsync parked on an application that was fully
        // live and Started already raised. Settling it unconditionally is the fix; settling it
        // *before* the callback is not, because _started completes continuations asynchronously
        // and would release StartAsync's awaiter concurrently with the handler still running here.
        // The same reasoning orders the failure report before the settle: settling first releases
        // StartAsync's awaiter concurrently with UnhandledException still running on this thread,
        // so a caller resuming from StartAsync could read state the report is still writing. The
        // finally keeps the no-strand guarantee even if the report path itself throws.
        var failure = RaiseIsolated(Started, EventArgs.Empty);

        try
        {
            if (failure is not null)
            {
                Report(failure);
            }
        }
        finally
        {
            _ = _started.TrySetResult();
        }
    }

    private void OnDispatcherUnhandled(object? sender, UnhandledEventArgs eventArgs)
    {
        eventArgs.IsHandled = true;
        Report(eventArgs.Exception);
    }

    private void OnIdle(object? sender, EventArgs eventArgs)
    {
        if (!_startedRaised || _stopping)
        {
            return;
        }

        if (!IsRendering && !Suspended() && Root.Pending != Invalidation.None)
        {
            ProcessInvalidation();
            return;
        }

        Idle?.Invoke(this, EventArgs.Empty);

        if (!IsRendering && !Suspended() && Root.Pending != Invalidation.None)
        {
            ProcessInvalidation();
        }
    }

    private async Task ObserveRenderAsync(
        ValueTask<RenderMetrics> operation,
        Frame frame,
        IDisposable hold,
        TaskCompletionSource completion)
    {
        RenderMetrics? metrics = null;
        Exception? failure = null;

        try
        {
            // ConfigureAwait(false) is required: this method is started synchronously from
            // StartRender on the dispatcher thread, so without it the continuation captures the
            // ambient DispatcherSynchronizationContext and resumes by posting back through it.
            // That post is documented to silently drop the continuation once the dispatcher is
            // stopping (see DispatcherSynchronizationContext.Post), which would abandon this
            // method entirely - never reaching the retirement below - instead of merely failing
            // the inner Dispatcher.Post call that retirement already guards against.
            metrics = await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        Dispatcher.PostBackgroundCompletion(
            () => CompleteRender(frame, hold, completion, metrics, failure),
            () =>
            {
                // CompleteRender never ran, so its usual `IsRendering = false` never happened
                // either. Leaving `IsRendering` true would wedge ObserveSessionAsync's shutdown
                // drain loop (`do { await _renderTask; } while (IsRendering);`) in an infinite
                // spin, since `_renderTask` (== `completion.Task`) is about to complete below.
                IsRendering = false;
                frame.Dispose();
                hold.Dispose();
                _ = completion.TrySetResult();
            });
    }


    private async Task ObserveSessionAsync()
    {
        Exception? failure = null;

        try
        {
            await _sessionTask;
        }
        catch (Exception exception) when (
            exception is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // BeginStopping's Stopping handler and the render drain both run before terminal-resource
        // cleanup and FinalizeStopped; wrapping them in try/finally keeps that cleanup chain
        // unconditional even if either step above throws unexpectedly, matching the promise that
        // an exit-callback failure cannot skip later application cleanup.
        //
        // This method is launched fire-and-forget and never awaited or observed by anyone, so a
        // full dispatcher queue that escaped either InvokeAsync call below would, at best, become
        // an unobserved task fault and, at worst - for the second call - skip FinalizeStopped
        // entirely, wedging _completion (and everything that awaits Application.Completion or
        // StopAsync) forever. InvokeWithQueueRetryAsync absorbs a merely transient full queue
        // instead of letting either call reach that outcome; only if the queue never drains for the
        // whole CleanupTimeout window does this still propagate, same as before this loop existed.
        try
        {
            var beginStoppingFailure = await InvokeWithQueueRetryAsync(() => BeginStopping(forced: true, failure));

            if (beginStoppingFailure is not null)
            {
                ExceptionDispatchInfo.Throw(beginStoppingFailure);
            }

            do
            {
                await _renderTask;
            } while (IsRendering);
        }
        finally
        {
            var cleanupFailure = await DisposeTerminalResourcesAsync();
            var finalizeFailure = await InvokeWithQueueRetryAsync(() =>
            {
                Failure ??= cleanupFailure;
                LastCleanupException ??= cleanupFailure;
                FinalizeStopped();
            });

            if (finalizeFailure is not null)
            {
                ExceptionDispatchInfo.Throw(finalizeFailure);
            }
        }
    }

    private void PerformLayout()
    {
        _engine.Layout(Root, Size);
        _layoutSinceLastRender = true;

        // Wheel scroll, programmatic scroll, and resize all move geometry under a stationary
        // pointer without dispatching a pointer record of their own, so hover would otherwise
        // keep pointing at whatever was under the cursor before the layout moved.
        CaptureValue?.ReconcileHover();
    }

    private void ProcessInvalidation()
    {
        if (!_initialized || _stopping || Suspended())
        {
            return;
        }

        var keyboardRevealPass = 0;

        while (true)
        {
            if ((Root.Pending & (Invalidation.Measure | Invalidation.Arrange)) != 0)
            {
                PerformLayout();
            }

            if (FocusValue?.ProcessPendingKeyboardReveal() != true)
            {
                break;
            }

            keyboardRevealPass++;

            if (keyboardRevealPass < _keyboardRevealLayoutPassLimit)
            {
                continue;
            }

            FocusValue.CancelPendingKeyboardReveal();

            if ((Root.Pending & (Invalidation.Measure | Invalidation.Arrange)) != 0)
            {
                PerformLayout();
            }

            break;
        }

        if ((Root.Pending & Invalidation.Render) != 0)
        {
            StartRender(skipCleanSubtrees: !_layoutSinceLastRender);
        }
    }

    private void Report(Exception exception)
    {
        Failure ??= exception;
        var eventArgs = new UnhandledEventArgs(exception);

        try
        {
            UnhandledException?.Invoke(this, eventArgs);
        }
        catch (Exception handlerException)
        {
            // A consumer handler that itself throws must be treated as though it never marked
            // the failure handled, so shutdown still proceeds; the handler's own exception is
            // recorded separately from the original Failure rather than propagated.
            eventArgs.IsHandled = false;
            LastCleanupException ??= handlerException;
        }

        if (!eventArgs.IsHandled)
        {
            _ = BeginStopping(forced: true, exception);
        }
    }

    /// <summary>Buffers out-of-band protocol bytes and drains them on the dispatcher.</summary>
    /// <param name="bytes">The exact bytes to write; flushed only when no frame render is in flight.</param>
    internal void PostOutOfBand(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _outOfBand.Write(bytes.Span);

            if (_outOfBandWake)
            {
                return;
            }

            _outOfBandWake = true;
        }

        PostOrResetWake(DrainOutOfBand, () => _outOfBandWake = false);
    }

    private void DrainOutOfBand()
    {
        Dispatcher.VerifyAccess();

        lock (_gate)
        {
            _outOfBandWake = false;
        }

        // A frame render owns the writer; CompleteRender re-drains afterward.
        if (IsRendering || _stopping || Suspended())
        {
            return;
        }

        FlushOutOfBand();
    }

    private void FlushOutOfBand()
    {
        Dispatcher.VerifyAccess();
        Debug.Assert(!IsRendering, "Out-of-band flush must not overlap a frame render.");

        byte[] payload;

        lock (_gate)
        {
            if (_outOfBand.WrittenCount == 0)
            {
                return;
            }

            payload = _outOfBand.WrittenSpan.ToArray();
            _outOfBand.Clear();
        }

        var hold = Dispatcher.Hold();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        IsRendering = true;
        var operation = WriteOutOfBandAsync(payload);
        _ = ObserveOutOfBandAsync(operation, hold, completion);
    }

    private async ValueTask WriteOutOfBandAsync(byte[] payload)
    {
        await _transport.WriteAsync(payload, _lifetime.Token).ConfigureAwait(false);
        await _transport.FlushAsync(_lifetime.Token).ConfigureAwait(false);
    }

    private async Task ObserveOutOfBandAsync(ValueTask operation, IDisposable hold, TaskCompletionSource completion)
    {
        Exception? failure = null;

        try
        {
            // See the matching comment in ObserveRenderAsync: this method also starts
            // synchronously on the dispatcher thread (from FlushOutOfBand), so ConfigureAwait(false)
            // is required to avoid resuming through a DispatcherSynchronizationContext post that is
            // silently dropped once the dispatcher is stopping.
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        Dispatcher.PostBackgroundCompletion(
            () => CompleteOutOfBand(hold, completion, failure),
            () =>
            {
                // CompleteOutOfBand never ran, so its usual `IsRendering = false` never happened
                // either. Leaving `IsRendering` true would wedge ObserveSessionAsync's shutdown
                // drain loop the same way an equivalent failure in ObserveRenderAsync would.
                IsRendering = false;
                hold.Dispose();
                _ = completion.TrySetResult();
            });
    }

    private void CompleteOutOfBand(IDisposable hold, TaskCompletionSource completion, Exception? failure)
    {
        Dispatcher.VerifyAccess();

        try
        {
            IsRendering = false;

            if (failure is not null &&
                (failure is not OperationCanceledException || !_lifetime.IsCancellationRequested))
            {
                Report(failure);

                // No early return here even though the write failed: a handler that marks the
                // failure handled is the documented way to keep a surviving application running,
                // and _stopping stays false in that case, so a render deferred behind this
                // out-of-band write must still be pumped or it is stranded until an unrelated
                // invalidation or idle tick. When the failure went unhandled, Report already
                // latched _stopping above, and PumpAfterWrite's own guard makes this a no-op.
            }

            PumpAfterWrite();
        }
        finally
        {
            hold.Dispose();
            _ = completion.TrySetResult();
        }
    }

    private void PumpAfterWrite()
    {
        if (_stopping)
        {
            // The same stranding this method's caller (CompleteOutOfBand) exists to avoid for a
            // frame render applies identically here: a stop can commit while THIS out-of-band
            // write was itself in flight, and any bytes a later PostOutOfBand buffered behind it
            // (Bell/SetTitle/clipboard OSC writes) must still be flushed - nothing downstream of
            // this method ever looks at _outOfBand again once stopping.
            if (HasPendingOutOfBand())
            {
                FlushOutOfBandOnStop();
            }

            return;
        }

        if (Suspended())
        {
            return;
        }

        if (HasPendingOutOfBand())
        {
            FlushOutOfBand();
            return;
        }

        if (_renderRequested || Root.Pending != Invalidation.None)
        {
            _renderRequested = false;
            ProcessInvalidation();
        }
    }

    private bool HasPendingOutOfBand()
    {
        lock (_gate)
        {
            return _outOfBand.WrittenCount > 0;
        }
    }

    private void StartRender(bool skipCleanSubtrees = false)
    {
        Dispatcher.VerifyAccess();

        if (IsRendering)
        {
            _renderRequested = true;
            return;
        }

        var renderer = _renderer ??= CreateRenderer();

        if (_rendererInvalidationPending)
        {
            renderer.Invalidate();
            _rendererInvalidationPending = false;
        }

        var frame = new Frame(Size, ambiguousWidth: CellPolicy.AmbiguousWidth);

        // Let render-clean subtrees copy their cells on demand instead of re-executing
        // OnRenderContent. After layout, cell positions may have changed, so the caller already
        // resolves skipCleanSubtrees from _layoutSinceLastRender before this call - copying stale
        // cells from the previous frame is avoided at the source, not rechecked here.
        if (skipCleanSubtrees)
        {
            _ = renderer.AttachCommittedFrame(frame);
        }

        _layoutSinceLastRender = false;

        // Set before the call it guards, not after: Root.Render runs application/control code
        // synchronously (event handlers, OnRenderContent overrides) on this same dispatcher
        // thread, and any of it that reaches StartRender again - e.g. a legal RefreshScreen()
        // call mid-paint - must see IsRendering already true so it coalesces through
        // _renderRequested like every other invalidation path, instead of finding the guard
        // still false and reentering Root.Render on the same Root control, which throws
        // InvalidOperationException("Render cannot be reentered.") from ControlBase.RenderCore's
        // own per-control guard.
        IsRendering = true;

        try
        {
            Root.Render(frame.Canvas);
        }
        catch
        {
            // Root.Render never touches the renderer or the terminal - only frame.Canvas - so
            // there is nothing here for the renderer/backend to retire (contrast the RenderAsync
            // catch below, which mirrors CompleteRender's failure path because that call may have
            // already started terminal I/O). This failure just needs IsRendering put back so the
            // application is not permanently wedged believing a render is still in flight.
            frame.Dispose();
            IsRendering = false;
            throw;
        }

        var hold = Dispatcher.Hold();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;

        ValueTask<RenderMetrics> operation;

        try
        {
            operation = renderer.RenderAsync(
                frame,
                _transport,
                TerminalProfile,
                _cellMetrics,
                _lifetime.Token);
        }
        catch
        {
            // RenderAsync is a non-async ValueTask method: a fault raised before its first await
            // (e.g. out of backend Prepare) propagates synchronously here instead of completing
            // the returned ValueTask. Without this catch, hold/completion/IsRendering would never
            // retire and ObserveRenderAsync would never run to dispose the frame, wedging shutdown
            // on an unobservable _renderTask. This performs exactly the retirement
            // CompleteRender would perform for the equivalent asynchronous fault, then rethrows so
            // the single Report call happens where it always does - OnDispatcherUnhandled.
            frame.Dispose();
            IsRendering = false;
            _renderer?.Invalidate();
            _rendererInvalidationPending = false;
            hold.Dispose();
            _ = completion.TrySetResult();
            throw;
        }

        _ = ObserveRenderAsync(operation, frame, hold, completion);
    }

    private Renderer CreateRenderer()
    {
        return new Renderer(
            Capabilities,
            _multiplexerRoute,
            cleanupTimeout: _options.CleanupTimeout,
            timeProvider: _timeProvider);
    }

    private async Task<Exception?> DisposeTerminalResourcesAsync()
    {
        var lifetimeDiagnostic = _renderer?.LastCleanupException ?? Session.LastCleanupException;
        Exception? failure = null;

        // Before the transport teardown below, because a clipboard transaction still in flight owns
        // a periodic DispatcherTimer. Left armed it posts to a stopped dispatcher, swallows the
        // ObjectDisposedException, and re-arms forever - rooting this Application, its dispatcher,
        // renderer, and whole control tree through the timer's own callback, so disposing the
        // Application would not have collected any of it.
        //
        // Must run on the owning dispatcher thread: TerminalServices tracks this pending clipboard
        // state exclusively there (see the threading requirement on TerminalServices.Dispose), but
        // this method itself runs on whatever background thread the Task infrastructure resumed
        // ObserveSessionAsync/FinishWithoutSessionAsync on. Calling Dispose() directly from here
        // would race a live query's own deadline tick - which fires on the dispatcher thread - on
        // the same in-flight, single-threaded transaction with no lock between them. The dispatcher
        // itself is still running at this point in every caller of this method, so this cannot
        // observe it disposed - only a transiently full queue, which InvokeWithQueueRetryAsync
        // retries away instead of letting escape immediately (see its own remarks). Only if the
        // queue somehow never drains for the whole CleanupTimeout window does that retry give up;
        // its exception is folded into failure below rather than rethrown, because permanently
        // skipping this call - not a late, bounded retry of it - is what leaves the periodic
        // clipboard-transaction DispatcherTimer armed and rooting the whole
        // Application/dispatcher/renderer/control tree (see the timer-leak remarks on
        // TerminalServices.Dispose): a last-resort leak is preferable to DisposeAsync throwing
        // outright.
        failure = await InvokeWithQueueRetryAsync(_terminalServices.Dispose).ConfigureAwait(false);

        if (_renderer is { } renderer)
        {
            try
            {
                using var timeout = new CancellationTokenSource(
                    _options.CleanupTimeout,
                    _timeProvider);
                await renderer.ShutdownAsync(_transport, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        try
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }

        try
        {
            await DisposeHostLeaseAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }

        LastCleanupException ??= lifetimeDiagnostic ??
            _renderer?.LastCleanupException ??
            Session.LastCleanupException ??
            failure;
        return failure;
    }

    /// <summary>The delay between successive queue-full retries in
    /// <see cref="InvokeWithQueueRetryAsync"/>.</summary>
    private static readonly TimeSpan _queueRetryDelay = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Invokes <paramref name="action"/> on the dispatcher, retrying while the bounded dispatcher
    /// queue is transiently full instead of letting the first
    /// <see cref="InvalidOperationException"/> ("the dispatcher queue is full") escape immediately.
    /// </summary>
    /// <remarks>
    /// Every call site sharing this helper (<see cref="DisposeTerminalResourcesAsync"/>,
    /// <see cref="FinishWithoutSessionAsync"/>, <see cref="ObserveSessionAsync"/>) runs while the
    /// owning dispatcher thread is confirmed still alive and actively draining its queue, so the
    /// transient full-queue rejection this loop retries is expected to clear well inside
    /// <see cref="TerminalOptions.CleanupTimeout"/> in practice - the same window
    /// <see cref="DisposeTerminalResourcesAsync"/> already bounds <c>renderer.ShutdownAsync</c>
    /// with, reused here for consistency rather than a second, independently-tuned budget.
    /// <see cref="ObjectDisposedException"/> means shutdown genuinely started elsewhere and is left
    /// to propagate immediately, unretried, exactly as before this loop existed - it is not a
    /// transient condition retrying could ever clear. If the queue somehow never drains for the
    /// entire bound, this gives up and returns the last <see cref="InvalidOperationException"/>
    /// instead of retrying forever; each caller decides how to fold that final failure into its own
    /// cleanup bookkeeping rather than hang indefinitely.
    /// </remarks>
    /// <param name="action">The non-null callback to run on the dispatcher.</param>
    /// <returns>Null once <paramref name="action"/> ran; otherwise the final
    /// <see cref="InvalidOperationException"/> once <see cref="TerminalOptions.CleanupTimeout"/>
    /// elapses without the queue accepting it.</returns>
    private async Task<Exception?> InvokeWithQueueRetryAsync(Action action)
    {
        using var timeout = new CancellationTokenSource(_options.CleanupTimeout, _timeProvider);

        while (true)
        {
            try
            {
                await Dispatcher.InvokeAsync(action).ConfigureAwait(false);
                return null;
            }
            catch (InvalidOperationException exception) when (exception is not ObjectDisposedException)
            {
                // ObjectDisposedException derives from InvalidOperationException, so without this
                // guard the catch above would also swallow it here and retry for the full timeout
                // instead of propagating it immediately, as the remarks above promise.
                if (timeout.Token.IsCancellationRequested)
                {
                    return exception;
                }

                try
                {
                    await Task.Delay(_queueRetryDelay, _timeProvider, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return exception;
                }
            }
        }
    }

    private void WakeInput()
    {
        var post = false;

        lock (_gate)
        {
            if (_input.Count > 0 && !_inputWake)
            {
                _inputWake = true;
                post = true;
            }
        }

        if (post)
        {
            PostOrResetWake(DrainInput, () => _inputWake = false);
        }
    }

    private bool Suspended() => Size.Width == 0 || Size.Height == 0;
}
