// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Coordinates reusable keyboard, pointer, capture, and pressed-state transitions.</summary>
/// <remarks>
/// The behavior owns only transient input state. Its owner supplies every control-tree operation,
/// allowing unrelated control roles to compose the same interaction without sharing inheritance.
/// </remarks>
internal sealed class PressInteraction
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

    /// <summary>Initializes one owner-bound press interaction.</summary>
    /// <param name="bounds">Returns the owner's current absolute arranged bounds.</param>
    /// <param name="isAvailable">Returns whether the owner accepts interaction.</param>
    /// <param name="canCompleteSpace">Returns whether a held Space release remains eligible.</param>
    /// <param name="requestFocus">Requests focus for the owner.</param>
    /// <param name="capturePointer">Requests pointer capture for the owner.</param>
    /// <param name="hasPointerCapture">Returns whether the owner currently holds capture.</param>
    /// <param name="releasePointerCapture">Releases capture held by the owner.</param>
    /// <param name="setPressed">Commits the owner's pressed visual state.</param>
    /// <param name="activate">Completes one semantic activation.</param>
    /// <exception cref="ArgumentNullException">Any callback is null.</exception>
    internal PressInteraction(
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

    /// <summary>Handles one routed key or pointer event through the shared state machine.</summary>
    /// <param name="eventArgs">The non-null routed event payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    internal void Handle(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!_isAvailable())
        {
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }
    }

    /// <summary>Cancels held state after the owner loses keyboard focus.</summary>
    /// <param name="focused">Whether the owner now holds keyboard focus.</param>
    internal void FocusChanged(bool focused)
    {
        if (!focused)
        {
            Cancel(releaseCapture: true);
        }
    }

    /// <summary>Cancels held state after manager-owned capture cancellation commits.</summary>
    internal void CaptureCancelled() => Cancel(releaseCapture: false);

    /// <summary>Cancels held state while the owner becomes unavailable.</summary>
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

    private void Handle(KeyEventArgs eventArgs)
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

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

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
