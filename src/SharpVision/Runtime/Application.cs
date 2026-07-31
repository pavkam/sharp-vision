// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Input;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Runtime;
using SharpVision.Windows;

using Terminal.Capabilities;
using Terminal.Rendering;

using MultiplexerRoute = Terminal.Multiplexing.Route;
using TerminalCellMetrics = Metrics;
using TerminalDiagnostic = Diagnostic;
using TerminalDiagnosticCode = DiagnosticCode;
using TerminalFocus = Terminal.Input.Focus;
using TerminalOptions = Terminal.Runtime.Options;
using TerminalResponse = Response;
using TerminalSequence = ProtocolSequence;
using TerminalSequenceKind = SequenceKind;
using TerminalText = Terminal.Input.Text;
using UnicodePolicy = Policy;

/// <summary>Owns the dispatcher-affine UI tree and asynchronous terminal runtime.</summary>
[DebuggerDisplay("Application {_screen?.GetType().Name,nq}")]
[PublicAPI]
public sealed class Application: ISink, IAsyncDisposable
{
    private const int _inputCapacity = 4096;
    private readonly Lock _gate = new();
    private readonly Queue<Record> _input = new();
    private readonly ITransport _transport;
    private readonly TerminalOptions _options;
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
    private KeyEventArgs? _routedKeyArgs;
    private Control? _routedKeyTarget;
    private Rune? _suppressedAccessKeyText;
    private string _clipboardText = string.Empty;
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
    private bool _rendering;
    private bool _renderRequested;
    private bool _layoutSinceLastRender;
    private bool _rendererInvalidationPending;
    private bool _startedRaised;
    private bool _raisingStopping;
    private bool _forcedWhileRaising;
    private volatile bool _stopping;
    private volatile bool _stopped;
    private int _startState;
    private int _disposeState;
    private int _hostLeaseDisposed;

    private Theme _theme = Themes.Dark;

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
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="root"/> is attached or already owned.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="root"/> is disposed.</exception>
    /// <exception cref="NotSupportedException">
    /// The terminal profile is not suitable for full-screen use.
    /// </exception>
    public Application(
        Control root,
        ITransport transport,
        IResizeSource resize,
        TerminalOptions? options = null,
        IAsyncDisposable? hostLease = null,
        TimeProvider? timeProvider = null)
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
        _hostLease = hostLease;
        _timeProvider = timeProvider ?? TimeProvider.System;
        TerminalProfile = _options.Profile;
        Capabilities = TerminalProfile.Capabilities;
        CellPolicy = new UnicodePolicy(Capabilities.AmbiguousWidth);
        Dispatcher = Dispatcher.Start(name: "SharpVision.UI", timeProvider: _timeProvider);
        Pointer = new PointerDevice(() => CaptureValue);
        Terminal = new TerminalServices(this);
        Session = new Session(
            transport,
            resize,
            this,
            _options,
            _timeProvider);
        _initializeModalKey = InitializeModalKey;
        Dispatcher.Idle += OnIdle;
        Dispatcher.UnhandledException += OnDispatcherUnhandled;
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
    public Control Root { get; }

    /// <summary>Gets or sets the application-wide theme published to attached controls.</summary>
    /// <exception cref="ArgumentNullException">The assigned theme is null.</exception>
    /// <exception cref="InvalidOperationException">The application is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The application is disposed.</exception>
    public Theme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_theme, value))
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Post(() => ApplyThemeChange(value));
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
    public ModalScope EnterModal(
        Control root,
        OutsideInteraction outsideInteraction = OutsideInteraction.Ignore,
        Control? initialFocus = null) =>
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

    /// <summary>Gets the immutable Unicode cell policy used by the active tree and frame.</summary>
    public UnicodePolicy CellPolicy { get; private set; }

    /// <summary>Gets the first primary runtime failure.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Gets the first secondary terminal or renderer cleanup failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets completion after stopped callbacks finish.</summary>
    public Task Completion => _completion.Task;

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

        try
        {
            await Dispatcher.InvokeAsync(
                () => Starting?.Invoke(this, EventArgs.Empty),
                cancellationToken);
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
            // unobserved by the caller (see #133).
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

            try
            {
                await request.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The queued request still runs. Observe it so a lifecycle-handler failure
                // cannot resurface later as an unobserved task exception.
                Observe(request);

                throw;
            }
        }

        if (_stopping)
        {
            await _completion.Task.WaitAsync(cancellationToken);
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
            // state before this cancellation landed (see #133); nothing further to await here.
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
            catch when (Failure is not null)
            {
            }
        }

        await Dispatcher.DisposeAsync();
        _lifetime.Dispose();
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
    public void Response(in PaletteResponse value) => Enqueue(Record.From(value));

    /// <inheritdoc/>
    public void Response(in MetricsResponse value) => Enqueue(Record.From(value));

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

        Dispatcher.Post(DrainProfile);
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

        Dispatcher.Post(DrainResize);
    }

    /// <inheritdoc/>
    public void Closed() => Enqueue(Record.Closed());

    /// <inheritdoc/>
    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Enqueue(Record.Fault(exception));
    }

    private void BeginStopping(bool forced, Exception? exception)
    {
        Dispatcher.VerifyAccess();

        if (_stopping)
        {
            Failure ??= exception;
            return;
        }

        if (_raisingStopping)
        {
            // A Stopping handler requested shutdown again. Dispatcher invocation runs inline on
            // the dispatcher thread, so without this guard the nested request would raise the
            // same cancellable event again and recurse until the stack is exhausted. One stop
            // request raises Stopping exactly once; a nested forced request must still override
            // a handler that cancelled this one.
            _forcedWhileRaising |= forced;
            Failure ??= exception;

            return;
        }

        var eventArgs = new StoppingEventArgs();
        _raisingStopping = true;

        try
        {
            Stopping?.Invoke(this, eventArgs);
        }
        finally
        {
            _raisingStopping = false;
        }

        if (eventArgs.Cancel && !forced && !_forcedWhileRaising)
        {
            return;
        }

        _forcedWhileRaising = false;
        _stopping = true;
        Failure ??= exception;
        _lifetime.Cancel();
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
        Metrics? metrics,
        Exception? exception)
    {
        Dispatcher.VerifyAccess();

        try
        {
            frame.Dispose();
            _rendering = false;

            if (exception is not null)
            {
                _renderer?.Invalidate();
                _rendererInvalidationPending = false;

                if (exception is not OperationCanceledException || !_lifetime.IsCancellationRequested)
                {
                    Report(exception);
                }

                return;
            }

            FrameRendered?.Invoke(this, new FrameRenderedEventArgs(metrics!.Value));
            MarkStarted();

            if (!_stopping && HasPendingOutOfBand())
            {
                FlushOutOfBand();
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

    private void Dispatch(Record record)
    {
        if (record.Kind != RecordKind.Text)
        {
            _suppressedAccessKeyText = null;
        }

        switch (record.Kind)
        {
            case RecordKind.Key:
                {
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
                        _ = Focus.MoveNext(result.Anchor, result.Command == PostRouteCommand.TabPrevious);
                    }

                    if (!result.Handled && _accessKeys?.Process(record.Stroke) == true)
                    {
                        _suppressedAccessKeyText = record.Stroke.Character;
                    }
                }

                break;
            case RecordKind.Text:
                if (_suppressedAccessKeyText is { } suppressed)
                {
                    _suppressedAccessKeyText = null;

                    if (record.Text.Value == suppressed)
                    {
                        break;
                    }
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
            case RecordKind.Closed:
                BeginStopping(forced: true, exception: null);
                break;
            case RecordKind.Fault:
                Report(record.Exception!);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void OnClipboardShortcut(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;
        var target = ReferenceEquals(eventArgs, _routedKeyArgs)
            ? _routedKeyTarget
            : ReferenceEquals(Focus.Focused, eventArgs.OriginalSource)
                ? eventArgs.OriginalSource
                : null;

        if (eventArgs.Phase != Phase.Preview ||
            eventArgs.Handled ||
            target is null ||
            !TryProcessClipboardShortcut(target, eventArgs.Stroke))
        {
            return;
        }

        eventArgs.Handled = true;
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
            eventArgs.Handled = true;
        }
    }

    private bool TryProcessClipboardShortcut(Control keyTarget, Stroke stroke)
    {
        if (stroke.Action != KeyAction.Press ||
            stroke.Code != Code.Character ||
            stroke.Character is not { } character ||
            (stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) != Modifiers.Control ||
            keyTarget is not TextInput input)
        {
            return false;
        }

        var command = Rune.ToLowerInvariant(character);

        if (command == new Rune('c'))
        {
            PublishClipboard(input.CopySelection());
        }
        else if (command == new Rune('x'))
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

    private void OnContextMenuPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview ||
            eventArgs.Handled ||
            eventArgs.Pointer.Action != PointerAction.Press ||
            !eventArgs.Pointer.Buttons.HasFlag(Buttons.Secondary))
        {
            return;
        }

        for (var target = eventArgs.OriginalSource; target is not null; target = target.Parent)
        {
            if (target is TextInput { ContextMenu: TextInputContextMenu contextMenu })
            {
                contextMenu.ClipboardWriter = PublishClipboard;
                contextMenu.ClipboardReader = () => _clipboardText;
                return;
            }
        }
    }

    private void PublishClipboard(string value)
    {
        Debug.Assert(value is not null, "TextInput clipboard operations return owned non-null text.");

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

        while (true)
        {
            Record record;

            lock (_gate)
            {
                if (!_input.TryDequeue(out record))
                {
                    _inputWake = false;
                    break;
                }
            }

            if (!_stopping)
            {
                Dispatch(record);
            }
        }

        ProcessInvalidation();
    }

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

        if (!_stopping && value is not null)
        {
            ApplyCapabilities(value);
        }
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
                    ModalityValue = new ModalityManager(Root, FocusValue, CaptureValue);
                    _accessKeys = new AccessKeyManager(Root, FocusValue, ModalityValue);
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
        _engine.Layout(Root, Size);
        _layoutSinceLastRender = true;
        Resize?.Invoke(this, new ResizeEventArgs(value));

        if (value.IsSuspended)
        {
            MarkStarted();
            return;
        }

        ProcessInvalidation();
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

        Dispatcher.Post(DrainInput);
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
            // transaction has committed or failed.
            _rendererInvalidationPending = true;
        }

        if (_initialized)
        {
            Root.SetCapabilities(Capabilities);
            Root.SetCellPolicy(CellPolicy);
        }

        CapabilitiesChanged?.Invoke(
            this,
            new CapabilitiesChangedEventArgs(previous, Capabilities));

        if (!_initialized)
        {
            return;
        }

        Root.Invalidate(measure ? Invalidation.Measure : Invalidation.Render);
        ProcessInvalidation();
    }

    private async Task FinishWithoutSessionAsync()
    {
        var cleanupFailure = await DisposeTerminalResourcesAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            Failure ??= cleanupFailure;
            LastCleanupException ??= cleanupFailure;
            FinalizeStopped();
        });
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

        var priorFailure = Failure;
        ExceptionDispatchInfo? cleanupFailure = null;

        if (_initialized)
        {
            CaptureCleanup(() => _clipboardShortcutRegistration?.Dispose(), ref cleanupFailure);
            _clipboardShortcutRegistration = null;
            _clipboardText = string.Empty;
            _accessKeys = null;
            _suppressedAccessKeyText = null;
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
        System.Action action,
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

    private void MarkStarted()
    {
        if (_startedRaised)
        {
            return;
        }

        _startedRaised = true;
        Started?.Invoke(this, EventArgs.Empty);
        _ = _started.TrySetResult();
    }

    private void OnDispatcherUnhandled(object? sender, UnhandledEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        Report(eventArgs.Exception);
    }

    private void OnIdle(object? sender, EventArgs eventArgs)
    {
        if (!_startedRaised || _stopping)
        {
            return;
        }

        if (!_rendering && !IsSuspended() && Root.Pending != Invalidation.None)
        {
            ProcessInvalidation();
            return;
        }

        Idle?.Invoke(this, EventArgs.Empty);
    }

    private async Task ObserveRenderAsync(
        ValueTask<Metrics> operation,
        Frame frame,
        IDisposable hold,
        TaskCompletionSource completion)
    {
        Metrics? metrics = null;
        Exception? failure = null;

        try
        {
            metrics = await operation;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            Dispatcher.Post(() => CompleteRender(frame, hold, completion, metrics, failure));
        }
        catch
        {
            frame.Dispose();
            hold.Dispose();
            _ = completion.TrySetResult();
        }
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

        await Dispatcher.InvokeAsync(() => BeginStopping(forced: true, failure));
        do
        {
            await _renderTask;
        } while (_rendering);

        var cleanupFailure = await DisposeTerminalResourcesAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            Failure ??= cleanupFailure;
            LastCleanupException ??= cleanupFailure;
            FinalizeStopped();
        });
    }

    private void ProcessInvalidation()
    {
        if (!_initialized || _stopping || IsSuspended())
        {
            return;
        }

        if ((Root.Pending & (Invalidation.Measure | Invalidation.Arrange)) != 0)
        {
            _engine.Layout(Root, Size);
            _layoutSinceLastRender = true;
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
        UnhandledException?.Invoke(this, eventArgs);

        if (!eventArgs.Handled)
        {
            BeginStopping(forced: true, exception);
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

        Dispatcher.Post(DrainOutOfBand);
    }

    private void DrainOutOfBand()
    {
        Dispatcher.VerifyAccess();

        lock (_gate)
        {
            _outOfBandWake = false;
        }

        // A frame render owns the writer; CompleteRender re-drains afterward.
        if (_rendering || _stopping || IsSuspended())
        {
            return;
        }

        FlushOutOfBand();
    }

    private void FlushOutOfBand()
    {
        Dispatcher.VerifyAccess();
        Debug.Assert(!_rendering, "Out-of-band flush must not overlap a frame render.");

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
        _rendering = true;
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
            await operation;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            Dispatcher.Post(() => CompleteOutOfBand(hold, completion, failure));
        }
        catch
        {
            hold.Dispose();
            _ = completion.TrySetResult();
        }
    }

    private void CompleteOutOfBand(IDisposable hold, TaskCompletionSource completion, Exception? failure)
    {
        Dispatcher.VerifyAccess();

        try
        {
            _rendering = false;

            if (failure is not null &&
                (failure is not OperationCanceledException || !_lifetime.IsCancellationRequested))
            {
                Report(failure);
                return;
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
        if (_stopping || IsSuspended())
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

        if (_rendering)
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
        // OnRenderContent. After layout, cell positions may have changed, so the optimization is
        // disabled to avoid copying stale cells from the previous frame.
        if (skipCleanSubtrees && !_layoutSinceLastRender)
        {
            _ = renderer.AttachCommittedFrame(frame);
        }

        _layoutSinceLastRender = false;

        try
        {
            Root.Render(frame.Canvas);
        }
        catch
        {
            frame.Dispose();
            throw;
        }

        var hold = Dispatcher.Hold();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        _rendering = true;
        var operation = renderer.RenderAsync(
            frame,
            _transport,
            TerminalProfile,
            _cellMetrics,
            _lifetime.Token);
        _ = ObserveRenderAsync(operation, frame, hold, completion);
    }

    private Renderer CreateRenderer()
    {
        var policy = _options.Negotiation?.Multiplexing;
        var route = policy is { Layers.Count: > 0 }
            ? new MultiplexerRoute(policy)
            : null;
        return new Renderer(
            Capabilities,
            route,
            cleanupTimeout: _options.CleanupTimeout,
            timeProvider: _timeProvider);
    }

    private async Task<Exception?> DisposeTerminalResourcesAsync()
    {
        var lifetimeDiagnostic = _renderer?.LastCleanupException ?? Session.LastCleanupException;
        Exception? failure = null;

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
                failure = exception;
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
            Dispatcher.Post(DrainInput);
        }
    }

    private bool IsSuspended() => Size.Width == 0 || Size.Height == 0;
}
