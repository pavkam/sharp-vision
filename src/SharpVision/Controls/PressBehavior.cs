// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

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
    private readonly System.Action _releasePointerCapture;
    private readonly Action<bool> _setPressed;
    private readonly Action<ActivationCause> _activate;
    private bool _pointerHeld;
    private bool _spaceHeld;

    internal PressBehavior(
        Func<Rect> bounds,
        Func<bool> isAvailable,
        Func<bool> canCompleteSpace,
        Func<bool> requestFocus,
        Func<bool> capturePointer,
        Func<bool> hasPointerCapture,
        System.Action releasePointerCapture,
        Action<bool> setPressed,
        Action<ActivationCause> activate)
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
        _bounds = bounds;
        _isAvailable = isAvailable;
        _canCompleteSpace = canCompleteSpace;
        _requestFocus = requestFocus;
        _capturePointer = capturePointer;
        _hasPointerCapture = hasPointerCapture;
        _releasePointerCapture = releasePointerCapture;
        _setPressed = setPressed;
        _activate = activate;
    }

    internal void Handle(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (!_isAvailable())
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

    internal void FocusChanged(bool focused)
    {
        if (!focused)
        {
            Cancel(releaseCapture: true);
        }
    }

    internal void CaptureLost() => Cancel(releaseCapture: false);

    internal void Unavailable() => Cancel(releaseCapture: false);

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
            eventArgs.Handled = true;
            if (stroke.Action == KeyAction.Press && !_spaceHeld)
            {
                _spaceHeld = true;
                _setPressed(true);
            }
            else if (stroke.Action == KeyAction.Release && _spaceHeld)
            {
                _spaceHeld = false;
                _setPressed(false);
                if (_canCompleteSpace())
                {
                    _activate(ActivationCause.Keyboard);
                }
            }

            return;
        }

        if (stroke.Code == Code.Enter)
        {
            eventArgs.Handled = true;
            if (stroke.Action == KeyAction.Press)
            {
                _setPressed(true);
                _activate(ActivationCause.Keyboard);
                _setPressed(false);
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

            eventArgs.Handled = wasHeld;
            return;
        }

        if ((pointer.Buttons & Buttons.Primary) == 0)
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
            eventArgs.Handled = true;
            return;
        }

        if (!_pointerHeld)
        {
            return;
        }

        _setPressed(inside);
        eventArgs.Handled = true;
        if (pointer.Action == PointerAction.Release)
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
