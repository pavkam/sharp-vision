// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Coordinates Space press completion for one-focus item owners whose selected face is
/// retained below the focus owner.</summary>
/// <remarks>
/// The exact selected target is captured for an authoritative press/release pair. A press-only
/// terminal instead receives a complete pressed-state pulse and immediate activation because it
/// cannot deliver the release that would otherwise finish the gesture.
/// </remarks>
internal sealed class SelectedItemPressBehavior
{
    private readonly Func<ControlBase?> _getSelectedTarget;
    private readonly Func<ControlBase, bool> _isTargetAvailable;
    private readonly Action<ControlBase, bool> _setTargetPressed;
    private readonly Action<ControlBase> _activateTarget;
    private readonly Func<bool> _keyReleasesExpected;
    private readonly bool _consumeWhenNoTarget;
    private ControlBase? _heldTarget;

    /// <summary>Initializes a selected-face Space interaction.</summary>
    /// <param name="getSelectedTarget">Returns the current selected target, or null.</param>
    /// <param name="isTargetAvailable">Reports whether one captured target can still activate.</param>
    /// <param name="setTargetPressed">Commits the captured target's pressed presentation.</param>
    /// <param name="activateTarget">Activates one still-current captured target.</param>
    /// <param name="keyReleasesExpected">Reports whether an authoritative release will arrive.</param>
    /// <param name="consumeWhenNoTarget">Whether an eligible Space with no target is consumed.</param>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public SelectedItemPressBehavior(
        Func<ControlBase?> getSelectedTarget,
        Func<ControlBase, bool> isTargetAvailable,
        Action<ControlBase, bool> setTargetPressed,
        Action<ControlBase> activateTarget,
        Func<bool> keyReleasesExpected,
        bool consumeWhenNoTarget)
    {
        ArgumentNullException.ThrowIfNull(getSelectedTarget);
        ArgumentNullException.ThrowIfNull(isTargetAvailable);
        ArgumentNullException.ThrowIfNull(setTargetPressed);
        ArgumentNullException.ThrowIfNull(activateTarget);
        ArgumentNullException.ThrowIfNull(keyReleasesExpected);
        _getSelectedTarget = getSelectedTarget;
        _isTargetAvailable = isTargetAvailable;
        _setTargetPressed = setTargetPressed;
        _activateTarget = activateTarget;
        _keyReleasesExpected = keyReleasesExpected;
        _consumeWhenNoTarget = consumeWhenNoTarget;
    }

    /// <summary>Routes one key event through the selected-target Space state machine.</summary>
    /// <param name="eventArgs">The non-null routed key event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    public void Handle(KeyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var stroke = eventArgs.Stroke;

        if (stroke.Code != Code.Character || stroke.Character != new Rune(' '))
        {
            return;
        }

        if (eventArgs.IsInitialKeyDown)
        {
            HandleInitialPress(eventArgs);
            return;
        }

        if (eventArgs.IsKeyUp)
        {
            CompleteRelease(eventArgs);
            return;
        }

        eventArgs.IsHandled = _heldTarget is not null;
    }

    /// <summary>Clears a held target without activating it.</summary>
    public void Cancel()
    {
        var target = _heldTarget;
        _heldTarget = null;

        if (target is not null && !target.IsDisposing && !target.IsDisposed)
        {
            _setTargetPressed(target, false);
        }
    }

    /// <summary>Forgets a captured target removed from its owning item host.</summary>
    /// <param name="removed">The immutable removed-control snapshot.</param>
    public void ReconcileRemoved(ReadOnlySpan<ControlBase> removed)
    {
        if (_heldTarget is not { } held)
        {
            return;
        }

        foreach (var target in removed)
        {
            if (ReferenceEquals(target, held))
            {
                Cancel();
                return;
            }
        }
    }

    private void HandleInitialPress(KeyEventArgs eventArgs)
    {
        if (!eventArgs.Stroke.Modifiers.IsActivationEligible())
        {
            return;
        }

        var target = _getSelectedTarget();

        if (target is null || !_isTargetAvailable(target))
        {
            eventArgs.IsHandled = _consumeWhenNoTarget;
            return;
        }

        eventArgs.IsHandled = true;
        _heldTarget = target;
        _setTargetPressed(target, true);

        if (_keyReleasesExpected())
        {
            return;
        }

        // The pulse completes before activation because a handler may synchronously dispose the
        // target or mutate selection. Re-read identity and availability after every publication.
        Cancel();
        if (ReferenceEquals(_getSelectedTarget(), target) && _isTargetAvailable(target))
        {
            _activateTarget(target);
        }
    }

    private void CompleteRelease(KeyEventArgs eventArgs)
    {
        var target = _heldTarget;

        if (target is null)
        {
            eventArgs.IsHandled = eventArgs.Stroke.Modifiers.IsActivationEligible();
            return;
        }

        eventArgs.IsHandled = true;
        Cancel();

        if (eventArgs.Stroke.Modifiers.IsActivationEligible() &&
            ReferenceEquals(_getSelectedTarget(), target) &&
            _isTargetAvailable(target))
        {
            _activateTarget(target);
        }
    }
}
