// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Owns hit targeting, hover, press, and exclusive pointer capture.</summary>
public sealed class PointerManager: IDisposable
{
    private static readonly TimeSpan _multiClickInterval = TimeSpan.FromMilliseconds(500);
    private readonly TimeProvider _timeProvider;
    private long _lastClickTimestamp;
    private Point? _lastClickCells;
    private Buttons _lastClickButtons;
    private Control? _lastClickTarget;
    private int _clickCount;

    #region Construction and state

    /// <summary>Initializes pointer ownership for one attached root.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <param name="timeProvider">The optional monotonic gesture clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is detached or already belongs to a capture manager.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    public PointerManager(Control root, TimeProvider? timeProvider = null)
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
        Root = root;
        root.SetCaptureOwner(this);
    }

    /// <summary>Gets the owned attached tree root.</summary>
    public Control Root { get; }

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public Control? Captured { get; private set; }

    /// <summary>Gets the current hover target, or null.</summary>
    public Control? Hovered { get; private set; }

    /// <summary>Gets the raw pointer-down origin, or null.</summary>
    /// <remarks>This is gesture bookkeeping only; semantic pressed state belongs to <see cref="PressBehavior"/>.</remarks>
    public Control? PressOrigin { get; private set; }

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
    public bool Capture(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifyAccess();

        if (!IsMember(control))
        {
            throw new ArgumentException("The capture target does not belong to this tree.", nameof(control));
        }

        if (CancellationDepth > 0 || !IsEligible(control))
        {
            return false;
        }

        if (ReferenceEquals(Captured, control))
        {
            return true;
        }

        var previous = Captured;
        Captured = null;
        var failure = (ExceptionDispatchInfo?) null;

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

        if (!IsEligible(control))
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
            captured.NotifyLostPointerCapture(PointerCaptureLossReason.Explicit);
            captured.PublishLostPointerCapture(PointerCaptureLossReason.Explicit);
        }
    }

    /// <summary>Releases capture only when the requested tree member owns it.</summary>
    /// <param name="control">The non-null member requesting its own capture release.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    internal void Release(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        VerifyAccess();
        Debug.Assert(IsMember(control), "Protected capture release originates inside the owned tree.");

        if (ReferenceEquals(Captured, control))
        {
            Captured = null;
            control.NotifyLostPointerCapture(PointerCaptureLossReason.Explicit);
            control.PublishLostPointerCapture(PointerCaptureLossReason.Explicit);
        }
    }

    /// <summary>Targets, updates state, and routes one decoded pointer value.</summary>
    /// <param name="pointer">The immutable decoded pointer value.</param>
    /// <returns>The capture or hit-test target, or null.</returns>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public Control? Dispatch(Pointer pointer)
    {
        VerifyAccess();
        var targets = ResolveTargets(pointer);
        var physical = targets.PhysicalLeaf;
        var target = targets.DeliveryTarget;

        // Capture governs routed input, while hover tracks the physical pointer
        // position so a drag never leaves stale visual feedback behind.
        SetPointerPath(physical);

        if (pointer.Action == PointerAction.Press && pointer.Cells is not null)
        {
            PressOrigin = target;

            if ((pointer.Buttons & Buttons.Primary) != 0)
            {
                FocusTarget(targets.FocusTarget);
            }
        }

        if (target is not null)
        {
            _ = Router.Route(
                target,
                Events.Pointer,
                new PointerEventArgs(pointer, ResolveClickCount(pointer, target)));
        }

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            PressOrigin = null;
        }

        return target;
    }

    private int ResolveClickCount(Pointer pointer, Control target)
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
        SetPointerPath(null);
        PressOrigin = null;
        Root.SetCaptureOwner(null);
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Cancels state owned within one unavailable subtree.</summary>
    internal void Unavailable(Control subtree, ReleaseReason reason)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        Debug.Assert(IsMember(subtree), "Unavailable subtrees belong to the capture root.");
        Debug.Assert(Enum.IsDefined(reason), "Implicit capture release requires a defined reason.");

        if (IsWithin(Captured, subtree) || IsWithin(Hovered, subtree) || IsWithin(PressOrigin, subtree))
        {
            Cancel(reason, subtree);
        }
    }

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    internal void RootDisposed()
    {
        Debug.Assert(ReferenceEquals(Root.CaptureOwner, this), "Root disposal severs this manager's ownership.");

        Root.SetCaptureOwner(null);
        IsDisposed = true;
    }

    private void Cancel(ReleaseReason reason, Control subtree)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture cancellation requires a defined release reason.");
        Debug.Assert(IsMember(subtree), "Capture cancellation is scoped to the owned tree.");

        var captured = IsWithin(Captured, subtree) ? Captured : null;
        var hovered = IsWithin(Hovered, subtree) ? Hovered : null;
        var pressed = IsWithin(PressOrigin, subtree) ? PressOrigin : null;

        CancellationDepth++;

        try
        {
            var failure = (ExceptionDispatchInfo?) null;

            // Publish manager ownership first. Every callback below therefore
            // observes a coherent cleared state, and cancellation depth rejects
            // re-entrant capture until cancellation publication completes.
            if (captured is not null)
            {
                Captured = null;
            }

            if (hovered is not null)
            {
                Hovered = null;
            }

            if (pressed is not null)
            {
                PressOrigin = null;
            }

            CaptureFailure(() => ClearPointerPath(hovered), ref failure);

            if (captured is not null)
            {
                var lossReason = reason == ReleaseReason.TerminalFocusLost
                    ? PointerCaptureLossReason.TerminalFocusLost
                    : PointerCaptureLossReason.Unavailable;
                CaptureFailure(() => captured.NotifyLostPointerCapture(lossReason), ref failure);
                CaptureFailure(() => captured.PublishLostPointerCapture(lossReason), ref failure);
            }
            failure?.Throw();
        }
        finally
        {
            CancellationDepth--;
        }
    }

    private static void CaptureFailure(System.Action action, ref ExceptionDispatchInfo? failure)
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

    private bool IsEligible(Control? control) =>
        control is not null && IsMember(control) && !control.IsDisposed &&
        control.Dispatcher is not null && control.EffectiveIsVisible && control.EffectiveIsEnabled;

    private bool IsMember(Control control)
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

    private static bool IsWithin(Control? control, Control subtree)
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

    private void SetPointerPath(Control? control)
    {
        Debug.Assert(control is null || IsMember(control), "Hover state remains inside the capture tree.");

        if (ReferenceEquals(Hovered, control))
        {
            return;
        }

        var previousPath = GetPath(Hovered);
        var currentPath = GetPath(control);
        var commonLength = GetCommonLength(previousPath, currentPath);

        for (var index = previousPath.Count - 1; index >= commonLength; index--)
        {
            previousPath[index].SetPointerOver(value: false, directlyOver: false);
        }

        Hovered = control;

        for (var index = 0; index < commonLength; index++)
        {
            var current = currentPath[index];
            current.SetPointerOver(value: true, directlyOver: ReferenceEquals(current, control));
        }

        for (var index = commonLength; index < currentPath.Count; index++)
        {
            var current = currentPath[index];
            current.SetPointerOver(value: true, directlyOver: ReferenceEquals(current, control));
        }
    }

    private static void ClearPointerPath(Control? control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            current.SetPointerOver(value: false, directlyOver: false);
        }
    }

    private static List<Control> GetPath(Control? control)
    {
        var path = new List<Control>();

        for (var current = control; current is not null; current = current.Parent)
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static int GetCommonLength(List<Control> previous, List<Control> current)
    {
        var length = Math.Min(previous.Count, current.Count);
        var index = 0;

        while (index < length && ReferenceEquals(previous[index], current[index]))
        {
            index++;
        }

        return index;
    }

    private InteractionTargets ResolveTargets(Pointer pointer)
    {
        var physical = pointer.Action == PointerAction.Leave || pointer.Cells is not { } cells
            ? null
            : Root.HitTest(cells);
        var capture = IsEligible(Captured) ? Captured : null;
        var delivery = capture ?? physical;

        return new InteractionTargets(physical, delivery, FindFocusTarget(delivery), capture);
    }

    private static Control? FindFocusTarget(Control? target)
    {
        for (var current = target; current is not null; current = current.Parent)
        {
            if (current.CanFocus)
            {
                return current;
            }
        }

        return null;
    }

    private static void FocusTarget(Control? target)
    {
        if (target is not null)
        {
            _ = target.FocusOwner?.Focus(target);
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
