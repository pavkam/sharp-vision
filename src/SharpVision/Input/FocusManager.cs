// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Owns transactional keyboard focus within one attached control tree.</summary>
[PublicAPI]
public sealed class FocusManager: IDisposable
{
    #region Construction and state

    /// <summary>Initializes focus ownership for one attached root. The caller owns and must
    /// dispose the manager.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is detached or already belongs to a focus manager.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    [MustDisposeResource]
    public FocusManager(ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.VerifyMutable();

        if (root.Dispatcher is null)
        {
            throw new ArgumentException("The focus root must be attached.", nameof(root));
        }

        if (root.FocusOwner is not null)
        {
            throw new ArgumentException("The root already belongs to a focus manager.", nameof(root));
        }

        Root = root;
        root.SetFocusOwner(this);
    }

    /// <summary>Raised before an observable focus request commits; unavailable cleanup bypasses this preview.</summary>
    public event EventHandler<FocusChangingEventArgs>? Changing;

    /// <summary>Raised after the manager commits away from a previous control.</summary>
    public event EventHandler<FocusChangedEventArgs>? Lost;

    /// <summary>Raised after the manager commits to a current control.</summary>
    public event EventHandler<FocusChangedEventArgs>? Gained;

    /// <summary>Gets the owned attached tree root.</summary>
    public ControlBase Root { get; }

    /// <summary>Gets the currently focused control, or null.</summary>
    public ControlBase? Focused { get; private set; }

    private bool IsChanging { get; set; }

    private bool IsPumping { get; set; }

    private bool CleanupPending { get; set; }

    private bool DisposalPending { get; set; }

    private List<ControlBase>? EligibilityNotificationsPending { get; set; }

    private Queue<(
        ControlBase? Control,
        FocusReason Reason,
        bool Cancellable,
        Action<bool, Exception?>? Completion,
        Func<bool>? Valid,
        ModalScope? Scope,
        ExceptionDispatchInfo? PriorFailure)>? PendingRequests
    { get; set; }

    /// <summary>Gets whether disposal has been requested, including while physical cleanup is deferred.</summary>
    internal bool IsDisposed { get; private set; }

    #endregion

    #region Focus operations

    /// <summary>Requests focus for one member, or releases focus with null.</summary>
    /// <param name="control">The requested member, or null.</param>
    /// <returns>True when focus is committed or already matches; false when blocked, ineligible, or cancelled.</returns>
    /// <exception cref="ArgumentException">The control is not a member of this tree.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public bool Focus(ControlBase? control) => Focus(control, FocusReason.Programmatic, cancellable: true);

    /// <summary>Requests one reasoned focus transition for an owned target or null release.</summary>
    /// <param name="control">The requested member, or null.</param>
    /// <param name="reason">The defined reason published with observable callbacks.</param>
    /// <param name="cancellable">Whether a preview cancellation may reject the transition.</param>
    /// <returns>True when focus is committed or already matches; false when blocked, ineligible, or cancelled.</returns>
    /// <exception cref="ArgumentException">The control is not a member of this tree.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal bool Focus(ControlBase? control, FocusReason reason, bool cancellable)
    {
        ValidateRequest(control, reason);

        if (!IsAllowed(control))
        {
            return false;
        }

        if (IsChanging || IsPumping)
        {
            Enqueue(
                control,
                reason,
                cancellable,
                completion: null,
                isValid: null,
                priorFailure: null);
            return false;
        }

        return IsEligibleTarget(control) &&
            Change(control, reason, cancellable, publishChanging: true);
    }

    /// <summary>Requests focus and publishes its normal immediate or deferred completion exactly once.</summary>
    /// <param name="control">The requested member, or null.</param>
    /// <param name="reason">The defined reason published with observable callbacks.</param>
    /// <param name="cancellable">Whether a preview cancellation may reject the transition.</param>
    /// <param name="completion">
    /// The callback receiving whether the request ultimately committed and its exceptional failure, if any.
    /// </param>
    /// <param name="isValid">An optional predicate that must remain true until the deferred request begins.</param>
    /// <param name="priorFailure">An optional earlier failure that remains authoritative through deferred work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="completion"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a member of this tree.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    /// <exception cref="Exception">
    /// A prior failure, focus-state callback, focus event, or completion callback fails.
    /// The earliest failure remains authoritative after cleanup and completion publication.
    /// </exception>
    internal void Focus(
        ControlBase? control,
        FocusReason reason,
        bool cancellable,
        Action<bool, Exception?> completion,
        Func<bool>? isValid = null,
        ExceptionDispatchInfo? priorFailure = null)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ValidateRequest(control, reason);
        Enqueue(control, reason, cancellable, completion, isValid, priorFailure);
        PumpPendingRequests();
    }

    /// <summary>Moves focus through tab index then tree order with wrapping.</summary>
    /// <param name="reverse">Whether to traverse backward.</param>
    /// <returns>True when an eligible target exists and accepts focus.</returns>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public bool MoveNext(bool reverse = false)
        => MoveNext(Focused, reverse);

    /// <summary>Moves focus from a stable member anchor after an input route has completed.</summary>
    /// <param name="anchor">The member used to determine the traversal position, or null.</param>
    /// <param name="reverse">Whether to traverse backward.</param>
    /// <returns>True when an eligible target exists and accepts focus.</returns>
    public bool MoveNext(ControlBase? anchor, bool reverse = false)
    {
        VerifyAccess();
        if (anchor is not null && !IsMember(anchor))
        {
            throw new ArgumentException("The traversal anchor does not belong to this tree.", nameof(anchor));
        }

        var candidates = new List<ControlBase>();
        var tracker = anchor is null ? null : new AnchorTracker(anchor);
        var modality = Root.ModalityOwner;
        var boundary = anchor is null ? null : modality?.BoundaryFor(anchor);
        var scope = modality?.Active is not null && boundary is null
            ? null
            : FindScope(anchor, boundary);

        if (scope is not null)
        {
            CollectScope(scope, candidates, includeOwner: !ReferenceEquals(scope, Root), tracker);
        }
        else if (modality?.Active is not null)
        {
            for (var index = 0; index < modality.ActiveRootCount; index++)
            {
                CollectScope(modality.ActiveRootAt(index), candidates, includeOwner: true, tracker);
            }
        }
        else
        {
            CollectScope(Root, candidates, includeOwner: false, tracker);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var current = candidates.FindIndex(item => ReferenceEquals(item, anchor));
        int next;

        if (current >= 0)
        {
            next = reverse
                ? current == 0 ? candidates.Count - 1 : current - 1
                : current == candidates.Count - 1 ? 0 : current + 1;
        }
        else
        {
            // The anchor is a live focus target that is not itself a candidate - a IsTabStop=false
            // leaf, or a descendant excluded by an ancestor's TabNavigation.None. Folding this
            // into the wrap branch above sent Tab and Shift+Tab to opposite ends of the scope
            // instead of the nearest candidate in tree order; the tracker built alongside
            // CollectScope resolves that position directly.
            var following = tracker?.FollowingIndex ?? -1;
            following = following < 0 ? candidates.Count : following;
            next = reverse
                ? following == 0 ? candidates.Count - 1 : following - 1
                : following == candidates.Count ? 0 : following;
        }

        return Focus(candidates[next], FocusReason.Keyboard, cancellable: true);
    }

    /// <summary>Focuses the first eligible tab participant inside one owned scope.</summary>
    /// <param name="scope">The non-null scope contained by this focus root.</param>
    /// <returns>True when one descendant exists and accepts keyboard focus.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is not a member of this tree.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal bool FocusFirst(ControlBase scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        VerifyAccess();

        if (!IsMember(scope))
        {
            throw new ArgumentException("The focus scope does not belong to this tree.", nameof(scope));
        }

        var candidates = new List<ControlBase>();
        CollectScope(scope, candidates, includeOwner: false);

        return candidates.Count > 0 &&
               Focus(candidates[0], FocusReason.Keyboard, cancellable: true);
    }

    #endregion

    #region Cleanup

    /// <summary>Releases focus ownership and all manager references.</summary>
    /// <remarks>
    /// Disposal becomes observable immediately. When requested by a focus callback, physical cleanup
    /// completes before the enclosing focus request returns so that no in-flight transition can restore focus.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="Exception">A focus-state or queued completion callback fails after cleanup commits.</exception>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Root.VerifyMutable();
        RequestDisposal();
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases focus when a subtree becomes invalid.</summary>
    /// <param name="subtree">The non-null owned subtree that became unavailable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subtree"/> is null.</exception>
    internal void Unavailable(ControlBase subtree)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        Debug.Assert(IsMember(subtree), "Unavailable subtrees belong to the focus root.");

        if (Focused is null || !IsWithin(Focused, subtree))
        {
            return;
        }

        if (IsChanging)
        {
            CleanupPending = true;
            return;
        }

        _ = Change(
            null,
            FocusReason.Unavailable,
            cancellable: false,
            publishChanging: false);
    }

    /// <summary>Releases focus when the focused control loses its own eligibility.</summary>
    /// <param name="control">The non-null owned control whose eligibility changed.</param>
    /// <returns>Whether cleanup completed synchronously and notification may publish.</returns>
    internal bool Ineligible(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Debug.Assert(IsMember(control), "Ineligible controls belong to the focus root.");

        if (!ReferenceEquals(Focused, control))
        {
            return true;
        }

        if (IsChanging)
        {
            CleanupPending = true;
            (EligibilityNotificationsPending ??= []).Add(control);
            return false;
        }

        _ = Change(
            null,
            FocusReason.Unavailable,
            cancellable: false,
            publishChanging: false);
        return true;
    }

    /// <summary>Retries one restore without preview, then repairs manager and direct-control focus facts.</summary>
    /// <param name="control">The eligible restore target, or null.</param>
    /// <param name="displaced">The focus target observed before the failed restore, or null.</param>
    /// <exception cref="ArgumentException"><paramref name="control"/> is foreign.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    /// <exception cref="Exception">A committed lost, gained, or control-state callback fails.</exception>
    internal void RestoreCoherently(ControlBase? control, ControlBase? displaced)
    {
        ValidateRequest(control, FocusReason.Restore);
        var target = IsAllowed(control) && IsEligibleTarget(control) ? control : null;
        ExceptionDispatchInfo? failure = null;

        try
        {
            _ = Change(
                target,
                FocusReason.Restore,
                cancellable: false,
                publishChanging: false,
                pumpPending: false);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            CaptureFailure(
                () => ForceFocusCoherence(target, displaced),
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Commits last-resort focus identity and direct flags without user notification.</summary>
    /// <param name="control">The eligible target, or null.</param>
    /// <param name="displaced">A possibly stale former target, or null.</param>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal void ForceFocusCoherence(ControlBase? control, ControlBase? displaced)
    {
        VerifyAccess();
        var target = control is not null && IsMember(control) &&
            IsAllowed(control) && IsEligible(control)
                ? control
                : null;
        var current = Focused;
        Focused = target;

        if (displaced is not null && !ReferenceEquals(displaced, target))
        {
            displaced.CommitFocusFact(false);
        }

        if (current is not null &&
            !ReferenceEquals(current, target) &&
            !ReferenceEquals(current, displaced))
        {
            current.CommitFocusFact(false);
        }

        target?.CommitFocusFact(true);
    }

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    /// <exception cref="Exception">A focus-state or deferred eligibility callback fails after cleanup commits.</exception>
    internal void RootDisposed()
    {
        Debug.Assert(
            IsDisposed || ReferenceEquals(Root.FocusOwner, this),
            "Root disposal either observes prior cleanup or severs this manager's ownership.");
        RequestDisposal();
    }

    private void RequestDisposal()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        DisposalPending = true;

        if (IsChanging || IsPumping)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;
        CapturePendingPriorFailure(ref failure);
        CaptureFailure(CompleteDisposal, ref failure);
        CompletePendingRequestsForDisposal(ref failure);
        failure?.Throw();
    }

    private void CapturePendingPriorFailure(ref ExceptionDispatchInfo? failure)
    {
        if (failure is not null || PendingRequests is null)
        {
            return;
        }

        // A queued prior failure predates work that resumes after its enqueue.
        // Peek without consuming so each queued completion still receives only
        // the failure attached to its own request.
        foreach (var (Control, Reason, Cancellable, Completion, Valid, Scope, PriorFailure) in PendingRequests)
        {
            if (PriorFailure is not null)
            {
                failure = PriorFailure;
                break;
            }
        }
    }

    private void CompleteDisposal()
    {
        if (!DisposalPending)
        {
            return;
        }

        DisposalPending = false;
        ExceptionDispatchInfo? failure = null;
        var previous = Focused;
        Focused = null;

        CaptureFailure(() => previous?.SetFocused(false), ref failure);
        previous?.CommitFocusFact(false);
        CaptureFailure(() => Root.SetFocusOwner(null), ref failure);
        Changing = null;
        Lost = null;
        Gained = null;
        CleanupPending = false;
        CaptureFailure(PublishEligibilityNotifications, ref failure);
        failure?.Throw();
    }

    private void CompletePendingRequestsForDisposal(ref ExceptionDispatchInfo? failure)
    {
        var pending = PendingRequests;
        PendingRequests = null;

        if (pending is null)
        {
            return;
        }

        while (pending.Count > 0)
        {
            var (Control, Reason, Cancellable, Completion, Valid, Scope, PriorFailure) = pending.Dequeue();
            failure ??= PriorFailure;
            CaptureFailure(
                () => Completion?.Invoke(
                    false,
                    PriorFailure?.SourceException),
                ref failure);
        }
    }

    #endregion

    #region Focus transaction

    private bool Change(
        ControlBase? control,
        FocusReason reason,
        bool cancellable,
        bool publishChanging,
        Action<bool, Exception?>? completion = null,
        ExceptionDispatchInfo? priorFailure = null,
        bool pumpPending = true)
    {
        Debug.Assert(control is null || IsMember(control), "Focus transactions target only owned controls.");
        Debug.Assert(Enum.IsDefined(reason), "Focus transactions publish a defined reason.");

        if (ReferenceEquals(Focused, control))
        {
            var sameRequestFailure = priorFailure;
            var aggregateFailure = priorFailure;
            CaptureFailure(
                () => completion?.Invoke(true, priorFailure?.SourceException),
                ref sameRequestFailure);
            CapturePendingPriorFailure(ref aggregateFailure);
            aggregateFailure ??= sameRequestFailure;

            if (DisposalPending)
            {
                CaptureFailure(CompleteDisposal, ref sameRequestFailure);
                CapturePendingPriorFailure(ref aggregateFailure);
                aggregateFailure ??= sameRequestFailure;
            }

            if (IsDisposed)
            {
                CompletePendingRequestsForDisposal(ref aggregateFailure);
            }

            aggregateFailure?.Throw();
            return !IsDisposed;
        }

        IsChanging = true;
        var outcomeKnown = false;
        var committed = false;
        var requestFailure = priorFailure;

        try
        {
            try
            {
                var preview = new FocusChangingEventArgs(Focused, control, reason);

                if (publishChanging)
                {
                    Changing?.Invoke(this, preview);
                }

                // Preview handlers may detach, hide, disable, or dispose the target.
                // Revalidate after notification and before committing any focus flag.
                if (IsDisposed ||
                    (cancellable && preview.Cancel) ||
                    (control is not null && (!IsEligible(control) || !IsAllowed(control))))
                {
                    outcomeKnown = true;
                }
                else
                {
                    var previous = Focused;
                    var previousPath = GetPath(previous);
                    var currentPath = GetPath(control);
                    var commonLength = GetCommonLength(previousPath, currentPath);
                    Focused = control;
                    previous?.SetFocused(false);

                    if (!IsDisposed)
                    {
                        control?.SetFocused(true);
                    }

                    var changed = new FocusChangedEventArgs(previous, control, reason);

                    if (!IsDisposed && previous is not null)
                    {
                        previous.PublishLostFocus();

                        for (var index = previousPath.Count - 1;
                            index >= commonLength && !IsDisposed;
                            index--)
                        {
                            previousPath[index].PublishFocusLeft();
                        }

                        if (!IsDisposed)
                        {
                            Lost?.Invoke(this, changed);
                        }
                    }

                    if (!IsDisposed && control is not null)
                    {
                        for (var index = commonLength;
                            index < currentPath.Count && !IsDisposed;
                            index++)
                        {
                            currentPath[index].PublishFocusEntered();
                        }

                        if (!IsDisposed)
                        {
                            control.PublishGotFocus();
                        }

                        // Only Tab/Shift+Tab traversal and access-key-driven focus move a target
                        // the caller never directly interacted with, so only those two reveal it.
                        // A pointer press already proved the target was visible, and a
                        // programmatic ControlBase.Focus() call leaves that choice to the caller.
                        if (!IsDisposed && reason == FocusReason.Keyboard)
                        {
                            control.RevealForKeyboardFocus();
                        }

                        if (!IsDisposed)
                        {
                            Gained?.Invoke(this, changed);
                        }
                    }

                    committed = !IsDisposed;
                    outcomeKnown = true;
                }
            }
            catch (Exception exception)
            {
                requestFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        finally
        {
            IsChanging = false;
            var aggregateFailure = priorFailure;
            CapturePendingPriorFailure(ref aggregateFailure);
            aggregateFailure ??= requestFailure;

            if (DisposalPending)
            {
                CaptureFailure(CompleteDisposal, ref requestFailure);
            }
            else
            {
                CaptureFailure(
                    () =>
                    {
                        if (CleanupPending)
                        {
                            // Cleanup requested during preview/notification cannot recurse
                            // until the outer transaction has released its reentrancy guard.
                            CleanupPending = false;

                            if (Focused is not null && !IsEligible(Focused))
                            {
                                _ = Change(
                                    null,
                                    FocusReason.Unavailable,
                                    cancellable: false,
                                    publishChanging: false);
                            }
                        }
                    },
                    ref requestFailure);

                CaptureFailure(PublishEligibilityNotifications, ref requestFailure);
            }

            CapturePendingPriorFailure(ref aggregateFailure);
            aggregateFailure ??= requestFailure;

            if (completion is not null)
            {
                var outcomeFailure = requestFailure?.SourceException;
                CaptureFailure(
                    () => completion(
                        !IsDisposed && outcomeKnown && committed && ReferenceEquals(Focused, control),
                        outcomeFailure),
                    ref requestFailure);
            }

            CapturePendingPriorFailure(ref aggregateFailure);
            aggregateFailure ??= requestFailure;

            if (DisposalPending)
            {
                CaptureFailure(CompleteDisposal, ref requestFailure);
                CapturePendingPriorFailure(ref aggregateFailure);
                aggregateFailure ??= requestFailure;
            }

            if (IsDisposed)
            {
                CompletePendingRequestsForDisposal(ref aggregateFailure);
            }

            if (pumpPending && !IsDisposed)
            {
                CaptureFailure(PumpPendingRequests, ref aggregateFailure);
            }
            aggregateFailure?.Throw();
        }

        return committed && !IsDisposed;
    }

    private void PumpPendingRequests()
    {
        if (IsChanging || IsPumping || PendingRequests is not { Count: > 0 })
        {
            return;
        }

        IsPumping = true;
        ExceptionDispatchInfo? aggregateFailure = null;

        try
        {
            while (PendingRequests is { Count: > 0 } pending)
            {
                var (Control, Reason, Cancellable, Completion, Valid, Scope, PriorFailure) = pending.Dequeue();
                aggregateFailure ??= PriorFailure;
                CapturePendingPriorFailure(ref aggregateFailure);
                var requestFailure = PriorFailure;

                try
                {
                    if (Scope is { IsActive: false } ||
                        Valid?.Invoke() is false)
                    {
                        Completion?.Invoke(
                            false,
                            PriorFailure?.SourceException);
                    }
                    else if ((Control is null || IsMember(Control)) &&
                        IsAllowed(Control) &&
                        IsEligibleTarget(Control))
                    {
                        _ = Change(
                            Control,
                            Reason,
                            Cancellable,
                            publishChanging: true,
                            Completion,
                            PriorFailure);
                    }
                    else
                    {
                        Completion?.Invoke(
                            false,
                            PriorFailure?.SourceException);
                    }
                }
                catch (Exception exception)
                {
                    requestFailure ??= ExceptionDispatchInfo.Capture(exception);
                }

                CapturePendingPriorFailure(ref aggregateFailure);
                aggregateFailure ??= requestFailure;

                if (DisposalPending)
                {
                    ExceptionDispatchInfo? disposalFailure = null;
                    CaptureFailure(CompleteDisposal, ref disposalFailure);
                    CapturePendingPriorFailure(ref aggregateFailure);
                    aggregateFailure ??= disposalFailure;
                }

                if (IsDisposed)
                {
                    CompletePendingRequestsForDisposal(ref aggregateFailure);
                    break;
                }
            }
        }
        finally
        {
            if (PendingRequests is { Count: 0 })
            {
                PendingRequests = null;
            }

            IsPumping = false;
        }

        aggregateFailure?.Throw();
    }

    private void Enqueue(
        ControlBase? control,
        FocusReason reason,
        bool cancellable,
        Action<bool, Exception?>? completion,
        Func<bool>? isValid,
        ExceptionDispatchInfo? priorFailure)
    {
        var scope = Root.ModalityOwner?.Active;
        (PendingRequests ??= []).Enqueue((
            control,
            reason,
            cancellable,
            completion,
            isValid,
            scope,
            priorFailure));
    }

    private void PublishEligibilityNotifications()
    {
        if (EligibilityNotificationsPending is not { } pending)
        {
            return;
        }

        EligibilityNotificationsPending = null;
        ExceptionDispatchInfo? failure = null;

        foreach (var control in pending)
        {
            try
            {
                control.PublishDeferredFocusableChange();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        failure?.Throw();
    }

    #endregion

    #region Target resolution

    private void CollectScope(
        ControlBase owner,
        List<ControlBase> candidates,
        bool includeOwner,
        AnchorTracker? tracker = null)
    {
        Debug.Assert(owner is not null, "Focus traversal visits a concrete control.");
        Debug.Assert(candidates is not null, "Focus traversal accumulates into an owned candidate list.");

        if (includeOwner)
        {
            AppendParticipant(owner, candidates, tracker);
            return;
        }

        var participants = new List<(ControlBase Control, int Order)>();
        var count = owner.NavigationCount;

        for (var index = 0; index < count; index++)
        {
            participants.Add((owner.NavigationAt(index), index));
        }

        participants.Sort(static (left, right) =>
        {
            var tab = left.Control.TabIndex.CompareTo(right.Control.TabIndex);
            return tab != 0 ? tab : left.Order.CompareTo(right.Order);
        });

        foreach (var (Control, Order) in participants)
        {
            AppendParticipant(Control, candidates, tracker);
        }
    }

    private void AppendParticipant(ControlBase control, List<ControlBase> candidates, AnchorTracker? tracker = null)
    {
        Debug.Assert(control is not null, "Focus traversal visits a concrete control.");
        Debug.Assert(candidates is not null, "Focus traversal accumulates into an owned candidate list.");

        if (control.TabNavigation is TabNavigation.Continue or TabNavigation.Cycle)
        {
            if (IsEligible(control) && control.CanTabStop)
            {
                candidates.Add(control);
            }
            else
            {
                // A IsTabStop=false control that is nonetheless the live anchor never becomes a
                // candidate itself, so mark its tree position here - before recursing into any
                // descendants - so MoveNext can resolve the nearest neighbor instead of treating
                // it as -1/wrap.
                tracker?.MarkIfAnchor(control, candidates.Count);
            }

            CollectScope(control, candidates, includeOwner: false, tracker);
            return;
        }

        if (control.TabNavigation == TabNavigation.None)
        {
            if (IsEligible(control) && control.CanTabStop)
            {
                candidates.Add(control);
            }

            // Descendants never enter traversal under None, so an anchor anywhere in this
            // subtree - including control itself - resolves to whatever candidate follows this
            // contribution.
            tracker?.MarkIfAnchorOrDescendant(control, candidates.Count);
            return;
        }

        // Once contributes exactly one entry: the owner when it is eligible,
        // otherwise the first eligible descendant in its local order.
        if (IsEligible(control) && control.CanTabStop)
        {
            candidates.Add(control);
        }
        else
        {
            var descendants = new List<ControlBase>();
            CollectScope(control, descendants, includeOwner: false);
            if (descendants.Count > 0)
            {
                candidates.Add(descendants[0]);
            }
        }

        tracker?.MarkIfAnchorOrDescendant(control, candidates.Count);
    }

    /// <summary>Resolves an anchor's tree position for <see cref="MoveNext(ControlBase?, bool)"/> when the
    /// anchor is not itself a tab candidate.</summary>
    private sealed class AnchorTracker
    {
        private readonly HashSet<ControlBase> _ancestors = [];
        private readonly ControlBase _anchor;

        public AnchorTracker(ControlBase anchor)
        {
            _anchor = anchor;

            for (var current = anchor.Parent; current is not null; current = current.Parent)
            {
                _ = _ancestors.Add(current);
            }
        }

        /// <summary>Gets the index of the first candidate that follows the anchor's tree
        /// position, or -1 while unresolved.</summary>
        public int FollowingIndex { get; private set; } = -1;

        public void MarkIfAnchor(ControlBase control, int candidateCount)
        {
            if (FollowingIndex < 0 && ReferenceEquals(control, _anchor))
            {
                FollowingIndex = candidateCount;
            }
        }

        public void MarkIfAnchorOrDescendant(ControlBase control, int candidateCount)
        {
            if (FollowingIndex < 0 && (ReferenceEquals(control, _anchor) || _ancestors.Contains(control)))
            {
                FollowingIndex = candidateCount;
            }
        }
    }

    private ControlBase? FindScope(ControlBase? focused, ControlBase? boundary)
    {
        for (var current = focused; current is not null; current = current.Parent)
        {
            // Once is a contribution rule (contribute one entry, then continue in the parent
            // scope), not a traversal boundary - treating it as one here collapsed every
            // enclosing scope to that single entry, trapping Tab and Shift+Tab on it forever
            // AppendParticipant's own Once branch already implements the contribution
            // correctly and is unaffected by this change.
            if (current.TabNavigation is TabNavigation.Cycle)
            {
                return current;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return boundary is null && Root.ModalityOwner?.Active is null ? Root : null;
    }

    private bool IsEligible(ControlBase control) =>
        IsMember(control) && control is
        {
            IsDisposed: false, Dispatcher: not null, CanFocus: true, EffectiveIsVisible: true, EffectiveIsEnabled: true
        };

    private bool IsEligibleTarget(ControlBase? control) => control is null || IsEligible(control);

    private bool IsAllowed(ControlBase? control) =>
        control is null || Root.ModalityOwner?.Allows(control) is not false;

    private void ValidateMembership(ControlBase? control)
    {
        if (control is not null && !IsMember(control))
        {
            throw new ArgumentException(
                "The focus target does not belong to this tree.",
                nameof(control));
        }
    }

    private void ValidateRequest(ControlBase? control, FocusReason reason)
    {
        VerifyAccess();

        ArgumentOutOfRangeException.ThrowIfNotDefined(reason, nameof(reason), "The focus reason is unknown.");

        ValidateMembership(control);
    }

    private static List<ControlBase> GetPath(ControlBase? control)
    {
        var path = new List<ControlBase>();

        for (var current = control; current is not null; current = current.Parent)
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static int GetCommonLength(List<ControlBase> previous, List<ControlBase> current)
    {
        var length = Math.Min(previous.Count, current.Count);
        var index = 0;

        while (index < length && ReferenceEquals(previous[index], current[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Determines whether a control is reachable from <see cref="Root"/> through owned
    /// parent edges, the same membership test <see cref="MoveNext(ControlBase?, bool)"/> applies
    /// to its anchor. Exposed so a caller holding a possibly-stale anchor - captured before an
    /// input route ran, which may have detached it - can check before calling, instead of
    /// discovering the failure only as a thrown <see cref="ArgumentException"/>.</summary>
    [Pure]
    internal bool IsMember(ControlBase control)
    {
        Debug.Assert(control is not null, "Focus membership requires a control instance.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(ControlBase control, ControlBase subtree)
    {
        Debug.Assert(control is not null, "Focus containment requires a control instance.");
        Debug.Assert(subtree is not null, "Focus containment requires a subtree root.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Root.VerifyMutable();
        Debug.Assert(ReferenceEquals(Root.FocusOwner, this), "A live manager remains registered on its root.");
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
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

    #endregion
}
