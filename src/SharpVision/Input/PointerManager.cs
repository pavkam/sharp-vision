// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Owns hit targeting, hover, press, and exclusive pointer capture.</summary>
[PublicAPI]
public sealed class PointerManager: IDisposable
{
    private static readonly TimeSpan _multiClickInterval = TimeSpan.FromMilliseconds(500);
    private readonly HashSet<ControlBase> _appliedHoverPath = new(ReferenceEqualityComparer.Instance);
    private readonly List<ControlBase> _pathBuffer = [];
    private readonly List<ControlBase> _exitBuffer = [];
    private readonly TimeProvider _timeProvider;
    private readonly Func<ControlBase?, ControlBase?>? _activationTarget;
    private long _hoverRevision;
    private long _lastClickTimestamp;
    private Point? _lastClickCells;
    private Buttons _lastClickButtons;
    private ControlBase? _lastClickTarget;
    private int _clickCount;
    private ControlBase? _hoverBoundary;
    private Point? _lastPointerCells;

    #region Construction and state

    /// <summary>Initializes pointer ownership for one attached root. The caller owns and must
    /// dispose the manager.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <param name="timeProvider">The optional monotonic gesture clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is detached or already belongs to a capture manager.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    [MustDisposeResource]
    public PointerManager(ControlBase root, TimeProvider? timeProvider = null)
        : this(root, timeProvider, activationTarget: null)
    {
    }

    /// <summary>Initializes pointer ownership with an application activation-target callback. The
    /// caller owns and must dispose the manager.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <param name="timeProvider">The optional monotonic gesture clock.</param>
    /// <param name="activationTarget">
    /// The optional callback for modal-eligible primary-press targets. Its result bounds pointer focus.
    /// </param>
    [MustDisposeResource]
    internal PointerManager(
        ControlBase root,
        TimeProvider? timeProvider,
        Func<ControlBase?, ControlBase?>? activationTarget)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.VerifyMutable();

        if (root.Dispatcher is null)
        {
            throw new ArgumentException("The capture root must be attached.", nameof(root));
        }

        if (root.CaptureOwner is not null)
        {
            throw new ArgumentException("The root already belongs to a capture manager.", nameof(root));
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _activationTarget = activationTarget;
        Root = root;
        root.SetCaptureOwner(this);
    }

    /// <summary>Gets the owned attached tree root.</summary>
    public ControlBase Root { get; }

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public ControlBase? Captured { get; private set; }

    /// <summary>Gets the current hover target, or null.</summary>
    public ControlBase? Hovered { get; private set; }

    /// <summary>Gets the raw pointer-down origin, or null.</summary>
    /// <remarks>This is gesture bookkeeping only; semantic pressed state belongs to <see cref="PressBehavior"/>.</remarks>
    public ControlBase? PressOrigin { get; private set; }

    private bool IsDisposed { get; set; }

    private int CancellationDepth { get; set; }

    #endregion

    #region Capture and dispatch

    /// <summary>Captures all subsequent pointer input to one eligible tree member.</summary>
    /// <param name="control">The non-null requested member.</param>
    /// <returns>True when capture is acquired or already owned; false when ineligible.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is outside this tree.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public bool Capture(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifyAccess();

        if (!IsMember(control))
        {
            throw new ArgumentException("The capture target does not belong to this tree.", nameof(control));
        }

        if (CancellationDepth > 0 || !IsEligibleInteraction(control))
        {
            return false;
        }

        if (ReferenceEquals(Captured, control))
        {
            return true;
        }

        var previous = Captured;
        Captured = null;
        ExceptionDispatchInfo? failure = null;

        if (previous is not null)
        {
            CancellationDepth++;

            try
            {
                CaptureFailure(
                    () => previous.NotifyLostPointerCapture(PointerCaptureLossReason.Transferred),
                    ref failure);
                CaptureFailure(
                    () => previous.PublishLostPointerCapture(PointerCaptureLossReason.Transferred),
                    ref failure);
            }
            finally
            {
                CancellationDepth--;
            }
        }

        if (!IsEligibleInteraction(control))
        {
            failure?.Throw();
            return false;
        }

        Captured = control;
        failure?.Throw();
        return true;
    }

    /// <summary>Explicitly releases capture without cancelling pointer state.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public void Release()
    {
        VerifyAccess();
        if (Captured is { } captured)
        {
            Captured = null;
            ExceptionDispatchInfo? failure = null;
            CaptureFailure(
                () => captured.NotifyLostPointerCapture(PointerCaptureLossReason.Explicit),
                ref failure);
            CaptureFailure(
                () => captured.PublishLostPointerCapture(PointerCaptureLossReason.Explicit),
                ref failure);
            failure?.Throw();
        }
    }

    /// <summary>Releases capture only when the requested tree member owns it.</summary>
    /// <param name="control">The non-null member requesting its own capture release.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal void Release(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifyAccess();
        Debug.Assert(IsMember(control), "Protected capture release originates inside the owned tree.");

        if (ReferenceEquals(Captured, control))
        {
            Captured = null;
            ExceptionDispatchInfo? failure = null;
            CaptureFailure(
                () => control.NotifyLostPointerCapture(PointerCaptureLossReason.Explicit),
                ref failure);
            CaptureFailure(
                () => control.PublishLostPointerCapture(PointerCaptureLossReason.Explicit),
                ref failure);
            failure?.Throw();
        }
    }

    /// <summary>Targets, updates state, and routes one decoded pointer value.</summary>
    /// <param name="pointer">The immutable decoded pointer value.</param>
    /// <returns>The modal-eligible capture or hit-test target, or null.</returns>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    /// <exception cref="Exception">
    /// A hover, focus, routed-input, or dismissal callback fails after state commits.
    /// </exception>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public ControlBase? Dispatch(Pointer pointer)
    {
        VerifyAccess();
        var targets = ResolveTargets(pointer);
        var target = targets.DeliveryTarget;
        ExceptionDispatchInfo? failure = null;

        // Capture governs routed input. Hover follows only the modal-eligible
        // physical path and is capped at that plane's route boundary.
        CaptureFailure(
            () => SetPointerPath(targets.HoverTarget, targets.HoverBoundary),
            ref failure);

        if (!IsInteractionSnapshotValid(targets))
        {
            CompletePress(pointer);
            BreakClickChainOnSwallowedPress(pointer);

            failure?.Throw();
            return null;
        }

        // This is the sole wheel/press dismiss trigger for a Dismiss-policy scope. A wheel whose
        // hit lies inside the modal subtree is never dismiss-eligible here or later, even when the
        // routed target leaves it unhandled (e.g. a list already at its scroll endpoint) - it is
        // swallowed instead, matching mainstream drop-down behavior.
        if (targets.IsOutsideModalPlane && targets.CaptureOwner is null)
        {
            CompletePress(pointer);
            BreakClickChainOnSwallowedPress(pointer);

            if (pointer.Action == PointerAction.Wheel ||
                (pointer.Action == PointerAction.Press &&
                    (pointer.Buttons & Buttons.Primary) != 0))
            {
                CaptureFailure(
                    () => targets.Modality?.RequestDismiss(targets.ModalScope!),
                    ref failure);
            }

            failure?.Throw();
            return null;
        }

        if (failure is not null)
        {
            CompletePress(pointer);
            failure.Throw();
        }

        if (pointer is { Action: PointerAction.Press, Cells: not null })
        {
            PressOrigin = target;

            if ((pointer.Buttons & Buttons.Primary) != 0)
            {
                ControlBase? activationBoundary = null;
                CaptureFailure(
                    () => activationBoundary = _activationTarget?.Invoke(target),
                    ref failure);

                if (failure is not null)
                {
                    if (ReferenceEquals(PressOrigin, target))
                    {
                        PressOrigin = null;
                    }

                    failure.Throw();
                }

                var focusTarget = FindFocusTarget(target, activationBoundary ?? targets.DeliveryBoundary);
                CaptureFailure(() => FocusTarget(focusTarget), ref failure);

                if (!IsInteractionSnapshotValid(targets))
                {
                    if (ReferenceEquals(PressOrigin, target))
                    {
                        PressOrigin = null;
                    }

                    failure?.Throw();
                    return null;
                }

                if (failure is not null)
                {
                    if (ReferenceEquals(PressOrigin, target))
                    {
                        PressOrigin = null;
                    }

                    failure.Throw();
                }

            }
        }

        PointerEventArgs? routedEvent = null;

        if (target is not null)
        {
            routedEvent = new PointerEventArgs(pointer, ResolveClickCount(pointer, target));
            CaptureFailure(
                () => _ = Router.Route(
                    target,
                    Events.Pointer,
                    routedEvent),
                ref failure);
        }
        else
        {
            BreakClickChainOnSwallowedPress(pointer);
        }

        CompletePress(pointer);

        failure?.Throw();
        return target;
    }

    private void CompletePress(Pointer pointer)
    {
        if (pointer.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(pointer))
        {
            PressOrigin = null;
        }
    }

    /// <summary>
    /// Breaks the multi-click chain when a press is consumed without ever reaching
    /// <see cref="ResolveClickCount"/> - an invalid interaction snapshot, an outside-modal-plane
    /// dismiss, or a press that resolves no target. Left unbroken, the chain state from the last
    /// routed press survives, and the next physical press after the swallowed one is reported as
    /// a continuation of a click sequence the user never performed.
    /// </summary>
    private void BreakClickChainOnSwallowedPress(Pointer pointer)
    {
        if (pointer.Action != PointerAction.Press)
        {
            return;
        }

        _clickCount = 0;
        _lastClickTarget = null;
    }

    private int ResolveClickCount(Pointer pointer, ControlBase target)
    {
        Debug.Assert(target is not null, "Gesture metadata requires a routed target.");

        if (pointer.Action != PointerAction.Press)
        {
            return 0;
        }

        var now = _timeProvider.GetTimestamp();
        var continues = _clickCount > 0 &&
                        ReferenceEquals(_lastClickTarget, target) &&
                        _lastClickCells == pointer.Cells &&
                        _lastClickButtons == pointer.Buttons &&
                        _timeProvider.GetElapsedTime(_lastClickTimestamp, now) <= _multiClickInterval;
        _clickCount = continues && _clickCount < int.MaxValue ? _clickCount + 1 : 1;
        _lastClickTimestamp = now;
        _lastClickCells = pointer.Cells;
        _lastClickButtons = pointer.Buttons;
        _lastClickTarget = target;
        return _clickCount;
    }

    /// <summary>Cancels capture, hover, and press because terminal focus was lost.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public void TerminalFocusLost()
    {
        VerifyAccess();
        Cancel(ReleaseReason.TerminalFocusLost, Root);
    }

    #endregion

    #region Cleanup

    /// <summary>Releases pointer ownership and all manager references.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Root.VerifyMutable();
        Captured = null;
        SetPointerPath(control: null, boundary: null);
        PressOrigin = null;
        _lastClickTarget = null;
        Root.SetCaptureOwner(null);
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Cancels state owned within one unavailable subtree.</summary>
    internal void Unavailable(ControlBase subtree, ReleaseReason reason)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        Debug.Assert(IsMember(subtree), "Unavailable subtrees belong to the capture root.");
        Debug.Assert(Enum.IsDefined(reason), "Implicit capture release requires a defined reason.");

        if (IsWithin(Captured, subtree) || IsWithin(Hovered, subtree) || IsWithin(PressOrigin, subtree))
        {
            Cancel(reason, subtree);
        }

        // _lastClickTarget outlives a completed press/release cycle - PressOrigin is already
        // null by then - so it needs its own check here instead of riding along with Cancel's
        // clearPressed branch above, which only runs when one of the other three is still live.
        // Left uncleared, a detached or disposed clicked control stays strongly referenced until
        // the next press anywhere in the tree, or manager disposal.
        if (IsWithin(_lastClickTarget, subtree))
        {
            _lastClickTarget = null;
        }
    }

    /// <summary>Constrains retained pointer state to one newly active modal plane.</summary>
    /// <param name="scope">The non-null active scope whose roots now govern interaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal void ModalScopeChanged(ModalScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        VerifyAccess();
        Debug.Assert(scope.IsActive, "Pointer confinement begins from the newly active scope.");

        var captured = Captured is not null && scope.BoundaryFor(Captured) is null
            ? Captured
            : null;
        var hoverBoundary = Hovered is null ? null : scope.BoundaryFor(Hovered);
        var hoverTarget = hoverBoundary is null ? null : Hovered;
        var clearPressed = PressOrigin is not null && scope.BoundaryFor(PressOrigin) is null;

        if (captured is null &&
            ReferenceEquals(Hovered, hoverTarget) &&
            ReferenceEquals(_hoverBoundary, hoverBoundary) &&
            !clearPressed)
        {
            return;
        }

        Cancel(
            ReleaseReason.ModalScopeChanged,
            captured,
            hoverTarget,
            hoverBoundary,
            clearPressed);
    }

    /// <summary>Expands retained eligible hover ancestry after the final modal scope ends.</summary>
    /// <remarks>
    /// Reconciliation retains the current geometric target and does not hit test,
    /// route input, or alter capture and press ownership.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    /// <exception cref="Exception">A hover callback fails after the complete path commits.</exception>
    internal void ReconcileUnrestrictedModality()
    {
        VerifyAccess();
        Debug.Assert(
            Root.ModalityOwner?.Active is null,
            "Unrestricted pointer reconciliation follows the final modal-scope removal.");

        if (Hovered is not { } hovered ||
            !IsEligibleInteraction(hovered) ||
            ReferenceEquals(_hoverBoundary, Root))
        {
            return;
        }

        SetPointerPath(hovered, Root);
    }

    /// <summary>Re-hit-tests at the last known pointer cell and repairs the hover path when the
    /// control under that cell changed - covering wheel scroll, programmatic scroll, and terminal
    /// resize, none of which move the physical pointer itself and so never dispatch a pointer
    /// record of their own.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    /// <exception cref="Exception">A hover callback fails after the complete path commits.</exception>
    internal void ReconcileHover()
    {
        VerifyAccess();

        if (_lastPointerCells is not { } cells)
        {
            return;
        }

        var physical = Root.HitTest(cells);
        var (hoverTarget, hoverBoundary) = ResolveHoverTarget(physical, Root.ModalityOwner?.Active);

        if (ReferenceEquals(hoverTarget, Hovered) && ReferenceEquals(hoverBoundary, _hoverBoundary))
        {
            return;
        }

        SetPointerPath(hoverTarget, hoverBoundary);
    }

    /// <summary>Resolves the hover target and its inclusive boundary for a physical hit-test
    /// result, honoring the active modal scope exactly as <see cref="ResolveTargets"/> does.</summary>
    private (ControlBase? Target, ControlBase? Boundary) ResolveHoverTarget(ControlBase? physical, ModalScope? scope)
    {
        var hoverBoundary = scope is null
            ? physical is null ? null : Root
            : physical is null ? null : scope.BoundaryFor(physical);
        var hoverTarget = hoverBoundary is null ? null : physical;
        return (hoverTarget, hoverBoundary);
    }

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    internal void RootDisposed()
    {
        Debug.Assert(ReferenceEquals(Root.CaptureOwner, this), "Root disposal severs this manager's ownership.");

        Root.SetCaptureOwner(null);
        IsDisposed = true;
    }

    private void Cancel(ReleaseReason reason, ControlBase subtree)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture cancellation requires a defined release reason.");
        Debug.Assert(IsMember(subtree), "Capture cancellation is scoped to the owned tree.");

        var captured = IsWithin(Captured, subtree) ? Captured : null;
        var clearHover = IsWithin(Hovered, subtree);
        var clearPressed = IsWithin(PressOrigin, subtree);

        Cancel(
            reason,
            captured,
            clearHover ? null : Hovered,
            clearHover ? null : _hoverBoundary,
            clearPressed);
    }

    private void Cancel(
        ReleaseReason reason,
        ControlBase? captured,
        ControlBase? hoverTarget,
        ControlBase? hoverBoundary,
        bool clearPressed)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture cancellation requires a defined release reason.");
        Debug.Assert(captured is null || ReferenceEquals(Captured, captured), "Cancellation clears the retained capture owner.");
        Debug.Assert(hoverTarget is null || IsMember(hoverTarget), "Retained hover remains inside the pointer tree.");
        Debug.Assert((hoverTarget is null) == (hoverBoundary is null), "A hover target and boundary are retained together.");

        CancellationDepth++;

        try
        {
            ExceptionDispatchInfo? failure = null;

            // Publish manager ownership first. Every callback below therefore
            // observes a coherent cleared state, and cancellation depth rejects
            // re-entrant capture until cancellation publication completes.
            if (captured is not null)
            {
                Captured = null;
            }

            if (clearPressed)
            {
                PressOrigin = null;
                _lastClickTarget = null;
            }

            CaptureFailure(
                () => SetPointerPath(hoverTarget, hoverBoundary),
                ref failure);

            if (captured is { } captureOwner)
            {
                var lossReason = reason switch
                {
                    ReleaseReason.Detached => PointerCaptureLossReason.Unavailable,
                    ReleaseReason.Disabled => PointerCaptureLossReason.Unavailable,
                    ReleaseReason.Hidden => PointerCaptureLossReason.Unavailable,
                    ReleaseReason.TerminalFocusLost => PointerCaptureLossReason.TerminalFocusLost,
                    ReleaseReason.Transferred => PointerCaptureLossReason.Transferred,
                    ReleaseReason.ModalScopeChanged => PointerCaptureLossReason.ModalScopeChanged,
                    ReleaseReason.Disposed => PointerCaptureLossReason.Unavailable,
                    _ => throw new UnreachableException("Pointer cancellation receives only defined release reasons."),
                };
                CaptureFailure(
                    () => PublishCaptureLoss(captureOwner, lossReason),
                    ref failure);
            }

            failure?.Throw();
        }
        finally
        {
            CancellationDepth--;
        }
    }

    private void PublishCaptureLoss(ControlBase captured, PointerCaptureLossReason reason)
    {
        ExceptionDispatchInfo? failure = null;
        CancellationDepth++;

        try
        {
            CaptureFailure(() => captured.NotifyLostPointerCapture(reason), ref failure);
            CaptureFailure(() => captured.PublishLostPointerCapture(reason), ref failure);
            failure?.Throw();
        }
        finally
        {
            CancellationDepth--;
        }
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

    #region Target resolution

    private bool IsEligible(ControlBase? control) =>
        control is not null && IsMember(control) && control is
        { IsDisposed: false, Dispatcher: not null, EffectiveIsVisible: true, EffectiveIsEnabled: true };

    private bool IsEligibleInteraction(ControlBase? control) =>
        IsEligible(control) && Root.ModalityOwner?.Allows(control) is not false;

    private bool IsInteractionSnapshotValid(in InteractionTargets targets)
    {
        var modality = Root.ModalityOwner;

        if (!ReferenceEquals(modality, targets.Modality) ||
            !ReferenceEquals(modality?.Active, targets.ModalScope))
        {
            return false;
        }

        var target = targets.DeliveryTarget;
        return target is null ||
            (IsEligible(target) && modality?.Allows(target) is not false);
    }

    private bool IsMember(ControlBase control)
    {
        Debug.Assert(control is not null, "Capture membership requires a control instance.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(ControlBase? control, ControlBase subtree)
    {
        Debug.Assert(subtree is not null, "Subtree containment requires a non-null root.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void SetPointerPath(ControlBase? control, ControlBase? boundary)
    {
        Debug.Assert(control is null || IsMember(control), "Hover state remains inside the capture tree.");
        Debug.Assert((control is null) == (boundary is null), "Hover state has an inclusive boundary exactly when it has a target.");
        Debug.Assert(boundary is null || IsWithin(control, boundary), "The hover boundary owns the hover target.");

        Hovered = control;
        _hoverBoundary = boundary;
        _hoverRevision++;
        var revision = _hoverRevision;
        ExceptionDispatchInfo? failure = null;

        ReconcilePointerPath(revision, ref failure);
        failure?.Throw();
    }

    private void ReconcilePointerPath(
        long revision,
        ref ExceptionDispatchInfo? failure)
    {
        var target = Hovered;
        FillPath(target, _hoverBoundary);
        _exitBuffer.Clear();

        foreach (var applied in _appliedHoverPath)
        {
            if (!ContainsReference(_pathBuffer, applied))
            {
                _exitBuffer.Add(applied);
            }
        }

        _exitBuffer.Sort(static (left, right) => Depth(right).CompareTo(Depth(left)));

        foreach (var previous in _exitBuffer)
        {
            ApplyPointerOver(previous, value: false, directlyOver: false, ref failure);

            if (revision != _hoverRevision)
            {
                _exitBuffer.Clear();
                return;
            }
        }

        // Scratch storage for this call only - clearing it here, not just at the next call's own
        // top-of-method Clear(), means a control this exited that is never hovered over again
        // (detached or disposed with no further pointer movement anywhere in the tree) is not
        // kept referenced indefinitely.
        _exitBuffer.Clear();

        foreach (var current in _pathBuffer)
        {
            if (!_appliedHoverPath.Contains(current))
            {
                continue;
            }

            ApplyPointerOver(
                current,
                value: true,
                directlyOver: ReferenceEquals(current, target),
                ref failure);

            if (revision != _hoverRevision)
            {
                return;
            }
        }

        foreach (var current in _pathBuffer)
        {
            if (_appliedHoverPath.Contains(current))
            {
                continue;
            }

            ApplyPointerOver(
                current,
                value: true,
                directlyOver: ReferenceEquals(current, target),
                ref failure);

            if (revision != _hoverRevision)
            {
                return;
            }
        }
    }

    private void ApplyPointerOver(
        ControlBase control,
        bool value,
        bool directlyOver,
        ref ExceptionDispatchInfo? failure)
    {
        _ = value
            ? _appliedHoverPath.Add(control)
            : _appliedHoverPath.Remove(control);
        CaptureFailure(
            () => control.SetPointerOver(value, directlyOver),
            ref failure);

        _ = control.IsPointerOver
            ? _appliedHoverPath.Add(control)
            : _appliedHoverPath.Remove(control);
    }

    private void FillPath(ControlBase? control, ControlBase? boundary)
    {
        _pathBuffer.Clear();

        for (var current = control; current is not null; current = current.Parent)
        {
            _pathBuffer.Add(current);

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        Debug.Assert(
            control is null || (_pathBuffer.Count > 0 && ReferenceEquals(_pathBuffer[^1], boundary)),
            "The hover boundary owns the complete path.");
        _pathBuffer.Reverse();
    }

    private static bool ContainsReference(
        List<ControlBase> controls,
        ControlBase candidate)
    {
        foreach (var control in controls)
        {
            if (ReferenceEquals(control, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static int Depth(ControlBase control)
    {
        var depth = 0;

        for (var current = control; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private InteractionTargets ResolveTargets(Pointer pointer)
    {
        _lastPointerCells = pointer.Action == PointerAction.Leave ? null : pointer.Cells;
        var physical = _lastPointerCells is not { } cells ? null : Root.HitTest(cells);
        var modality = Root.ModalityOwner;
        var scope = modality?.Active;
        var (hoverTarget, hoverBoundary) = ResolveHoverTarget(physical, scope);
        var capture = IsEligible(Captured) &&
            (scope is null || scope.BoundaryFor(Captured!) is not null)
            ? Captured
            : null;
        var delivery = capture ?? hoverTarget;
        var deliveryBoundary = delivery is null
            ? null
            : scope?.BoundaryFor(delivery) ?? Root;

        return new InteractionTargets(
            physical,
            hoverTarget,
            hoverBoundary,
            delivery,
            deliveryBoundary,
            capture,
            modality,
            scope);
    }

    /// <summary>Gets the total number of ancestry walks <see cref="FindFocusTarget"/> has performed
    /// since construction, for a deterministic bound on how often Press-only focus resolution runs.</summary>
    internal int FocusResolutionCount { get; private set; }

    private ControlBase? FindFocusTarget(ControlBase? target, ControlBase? boundary)
    {
        FocusResolutionCount++;

        for (var current = target; current is not null; current = current.Parent)
        {
            if (current.CanFocus)
            {
                return current;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return null;
    }

    private static void FocusTarget(ControlBase? target)
    {
        if (target is not null)
        {
            _ = target.FocusOwner?.Focus(target, FocusReason.Pointer, cancellable: true);
        }
    }


    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Root.VerifyMutable();
        Debug.Assert(ReferenceEquals(Root.CaptureOwner, this), "A live manager remains registered on its root.");
    }

    #endregion
}
