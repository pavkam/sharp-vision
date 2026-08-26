// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Composes press semantics into a control without imposing a control inheritance role.</summary>
/// <remarks>Consumers compose it; raw pointer routing never owns semantic pressed state.</remarks>
internal sealed class PressBehavior
{
    private readonly Func<Rect> _bounds;
    private readonly Func<bool> _isAvailable;
    private readonly Func<bool> _canCompleteSpace;
    private readonly Func<bool> _requestFocus;
    private readonly Func<bool> _capturePointer;
    private readonly Func<bool> _hasPointerCapture;
    private readonly Action _releasePointerCapture;
    private readonly Action<bool> _setPressed;
    private readonly Action<ActivationCause> _activate;
    private readonly Func<bool> _keyReleasesExpected;
    private bool _pointerHeld;
    private bool _spaceHeld;

    public PressBehavior(
        Func<Rect> bounds,
        Func<bool> isAvailable,
        Func<bool> canCompleteSpace,
        Func<bool> requestFocus,
        Func<bool> capturePointer,
        Func<bool> hasPointerCapture,
        Action releasePointerCapture,
        Action<bool> setPressed,
        Action<ActivationCause> activate,
        Func<bool> keyReleasesExpected)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(isAvailable);
        ArgumentNullException.ThrowIfNull(canCompleteSpace);
        ArgumentNullException.ThrowIfNull(requestFocus);
        ArgumentNullException.ThrowIfNull(capturePointer);
        ArgumentNullException.ThrowIfNull(hasPointerCapture);
        ArgumentNullException.ThrowIfNull(releasePointerCapture);
        ArgumentNullException.ThrowIfNull(setPressed);
        ArgumentNullException.ThrowIfNull(activate);
        ArgumentNullException.ThrowIfNull(keyReleasesExpected);
        _bounds = bounds;
        _isAvailable = isAvailable;
        _canCompleteSpace = canCompleteSpace;
        _requestFocus = requestFocus;
        _capturePointer = capturePointer;
        _hasPointerCapture = hasPointerCapture;
        _releasePointerCapture = releasePointerCapture;
        _setPressed = setPressed;
        _activate = activate;
        _keyReleasesExpected = keyReleasesExpected;
    }

    public void Handle(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (eventArgs.IsHandled || !_isAvailable())
        {
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            HandleKey(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            HandlePointer(pointer);
        }
    }

    public void FocusChanged(bool focused)
    {
        if (!focused)
        {
            Cancel(releaseCapture: true);
        }
    }

    public void CaptureLost() => Cancel(releaseCapture: false);

    public void Unavailable() => Cancel(releaseCapture: false);

    private void Cancel(bool releaseCapture)
    {
        _spaceHeld = false;
        _pointerHeld = false;
        _setPressed(false);
        if (releaseCapture && _hasPointerCapture())
        {
            _releasePointerCapture();
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;
        var space = stroke.Code == Code.Character && stroke.Character == new Rune(' ');
        if (space)
        {
            if (eventArgs.IsInitialKeyDown && !_spaceHeld)
            {
                // An incidental modifier (Ctrl, Alt, Super, Hyper, Meta) rides the same key and
                // must not silently commit an activation the user did not intend. Leave the
                // stroke unhandled so a shortcut bound to the modified combination still sees it.
                if (!stroke.Modifiers.IsActivationEligible())
                {
                    return;
                }

                eventArgs.IsHandled = true;
                if (!_keyReleasesExpected())
                {
                    // A press-only terminal never delivers the completing release, so arming the
                    // held state here left Space permanently latched and never activating.
                    // Behave like Enter instead: one pressed-frame pulse and an immediate
                    // completed activation on the press itself. The pressed-frame pulse must
                    // fully resolve before activation runs, since activation can synchronously
                    // dispose the control this behavior is composed into.
                    _setPressed(true);
                    _setPressed(false);
                    if (_canCompleteSpace())
                    {
                        _activate(ActivationCause.Keyboard);
                    }

                    return;
                }

                _spaceHeld = true;
                _setPressed(true);
                return;
            }

            if (eventArgs.IsKeyUp)
            {
                if (!_spaceHeld)
                {
                    // The paired press never armed this behavior - either it was gated by an
                    // incidental modifier, or it was never observed here at all. A
                    // modifier-carrying release must bubble to match its gated press the same
                    // way the press itself did, instead of being silently swallowed here; an
                    // eligible unmatched release keeps the consumed no-op behavior it has
                    // always had.
                    eventArgs.IsHandled = stroke.Modifiers.IsActivationEligible();
                    return;
                }

                // The armed hold always consumes its paired release, whether or not it goes on
                // to activate. But an incidental modifier that appears only between press and
                // release must not silently commit the activation the user did not intend -
                // gate the activation on eligibility, mirroring the press-side gate, without
                // un-consuming the stroke.
                eventArgs.IsHandled = true;
                _spaceHeld = false;
                _setPressed(false);
                if (stroke.Modifiers.IsActivationEligible() && _canCompleteSpace())
                {
                    _activate(ActivationCause.Keyboard);
                }

                return;
            }

            eventArgs.IsHandled = true;
            return;
        }

        if (stroke.Code == Code.Enter)
        {
            // The gate applies to the whole Enter pair, not just the activating press - an
            // ancestor that saw the gated press bubble past it expects the paired release to
            // bubble too, instead of being silently swallowed here.
            if (!stroke.Modifiers.IsActivationEligible())
            {
                return;
            }

            eventArgs.IsHandled = true;
            if (eventArgs.IsInitialKeyDown)
            {
                // The pressed-frame pulse must fully resolve before activation runs, since
                // activation can synchronously dispose the control this behavior is composed
                // into.
                _setPressed(true);
                _setPressed(false);
                _activate(ActivationCause.Keyboard);
            }
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Leave)
        {
            var wasHeld = _pointerHeld;
            _pointerHeld = false;
            _setPressed(false);

            if (_hasPointerCapture())
            {
                _releasePointerCapture();
            }

            eventArgs.IsHandled = wasHeld;
            return;
        }

        if (pointer.Action == PointerAction.Release && !PointerButtonTransition.IsPrimaryRelease(pointer))
        {
            return;
        }

        if ((pointer.Buttons & Buttons.Primary) == 0 && pointer.Action != PointerAction.Release)
        {
            if (pointer.Action == PointerAction.Press)
            {
                _setPressed(false);
            }

            return;
        }

        var inside = pointer.Cells is { } cells && _bounds().Contains(cells);
        if (pointer.Action == PointerAction.Press && inside)
        {
            if (!_capturePointer())
            {
                return;
            }

            _ = _requestFocus();
            _pointerHeld = true;
            _setPressed(true);
            eventArgs.IsHandled = true;
            return;
        }

        if (!_pointerHeld)
        {
            return;
        }

        _setPressed(inside);
        eventArgs.IsHandled = true;
        if (PointerButtonTransition.IsPrimaryRelease(pointer))
        {
            _pointerHeld = false;
            if (_hasPointerCapture())
            {
                _releasePointerCapture();
            }

            _setPressed(false);
            if (inside)
            {
                _activate(ActivationCause.Pointer);
            }
        }
    }
}
