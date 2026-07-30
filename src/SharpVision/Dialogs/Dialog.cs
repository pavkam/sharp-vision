// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;
using SharpVision.Windows;

/// <summary>Provides one-shot typed completion for a directly presented modal Window.</summary>
/// <typeparam name="TResult">The semantic result produced when the dialog completes.</typeparam>
/// <remarks>
/// The dialog object is the retained, presented, modal, and disposed surface identity. Presentation
/// owns a temporary host edge and one modal scope until completion, cancellation, or disposal.
/// </remarks>
[PublicAPI]
public abstract class Dialog<TResult>: Window
{
    private readonly TResult _cancelledResult;
    private readonly Lock _completionGate = new();

    private TaskCompletionSource<TResult>? _completion;
    private PresentationHost? _host;
    private ModalScope? _scope;
    private CancellationTokenRegistration _externalCancellation;
    private bool _completed;
    private bool _completesWithCancellation;
    private bool _hasPendingResult;
    private bool _isFinishingCompletion;
    private bool _closingRequestObserved;
    private bool _scheduledInvoked;
    private CancellationToken _pendingCancellationToken;
    private TResult? _pendingResult;
    private Dispatcher? _scheduledDispatcher;
    private EventHandler? _scheduledIdle;

    /// <summary>Initializes a dialog with the semantic result used for dismissal.</summary>
    /// <param name="cancelledResult">The result produced by a close request or explicit disposal.</param>
    protected Dialog(TResult cancelledResult)
    {
        _cancelledResult = cancelledResult;
        CanMove = false;
        CanClose = true;
        CloseOnEscape = true;
        HeaderPlacement = WindowTitlePlacement.Center;
        Closing += OnClosing;
    }

    /// <summary>Raised when a semantic action selects a typed result.</summary>
    /// <remarks>
    /// Modal callers normally observe the returned task. A directly mounted modeless dialog remains
    /// open and publishes this event instead, so its buttons never consume input without an outcome.
    /// External cancellation, forced detachment, and disposal do not represent result selection.
    /// </remarks>
    public event EventHandler? ResultSelected;

    /// <summary>Gets whether a semantic action has selected at least one result on this dialog.</summary>
    public bool HasSelectedResult { get; private set; }

    /// <summary>Gets the most recently selected semantic result, or the default value before selection.</summary>
    public TResult? SelectedResult { get; private set; }

    /// <summary>Gets whether this dialog currently owns deferred dispatcher completion work.</summary>
    internal bool HasPendingCompletionSchedule
    {
        get
        {
            lock (_completionGate)
            {
                return _scheduledDispatcher is not null || _scheduledIdle is not null;
            }
        }
    }

    #region Presentation

    /// <summary>Resolves an explicit Overlay, owning Screen, or outermost fallback Overlay.</summary>
    /// <param name="owner">The control whose retained ancestry is searched.</param>
    /// <returns>The resolved presentation host, or null when no supported host exists.</returns>
    private protected static PresentationHost? FindHost(Control owner) => PresentationHost.Resolve(owner);

    /// <summary>Presents this dialog through the supplied host and enters application modality.</summary>
    /// <param name="host">The host that already directly owns this dialog.</param>
    /// <param name="initialFocus">An optional eligible initial focus target owned by this dialog.</param>
    /// <param name="cancellationToken">Cancels the pending result and tears down presentation.</param>
    /// <returns>The one-shot task representing this presentation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentException">The host does not directly own this dialog.</exception>
    /// <exception cref="InvalidOperationException">
    /// The dialog is detached, presentation is repeated, or modal entry fails.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The dialog or modality owner is disposed.</exception>
    private protected Task<TResult> PresentAsync(
        PresentationHost host,
        Control? initialFocus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_completion is not null)
        {
            throw new InvalidOperationException("A dialog can be presented only once.");
        }

        if (!host.Owns(this) || Dispatcher is null)
        {
            throw new ArgumentException("The presentation host must directly own the attached dialog.", nameof(host));
        }

        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        _host = host;

        try
        {
            _scope = ShowModal(OutsideInteraction.Ignore, initialFocus);

            if (!_scope.IsActive)
            {
                throw new InvalidOperationException("A presented dialog requires one active modal scope.");
            }

            if (cancellationToken.CanBeCanceled)
            {
                var dispatcher = Dispatcher;
                Debug.Assert(dispatcher is not null, "A presented dialog retains its dispatcher.");
                _externalCancellation = cancellationToken.Register(() =>
                {
                    if (TryBeginCancellation(cancellationToken))
                    {
                        _ = ScheduleCompletion(dispatcher);
                    }
                });
            }

            return completion.Task;
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);
            _ = TryBeginResult(_cancelledResult);
            CleanupPresentation(dispose: true, ref failure);
            SettleCompletion();
            failure!.Throw();
            throw;
        }
    }

    /// <summary>Completes this dialog once with the supplied semantic result.</summary>
    /// <param name="result">The result returned to the presentation caller.</param>
    /// <returns>True when this call committed the one-shot result; otherwise false.</returns>
    /// <remarks>Repeated completion attempts after the first committed result are ignored.</remarks>
    protected bool Complete(TResult result)
    {
        if (!TryBeginResult(result))
        {
            if (!CanSelectModelessResult())
            {
                return false;
            }

            PublishSelectedResult(result);
            return true;
        }

        var dispatcher = Dispatcher;

        if (dispatcher is not null)
        {
            _ = ScheduleCompletion(dispatcher);
            return true;
        }

        FinishCompletion();
        return true;
    }

    /// <summary>Completes this dialog with the cancellation result supplied at construction.</summary>
    /// <returns>True when this call committed cancellation; otherwise false.</returns>
    /// <remarks>Repeated cancellation after completion is ignored.</remarks>
    protected bool Cancel()
    {
        lock (_completionGate)
        {
            if (_completion is null)
            {
                return false;
            }
        }

        return Complete(_cancelledResult);
    }

    private void FinishCompletion()
    {
        ExceptionDispatchInfo? failure = null;

        _isFinishingCompletion = true;

        try
        {
            if (IsSurfacePresented && !IsDisposed)
            {
                ExceptionAggregation.Capture(ClosePresentedDialog, ref failure);
            }

            CleanupPresentation(dispose: true, ref failure);
        }
        finally
        {
            _isFinishingCompletion = false;
        }

        SettleCompletion();
        failure?.Throw();
    }

    private void ClosePresentedDialog()
    {
        if (_closingRequestObserved)
        {
            _ = CloseSurfaceAfterClosingRequest(static () => { }, RemoveFromHost);
            return;
        }

        _ = CloseSurface(static () => { }, RemoveFromHost);
    }

    private bool TryBeginResult(TResult result)
    {
        lock (_completionGate)
        {
            if (_completed || _completion is null)
            {
                return false;
            }

            _completed = true;
            _hasPendingResult = true;
            _pendingResult = result;
            return true;
        }
    }

    private bool TryBeginCancellation(CancellationToken cancellationToken)
    {
        lock (_completionGate)
        {
            if (_completed || _completion is null)
            {
                return false;
            }

            _completed = true;
            _completesWithCancellation = true;
            _pendingCancellationToken = cancellationToken;
            return true;
        }
    }

    private bool CanSelectModelessResult()
    {
        lock (_completionGate)
        {
            return _completion is null && !_completed && !IsDisposed;
        }
    }

    private void PublishSelectedResult(TResult result)
    {
        SelectedResult = result;
        HasSelectedResult = true;
        ResultSelected?.Invoke(this, EventArgs.Empty);
    }

    private void SettleCompletion()
    {
        TaskCompletionSource<TResult>? completion;
        bool cancels;
        CancellationToken cancellationToken;
        TResult result;

        lock (_completionGate)
        {
            completion = _completion;

            if (completion is null || completion.Task.IsCompleted || !_completed)
            {
                return;
            }

            cancels = _completesWithCancellation;
            cancellationToken = _pendingCancellationToken;
            result = _pendingResult!;
        }

        if (cancels)
        {
            _ = completion.TrySetCanceled(cancellationToken);
        }
        else
        {
            Debug.Assert(_hasPendingResult, "A semantic dialog completion retains one pending result.");
            _ = completion.TrySetResult(result);
        }
    }

    private void CleanupPresentation(bool dispose, ref ExceptionDispatchInfo? failure)
    {
        ExceptionAggregation.Capture(_externalCancellation.Dispose, ref failure);
        _externalCancellation = default;

        var scope = _scope;
        _scope = null;

        if (scope is { IsActive: true })
        {
            ExceptionAggregation.Capture(scope.Dispose, ref failure);
        }

        ExceptionAggregation.Capture(RemoveFromHost, ref failure);

        if (dispose && !IsDisposed)
        {
            ExceptionAggregation.Capture(Dispose, ref failure);
        }
    }

    private void RemoveFromHost()
    {
        var host = _host;
        _host = null;

        if (host is not null && host.Owns(this))
        {
            _ = host.Remove(this);
        }
    }

    private void OnClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _closingRequestObserved = true;
        _ = Complete(_cancelledResult);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is KeyEventArgs
            {
                Stroke:
                {
                    Action: KeyAction.Press,
                    Code: Code.Escape
                }
            } && IsModeless())
        {
            return;
        }

        base.OnEvent(eventArgs);
    }

    private bool IsModeless()
    {
        lock (_completionGate)
        {
            return _completion is null;
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        var dispatcher = Dispatcher;

        if (reason == ReleaseReason.Detached &&
            !_isFinishingCompletion &&
            dispatcher is not null &&
            TryBeginResult(_cancelledResult))
        {
            _ = ScheduleCompletion(dispatcher);
        }

        base.OnUnavailable(reason);
    }

    #endregion

    #region Disposal

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        ExceptionDispatchInfo? failure = null;

        if (!_isFinishingCompletion)
        {
            _ = TryBeginResult(_cancelledResult);
            ExceptionAggregation.Capture(_externalCancellation.Dispose, ref failure);
            _externalCancellation = default;
        }

        ExceptionAggregation.Capture(base.OnDisposing, ref failure);
        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnDisposed()
    {
        CancelScheduledCompletion();
        base.OnDisposed();
        _host = null;
        _scope = null;
        SettleCompletion();
        ResultSelected = null;
    }

    #endregion

    #region Dispatcher scheduling

    private bool ScheduleCompletion(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        EventHandler idle = OnScheduledIdle;

        lock (_completionGate)
        {
            if (_scheduledInvoked)
            {
                return false;
            }

            if (_scheduledDispatcher is not null)
            {
                return true;
            }

            _scheduledDispatcher = dispatcher;
            _scheduledIdle = idle;
            dispatcher.Idle += idle;
        }

        try
        {
            dispatcher.Post(RunScheduledCompletion);
            return true;
        }
        catch (ObjectDisposedException)
        {
            CancelScheduledCompletion();
            return false;
        }
        catch (InvalidOperationException)
        {
            // A full bounded queue will eventually reach Idle on this dispatcher;
            // the one-shot handler performs cleanup without a retry loop.
            return true;
        }
    }

    private void OnScheduledIdle(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RunScheduledCompletion();
    }

    private void RunScheduledCompletion()
    {
        Dispatcher? dispatcher;
        EventHandler? idle;

        lock (_completionGate)
        {
            if (_scheduledInvoked)
            {
                return;
            }

            _scheduledInvoked = true;
            dispatcher = _scheduledDispatcher;
            idle = _scheduledIdle;
            _scheduledDispatcher = null;
            _scheduledIdle = null;
        }

        if (dispatcher is not null && idle is not null)
        {
            dispatcher.Idle -= idle;
        }

        FinishCompletion();
    }

    // Runs when the owning dispatcher was already stopping by the time Post itself
    // rejected the scheduled completion. Unlike RunScheduledCompletion's FinishCompletion
    // call, this settles the result directly instead of also attempting the dispatcher-
    // dependent close/cleanup FinishCompletion performs, which would fail the same way.
    private void CancelScheduledCompletion()
    {
        Dispatcher? dispatcher;
        EventHandler? idle;

        lock (_completionGate)
        {
            _scheduledInvoked = true;
            dispatcher = _scheduledDispatcher;
            idle = _scheduledIdle;
            _scheduledDispatcher = null;
            _scheduledIdle = null;
        }

        if (dispatcher is not null && idle is not null)
        {
            dispatcher.Idle -= idle;
        }

        SettleCompletion();
    }

    #endregion
}
