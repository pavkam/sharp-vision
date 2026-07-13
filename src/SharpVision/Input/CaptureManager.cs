// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;


using SharpVision.Terminal.Input;

/// <summary>Owns hit targeting, hover, press, and exclusive pointer capture.</summary>
public sealed class CaptureManager: IDisposable
{
    /// <summary>Initializes pointer ownership for one attached root.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is detached or already belongs to a capture manager.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    public CaptureManager(Control root)
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

        Root = root;
        root.SetCaptureOwner(this);
    }

    /// <summary>Raised once when capture or an active press is implicitly cancelled.</summary>
    public event EventHandler<CaptureCancelledEventArgs>? Cancelled;

    /// <summary>Gets the owned attached tree root.</summary>
    public Control Root { get; }

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public Control? Captured { get; private set; }

    /// <summary>Gets the current hover target, or null.</summary>
    public Control? Hovered { get; private set; }

    /// <summary>Gets the control where the active press began, or null.</summary>
    public Control? Pressed { get; private set; }

    private bool IsDisposed { get; set; }

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

        if (!IsEligible(control))
        {
            return false;
        }

        Captured = control;
        return true;
    }

    /// <summary>Explicitly releases capture without cancelling pointer state.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public void Release()
    {
        VerifyAccess();
        Captured = null;
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
        Control? physical = pointer.Action == PointerAction.Leave || pointer.Cells is not { } cells
            ? null
            : Root.HitTest(cells);
        Control? target = IsEligible(Captured) ? Captured : physical;

        // Capture governs routed input, while hover tracks the physical pointer
        // position so a drag never leaves stale visual feedback behind.
        SetHovered(ResolveHover(physical));

        if (pointer.Action == PointerAction.Press && pointer.Cells is not null)
        {
            SetPressed(target);

            if ((pointer.Buttons & Buttons.Primary) != 0)
            {
                FocusTarget(target);
            }
        }

        if (target is not null)
        {
            Router.Route(target, Events.Pointer, new PointerEventArgs(pointer));
        }

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            SetPressed(null);
        }

        return target;
    }

    /// <summary>Cancels capture, hover, and press because terminal focus was lost.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public void TerminalFocusLost()
    {
        VerifyAccess();
        Cancel(ReleaseReason.TerminalFocusLost, Root);
    }

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
        SetHovered(null);
        SetPressed(null);
        Root.SetCaptureOwner(null);
        Cancelled = null;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Cancels state owned within one unavailable subtree.</summary>
    internal void Unavailable(Control subtree, ReleaseReason reason)
    {
        ArgumentNullException.ThrowIfNull(subtree);

        if (IsWithin(Captured, subtree) || IsWithin(Hovered, subtree) || IsWithin(Pressed, subtree))
        {
            Cancel(reason, subtree);
        }
    }

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    internal void RootDisposed()
    {
        Root.SetCaptureOwner(null);
        Cancelled = null;
        IsDisposed = true;
    }

    private void Cancel(ReleaseReason reason, Control subtree)
    {
        Control? cancelled = IsWithin(Captured, subtree) ? Captured : Pressed;

        if (IsWithin(Captured, subtree))
        {
            Captured = null;
        }

        if (IsWithin(Hovered, subtree))
        {
            SetHovered(null);
        }

        if (IsWithin(Pressed, subtree))
        {
            SetPressed(null);
        }

        if (cancelled is not null)
        {
            Cancelled?.Invoke(this, new CaptureCancelledEventArgs(cancelled, reason));
        }
    }

    private bool IsEligible(Control? control) =>
        control is not null && IsMember(control) && !control.IsDisposed &&
        control.Dispatcher is not null && control.EffectiveIsVisible && control.EffectiveIsEnabled;

    private bool IsMember(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
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
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void SetHovered(Control? control)
    {
        if (ReferenceEquals(Hovered, control))
        {
            return;
        }

        Hovered?.SetHovered(false);
        Hovered = control;
        control?.SetHovered(true);
    }

    private static Control? ResolveHover(Control? physical)
    {
        for (Control? current = physical; current is not null; current = current.Parent)
        {
            if (current.OwnsHover)
            {
                return current;
            }
        }

        return physical;
    }

    private static void FocusTarget(Control? target)
    {
        for (Control? current = target; current is not null; current = current.Parent)
        {
            if (current.CanFocus)
            {
                _ = current.FocusOwner?.Focus(current);
                return;
            }
        }
    }

    private void SetPressed(Control? control)
    {
        if (ReferenceEquals(Pressed, control))
        {
            return;
        }

        Pressed?.SetPressed(false);
        Pressed = control;
        control?.SetPressed(true);
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Root.VerifyMutable();
    }
}
