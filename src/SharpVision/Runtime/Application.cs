// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.Buffers;

using SharpVision.Styling;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;
using SharpVision.Threading;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;
using TerminalDiagnostic = Terminal.Protocols.Diagnostic;
using TerminalDiagnosticCode = Terminal.Protocols.DiagnosticCode;
using TerminalFocus = Terminal.Input.Focus;
using TerminalOptions = Terminal.Runtime.Options;
using TerminalResponse = Terminal.Protocols.Response;
using TerminalSequence = Terminal.Protocols.ProtocolSequence;
using TerminalSequenceKind = Terminal.Protocols.SequenceKind;
using TerminalText = Terminal.Input.Text;
using UnicodePolicy = Terminal.Unicode.Policy;

/// <summary>Owns the dispatcher-affine UI tree and asynchronous terminal runtime.</summary>
public sealed partial class Application: ISink, IAsyncDisposable
{
    private const int _inputCapacity = 4096;
    private readonly Lock _gate = new();
    private readonly Queue<Record> _input = new();
    private readonly ITransport _transport;
    private readonly TerminalOptions _options;
    private readonly IAsyncDisposable? _hostLease;
    private readonly Session _session;
    private readonly Renderer _renderer = new();
    private readonly Engine _engine = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ArrayBufferWriter<byte> _outOfBand = new();
    private Dimensions _latestResize;
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
    private bool _startedRaised;
    private volatile bool _stopping;
    private volatile bool _stopped;
    private int _startState;
    private int _disposeState;
    private int _hostLeaseDisposed;

    private Theme _theme = Themes.Dark;
    private ThemeContext? _themeContext;

    private FocusManager? FocusValue { get; set; }

    private CaptureManager? CaptureValue { get; set; }

    /// <summary>Initializes an application that owns all supplied terminal resources.</summary>
    /// <param name="root">The non-null detached root control.</param>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null terminal resize source.</param>
    /// <param name="options">Validated terminal options, or null for defaults.</param>
    /// <param name="hostLease">An optional host resource disposed last after cleanup, or null.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="root"/> is already attached.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="root"/> is disposed.</exception>
    public Application(
        Control root,
        ITransport transport,
        IResizeSource resize,
        TerminalOptions? options = null,
        IAsyncDisposable? hostLease = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ObjectDisposedException.ThrowIf(root.IsDisposed, root);

        if (root.Dispatcher is not null)
        {
            throw new ArgumentException("The application root must be detached.", nameof(root));
        }

        // The application owns the terminal viewport. Ordinary descendants
        // remain content-sized unless their parent or caller opts into stretch.
        root.HorizontalAlignment = HorizontalAlignment.Stretch;
        root.VerticalAlignment = VerticalAlignment.Stretch;
        Root = root;
        _transport = transport;
        _options = options ?? new TerminalOptions();
        _hostLease = hostLease;
        Capabilities = _options.Capabilities;
        CellPolicy = new UnicodePolicy(Capabilities.AmbiguousWidth);
        Dispatcher = Dispatcher.Start(name: "SharpVision.UI");
        Pointer = new PointerDevice(() => CaptureValue);
        Terminal = new TerminalServices(this);
        _session = new Session(transport, resize, this, _options);
        SubscribeTheme(_theme);
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

            Dispatcher? dispatcher = Dispatcher;

            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Post(() => ApplyThemeChange(value));
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
        UnsubscribeTheme(_theme);
        _theme = value;
        SubscribeTheme(_theme);
        PublishThemeContext();

        if (!_initialized)
        {
            return;
        }

        Root.Invalidate(Invalidation.Measure);
        ProcessInvalidation();
    }

    /// <summary>Gets focus ownership after the first resize attaches the tree.</summary>
    public FocusManager Focus => FocusValue ??
        throw new InvalidOperationException("Focus is available after the first resize.");

    /// <summary>Gets pointer ownership after the first resize attaches the tree.</summary>
    public CaptureManager Capture => CaptureValue ??
        throw new InvalidOperationException("Capture is available after the first resize.");

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

        _sessionTask = _session.RunAsync(_lifetime.Token).AsTask();
        _ = ObserveSessionAsync();
        Task completed = await Task.WhenAny(_started.Task, _completion.Task)
            .WaitAsync(cancellationToken);
        await completed.WaitAsync(cancellationToken);
    }

    /// <summary>Requests idempotent shutdown and waits for complete cleanup.</summary>
    /// <param name="cancellationToken">Cancels only the caller's wait.</param>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _startState) == 0)
        {
            return;
        }

        if (!_stopping)
        {
            await Dispatcher.InvokeAsync(
                () => BeginStopping(forced: false, exception: null),
                cancellationToken);
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
        await StartAsync(cancellationToken).ConfigureAwait(false);

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
    public void Sequence(TerminalSequence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        long discardedBytes = checked(
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

        StoppingEventArgs eventArgs = new();
        Stopping?.Invoke(this, eventArgs);

        if (eventArgs.Cancel && !forced)
        {
            return;
        }

        _stopping = true;
        Failure ??= exception;
        _lifetime.Cancel();
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
                _renderer.Invalidate();

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
        switch (record.Kind)
        {
            case RecordKind.Key:
                if (Focus.Focused is { } keyTarget)
                {
                    Router.Route(keyTarget, Events.Key, new KeyEventArgs(record.Stroke));
                }

                break;
            case RecordKind.Text:
                if (Focus.Focused is { } textTarget)
                {
                    Router.Route(textTarget, Events.Text, new TextEventArgs(record.Text));
                }

                break;
            case RecordKind.Pointer:
                Pointer.Observe(record.Pointer);
                _ = Capture.Dispatch(record.Pointer);
                break;
            case RecordKind.Paste:
                if (Focus.Focused is { } pasteTarget)
                {
                    Debug.Assert(record.Paste.HasValue, "A paste record must carry its payload.");
                    Router.Route(
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

                Router.Route(
                    Focus.Focused ?? Root,
                    Events.Focus,
                    new FocusEventArgs(record.Focus));
                break;
            case RecordKind.Diagnostic:
                Diagnostic?.Invoke(this, new DiagnosticEventArgs(record.Diagnostic));
                break;
            case RecordKind.Response:
                ResponseReceived?.Invoke(this, new ProtocolResponseEventArgs(record.Response));
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

        if (!_initialized)
        {
            Root.Attach(Dispatcher, CellPolicy);
            PublishThemeContext();
            FocusValue = new FocusManager(Root);
            CaptureValue = new CaptureManager(Root);
            _initialized = true;
            WakeInput();
        }

        Size = value.Cells;
        _engine.Layout(Root, Size);
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

        TerminalCapabilities previous = Capabilities;
        bool measure = previous.AmbiguousWidth != value.AmbiguousWidth;
        Capabilities = value;
        CellPolicy = new UnicodePolicy(value.AmbiguousWidth);

        if (_initialized)
        {
            Root.SetCellPolicy(CellPolicy);
        }

        CapabilitiesChanged?.Invoke(
            this,
            new CapabilitiesChangedEventArgs(previous, value));

        if (!_initialized)
        {
            return;
        }

        Root.Invalidate(measure ? Invalidation.Measure : Invalidation.Render);
        ProcessInvalidation();
    }

    private async Task FinishWithoutSessionAsync()
    {
        await _session.DisposeAsync();
        await DisposeHostLeaseAsync();
        await Dispatcher.InvokeAsync(FinalizeStopped);
    }

    private async ValueTask DisposeHostLeaseAsync()
    {
        if (_hostLease is not null && Interlocked.Exchange(ref _hostLeaseDisposed, 1) == 0)
        {
            await _hostLease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void SubscribeTheme(Theme theme) => theme.Changed += OnThemeChanged;

    private void UnsubscribeTheme(Theme theme) => theme.Changed -= OnThemeChanged;

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs eventArgs)
    {
        Dispatcher? dispatcher = Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => OnThemeChanged(sender, eventArgs));
            return;
        }

        if (_stopping || !ReferenceEquals(sender, _theme))
        {
            return;
        }

        PublishThemeContext();
        Root.Invalidate(eventArgs.Impact == Impact.Measure ? Invalidation.Measure : Invalidation.Render);
        ProcessInvalidation();
    }

    private void PublishThemeContext()
    {
        _themeContext = ThemeContext.Create(_theme);
        ApplyThemeContext();
    }

    private void ApplyThemeContext()
    {
        if (_themeContext is null)
        {
            return;
        }

        Root.PropagateThemeContext(_themeContext);
    }

    private void FinalizeStopped()
    {
        Dispatcher.VerifyAccess();

        if (_stopped)
        {
            return;
        }

        if (_initialized)
        {
            CaptureValue?.Dispose();
            FocusValue?.Dispose();
        }

        UnsubscribeTheme(_theme);
        _renderer.Dispose();
        Root.Dispose();
        LastCleanupException = _session.LastCleanupException ?? _renderer.LastCleanupException;
        _stopped = true;
        Stopped?.Invoke(this, EventArgs.Empty);

        _ = Failure is { } failure
            ? _completion.TrySetException(failure)
            : _completion.TrySetResult();
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
        }
        while (_rendering);

        await _session.DisposeAsync();
        await DisposeHostLeaseAsync();
        await Dispatcher.InvokeAsync(FinalizeStopped);
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
        }

        if ((Root.Pending & Invalidation.Render) != 0)
        {
            StartRender();
        }
    }

    private void Report(Exception exception)
    {
        Failure ??= exception;
        UnhandledEventArgs eventArgs = new(exception);
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

        IDisposable hold = Dispatcher.Hold();
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        _rendering = true;
        ValueTask operation = WriteOutOfBandAsync(payload);
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

    private void StartRender()
    {
        Dispatcher.VerifyAccess();

        if (_rendering)
        {
            _renderRequested = true;
            return;
        }

        Frame frame = new(Size, ambiguousWidth: CellPolicy.AmbiguousWidth);

        try
        {
            Root.Render(frame.Canvas);
        }
        catch
        {
            frame.Dispose();
            throw;
        }

        IDisposable hold = Dispatcher.Hold();
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTask = completion.Task;
        _rendering = true;
        ValueTask<Metrics> operation = _renderer.RenderAsync(
            frame,
            _transport,
            Capabilities,
            _lifetime.Token);
        _ = ObserveRenderAsync(operation, frame, hold, completion);
    }

    private void WakeInput()
    {
        bool post = false;

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
