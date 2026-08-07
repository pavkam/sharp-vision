// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Composable drag interaction state machine for pointer-driven value editing.</summary>
internal sealed class DragBehavior
{
    private readonly Func<Rect> _contentBounds;
    private readonly Func<bool> _isAvailable;
    private readonly Func<bool> _requestFocus;
    private readonly Func<bool> _capturePointer;
    private readonly Func<bool> _hasCapture;
    private readonly Action _releaseCapture;
    private readonly Action<bool> _setPressed;

    public DragBehavior(
        Func<Rect> contentBounds,
        Func<bool> isAvailable,
        Func<bool> requestFocus,
        Func<bool> capturePointer,
        Func<bool> hasCapture,
        Action releaseCapture,
        Action<bool> setPressed)
    {
        _contentBounds = contentBounds;
        _isAvailable = isAvailable;
        _requestFocus = requestFocus;
        _capturePointer = capturePointer;
        _hasCapture = hasCapture;
        _releaseCapture = releaseCapture;
        _setPressed = setPressed;
    }

    public bool Dragging { get; private set; }

    /// <summary>Attempts to start a drag from a pointer press event. Returns true if drag started.</summary>
    public bool TryStart(Point cells)
    {
        if (!_isAvailable() || !_contentBounds().Contains(cells))
        {
            return false;
        }

        _ = _requestFocus();

        if (_capturePointer())
        {
            Dragging = true;
            _setPressed(true);
            return true;
        }

        return false;
    }

    /// <summary>Cancels an active drag.</summary>
    public void Cancel(bool releaseCapture)
    {
        Dragging = false;
        _setPressed(false);

        if (releaseCapture && _hasCapture())
        {
            _releaseCapture();
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
}
