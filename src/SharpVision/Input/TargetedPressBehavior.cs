// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Composes press semantics for an owner whose face renders several independently
/// pressable sub-targets - a breadcrumb path's items and overflow trigger, a pager's page slots -
/// on top of the same pointer/keyboard state machine <see cref="PressBehavior"/> already runs for
/// a single whole-control target.</summary>
/// <typeparam name="TTarget">The pressable target type. May be a reference type or a value
/// type.</typeparam>
/// <remarks>
/// A primary pointer press resolves which target was hit before the composed
/// <see cref="PressBehavior"/> processes the same event, so that behavior's bounds, availability,
/// pressed-visual, and activation callbacks all close over the target resolved at press time
/// rather than re-resolving it on every subsequent move or release. The resolved target and
/// whether one is currently held are tracked as two separate fields rather than a single
/// <c>TTarget?</c> field - for an open type parameter constrained only by
/// <see langword="notnull"/>, C# erases <c>TTarget?</c> to plain <c>TTarget</c> rather than
/// <see cref="Nullable{T}"/> when <c>TTarget</c> is later instantiated with a value type, so a
/// value-typed target would otherwise never compare equal to "no target" once cleared. The
/// resolved target is retained only for the duration of one press gesture: it is cleared once the
/// composed behavior has processed a release or a pointer leave, and on every lifecycle
/// cancellation (direct focus loss, pointer-capture loss, or the owner becoming unavailable).
/// Keyboard activation is not handled here - every current owner routes Enter/Space itself and
/// only feeds pointer events through this behavior.
/// </remarks>
internal sealed class TargetedPressBehavior<TTarget>: IPressActivationBehavior
    where TTarget : notnull
{
    private readonly TargetHitTest<TTarget> _hitTarget;
    private readonly PressBehavior _inner;
    private bool _handlingEvent;
    private bool _hasPressed;
    private TTarget _pressed;

    /// <summary>Initializes the composed state machine from the owner's target-hit, target-state,
    /// and control-level interaction delegates.</summary>
    /// <param name="hitTarget">Resolves the target under a pointer location.</param>
    /// <param name="targetBounds">Resolves the rectangle a resolved target is pressed and released
    /// against.</param>
    /// <param name="isTargetCurrent">Reports whether a previously resolved target is still a valid
    /// press target - for example, the owner's layout has not since regenerated in a way that
    /// invalidates it.</param>
    /// <param name="setTargetPressed">Commits the pressed appearance for a resolved target.</param>
    /// <param name="activateTarget">Runs the completed activation for a resolved target.</param>
    /// <param name="isAvailable">Reports whether press interaction can currently start or
    /// continue, independent of any resolved target.</param>
    /// <param name="requestFocus">Requests focus for the interaction.</param>
    /// <param name="capturePointer">Attempts to acquire pointer capture for the interaction.</param>
    /// <param name="hasPointerCapture">Reports whether the owner currently holds pointer
    /// capture.</param>
    /// <param name="releasePointerCapture">Releases pointer capture the owner currently holds.</param>
    /// <param name="keyReleasesExpected">Reports whether the active terminal delivers key-release
    /// events authoritatively.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public TargetedPressBehavior(
        TargetHitTest<TTarget> hitTarget,
        Func<TTarget, Rect> targetBounds,
        Func<TTarget, bool> isTargetCurrent,
        Action<TTarget, bool> setTargetPressed,
        Action<TTarget, ActivationCause> activateTarget,
        Func<bool> isAvailable,
        Func<bool> requestFocus,
        Func<bool> capturePointer,
        Func<bool> hasPointerCapture,
        Action releasePointerCapture,
        Func<bool> keyReleasesExpected)
    {
        ArgumentNullException.ThrowIfNull(hitTarget);
        ArgumentNullException.ThrowIfNull(targetBounds);
        ArgumentNullException.ThrowIfNull(isTargetCurrent);
        ArgumentNullException.ThrowIfNull(setTargetPressed);
        ArgumentNullException.ThrowIfNull(activateTarget);
        ArgumentNullException.ThrowIfNull(isAvailable);
        ArgumentNullException.ThrowIfNull(requestFocus);
        ArgumentNullException.ThrowIfNull(capturePointer);
        ArgumentNullException.ThrowIfNull(hasPointerCapture);
        ArgumentNullException.ThrowIfNull(releasePointerCapture);
        ArgumentNullException.ThrowIfNull(keyReleasesExpected);

        _hitTarget = hitTarget;
        _pressed = default!;
        _inner = new PressBehavior(
            () => _hasPressed && isTargetCurrent(_pressed) ? targetBounds(_pressed) : default,
            () => isAvailable() && _hasPressed && isTargetCurrent(_pressed),
            static () => false,
            requestFocus,
            capturePointer,
            hasPointerCapture,
            releasePointerCapture,
            v =>
            {
                if (_hasPressed)
                {
                    setTargetPressed(_pressed, v);
                }
            },
            cause =>
            {
                if (_hasPressed && isTargetCurrent(_pressed))
                {
                    activateTarget(_pressed, cause);
                }
            },
            keyReleasesExpected);
    }

    /// <inheritdoc/>
    public void Handle(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs { Pointer: { Action: PointerAction.Press, Cells: { } cells } pointer } &&
            (pointer.Buttons & Buttons.Primary) != 0)
        {
            _hasPressed = _hitTarget(cells, out _pressed);
        }

        // Releasing pointer capture or requesting focus from inside the composed PressBehavior can
        // synchronously fan a focus-loss or capture-loss notification back out to every registered
        // lifecycle participant, including this one, while that same PressBehavior call is still
        // running - for example, completing a release cycles capture in a way that reports a
        // transient focus loss before the gesture's own activation check runs. Marking this call
        // in progress lets FocusChanged/CaptureLost/Unavailable recognize such a reentrant report
        // and defer clearing the resolved target to this method's own post-processing below, which
        // runs once the full gesture - including activation - has settled, instead of letting a
        // side effect of completing the gesture erase the very target the gesture is completing.
        _handlingEvent = true;

        try
        {
            _inner.Handle(eventArgs);
        }
        finally
        {
            _handlingEvent = false;
        }

        if (eventArgs is PointerEventArgs { Pointer: { } settled } &&
            (settled.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(settled)))
        {
            ClearPressed();
        }
    }

    /// <inheritdoc/>
    public void FocusChanged(bool focused)
    {
        _inner.FocusChanged(focused);

        if (!focused && !_handlingEvent)
        {
            ClearPressed();
        }
    }

    /// <inheritdoc/>
    public void Unavailable()
    {
        _inner.Unavailable();

        if (!_handlingEvent)
        {
            ClearPressed();
        }
    }

    /// <inheritdoc/>
    public object? PressedTarget => _hasPressed ? _pressed : null;

    void IControlLifecycleParticipant.CaptureLost(PointerCaptureLossReason reason)
    {
        _ = reason;
        _inner.CaptureLost();

        if (!_handlingEvent)
        {
            ClearPressed();
        }
    }

    void IControlLifecycleParticipant.Unavailable(ReleaseReason reason)
    {
        _ = reason;
        Unavailable();
    }

    private void ClearPressed()
    {
        _hasPressed = false;
        _pressed = default!;
    }
}
