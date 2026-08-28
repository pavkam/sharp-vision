// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
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
    private long _selectedResultVersion;
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
    /// open and publishes this event instead, so its buttons and unmodified Escape never consume
    /// input without an outcome.
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

    #region Composition

    /// <summary>Creates the shared dialog action bar with a separator directly above its actions.</summary>
    /// <param name="actions">The detached retained layout containing the dialog action Buttons.</param>
    /// <param name="buttons">The non-null Buttons whose resolved shadows determine bottom clearance.</param>
    /// <param name="separator">The retained divider, exposed so a caller can bind its own forwarded
    /// <see cref="SeparatorStyle"/> onto it.</param>
    /// <returns>A full-width action bar containing the separator and shadow-aware action host.</returns>
    /// <exception cref="ArgumentNullException">The action layout, array, or one Button is null.</exception>
    private protected static Grid CreateActionBar(ControlBase actions, Button[] buttons, out Separator separator)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(buttons);

        foreach (var button in buttons)
        {
            ArgumentNullException.ThrowIfNull(button);
        }

        separator = new Separator();
        var actionHost = new DialogActionHost(actions, buttons);
        var actionBar = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        actionBar.Columns.Add(Track.Star(1, minimum: 1));
        actionBar.Rows.Add(Track.Auto(minimum: 1));
        actionBar.Rows.Add(Track.Auto());
        Grid.SetRow(actionHost, 1);
        actionBar.Children.Add(separator);
        actionBar.Children.Add(actionHost);
        return actionBar;
    }

    #endregion

    #region Presentation

    /// <summary>Resolves an explicit Overlay, owning Screen, or outermost fallback Overlay.</summary>
    /// <param name="owner">The control whose retained ancestry is searched.</param>
    /// <returns>The resolved presentation host, or null when no supported host exists.</returns>
    private protected static PresentationHost? FindHost(ControlBase owner) => PresentationHost.Resolve(owner);

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
        ControlBase? initialFocus,
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

            // An older scope torn down from underneath this one unwinds this scope too
            // (ModalityManager.Exit unwinds every younger scope in reverse order). Without this,
            // the dialog stays attached, painted, and no longer modal, with nothing ever settling
            // its completion.
            _scope.Exited += OnScopeExited;

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

    /// <summary>Resolves an owner's presentation host, attaches this dialog to it, and presents it.</summary>
    /// <remarks>
    /// This is the reachable entry point for a dialog type defined outside this assembly — the
    /// presentation host itself is an internal implementation detail, so this overload takes the
    /// owning <see cref="ControlBase"/> directly instead. A dialog subclass typically calls this from
    /// its own static asynchronous factory after constructing itself. Built-in dialogs use this same
    /// transaction so validation, attachment, rollback, and disposal cannot drift by family.
    /// </remarks>
    /// <param name="owner">The non-null, attached, undisposed control whose presentation host will
    /// own this dialog.</param>
    /// <param name="initialFocus">An optional eligible initial focus target owned by this dialog.</param>
    /// <param name="cancellationToken">Cancels the pending result and tears down presentation.</param>
    /// <returns>The one-shot task representing this presentation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException">The owner is detached or has no presentation host.</exception>
    /// <exception cref="InvalidOperationException">
    /// The call is made off the owner's dispatcher, the dialog is detached, presentation is
    /// repeated, or modal entry fails.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or this dialog is disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    protected Task<TResult> PresentAsync(ControlBase owner, ControlBase? initialFocus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = owner.Dispatcher ??
            throw new ArgumentException("The dialog owner must be attached.", nameof(owner));
        dispatcher.VerifyAccess();
        var host = FindHost(owner) ??
            throw new ArgumentException("The dialog owner must have a presentation host.", nameof(owner));

        // A repeated call on an already-presented dialog must reach the private-protected core's own
        // "presented only once" guard directly, without re-attaching (host.Add would reject an
        // already-owned control) or rolling back the first, still-pending presentation on failure.
        if (host.Owns(this))
        {
            return PresentAsync(host, initialFocus, cancellationToken);
        }

        host.Add(this);

        try
        {
            return PresentAsync(host, initialFocus, cancellationToken);
        }
        catch
        {
            _ = host.Remove(this);
            Dispose();
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
    /// <remarks>
    /// Repeated cancellation after completion is ignored. Delegates unconditionally to
    /// <see cref="Complete"/> so its own fallback for a directly mounted, never-presented
    /// (modeless) dialog decides the outcome the same way an accepted result does, rather than
    /// pre-empting that fallback with a narrower check of its own.
    /// </remarks>
    protected bool Cancel() => Complete(_cancelledResult);

    private void FinishCompletion()
    {
        ExceptionDispatchInfo? failure = null;
        var vetoed = false;

        _isFinishingCompletion = true;

        try
        {
            if (IsSurfacePresented && !IsDisposed)
            {
                var closed = true;
                ExceptionAggregation.Capture(() => closed = ClosePresentedDialog(), ref failure);

                // A handler vetoing CloseRequested reports failure through the returned bool, not
                // an exception, so only a clean (non-exceptional) false counts as a veto. An actual
                // exception from the close attempt still falls through to the unconditional cleanup
                // below, preserving the existing exception-aggregation behavior for that case.
                vetoed = failure is null && !closed;
            }

            if (vetoed)
            {
                RollbackPendingCompletion();
            }
            else
            {
                CleanupPresentation(dispose: true, ref failure);
            }
        }
        finally
        {
            _isFinishingCompletion = false;
        }

        if (!vetoed)
        {
            SettleCompletion();
        }

        failure?.Throw();
    }

    private bool ClosePresentedDialog() =>
        _closingRequestObserved
            ? CloseSurfaceAfterClosingRequest(static () => { }, RemoveFromHost)
            : CloseSurface(static () => { }, RemoveFromHost);

    /// <summary>Undoes a latched <see cref="TryBeginResult"/>/<see cref="TryBeginCancellation"/> commit
    /// after the close attempt it was staged for turned out to be vetoed by a <c>CloseRequested</c>
    /// handler, so a later <see cref="Complete"/>/cancellation attempt can be retried cleanly.</summary>
    private void RollbackPendingCompletion()
    {
        lock (_completionGate)
        {
            _completed = false;
            _hasPendingResult = false;
            _pendingResult = default;
            _completesWithCancellation = false;
            _pendingCancellationToken = default;

            // The scheduling cycle that led to this now-vetoed FinishCompletion call has already
            // marked itself invoked (RunScheduledCompletion sets this before calling FinishCompletion),
            // and never resets on its own since only one cycle is ever expected to run. A retried
            // Complete/cancellation needs a fresh cycle to actually reach FinishCompletion again
            // rather than have ScheduleCompletion silently decline to re-post.
            _scheduledInvoked = false;
        }
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
        var version = ++_selectedResultVersion;
        SelectedResult = result;
        HasSelectedResult = true;
        NotifyPropertyChanged(nameof(SelectedResult), InvalidationImpact.None);

        if (version != _selectedResultVersion)
        {
            return;
        }

        NotifyPropertyChanged(nameof(HasSelectedResult), InvalidationImpact.None);

        if (version != _selectedResultVersion)
        {
            return;
        }

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

        scope?.Exited -= OnScopeExited;

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
                IsInitialKeyDown: true,
                Stroke.Code: Code.Escape,
                Stroke.Modifiers: var modifiers
            } && IsModeless())
        {
            if (modifiers.IsActivationEligible())
            {
                eventArgs.IsHandled = Cancel();
            }

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
    internal override void ValidateAttachment()
    {
        base.ValidateAttachment();

        lock (_completionGate)
        {
            if (_completed && _completion is not null && !_isFinishingCompletion)
            {
                throw new InvalidOperationException(
                    "A dialog cannot be reattached while presentation completion is pending.");
            }
        }
    }

    private void OnScopeExited(object? sender, EventArgs eventArgs)
    {
        var dispatcher = Dispatcher;

        // FinishCompletion already begins its result before CleanupPresentation disposes the
        // scope, so _completed is already true - and TryBeginResult a no-op - for every normal
        // completion path; this only fires for real when an older scope unwound this one out from
        // under a still-running dialog. Deferred via ScheduleCompletion because Exited is
        // published mid-transaction from inside ModalityManager's unwind pump: settling inline
        // would re-enter CleanupPresentation -> scope.Dispose() -> Exit while the manager is still
        // walking its scope stack.
        if (!_isFinishingCompletion && dispatcher is not null && TryBeginResult(_cancelledResult))
        {
            _ = ScheduleCompletion(dispatcher);
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
