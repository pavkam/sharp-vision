// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;


/// <summary>Registers a preview pointer handler that dismisses a popup on outside press.</summary>
internal sealed class LightDismiss: IDisposable
{
    private readonly Control? _anchor;
    private readonly Func<bool> _isOpen;
    private readonly Func<Rect> _surfaceBounds;
    private readonly Action _dismiss;
    private IDisposable? _registration;

    public LightDismiss(
        Control surface,
        Control? anchor,
        Func<bool> isOpen,
        Func<Rect> surfaceBounds,
        Action dismiss)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(isOpen);
        ArgumentNullException.ThrowIfNull(surfaceBounds);
        ArgumentNullException.ThrowIfNull(dismiss);
        _anchor = anchor;
        _isOpen = isOpen;
        _surfaceBounds = surfaceBounds;
        _dismiss = dismiss;

        var root = surface;
        while (root.Parent is { } parent)
        {
            root = parent;
        }

        _registration = root.AddHandler(Events.Pointer, OnPointer);
    }

    public void Dispose()
    {
        _registration?.Dispose();
        _registration = null;
    }

    private void OnPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview ||
            eventArgs.Pointer.Action != Terminal.Input.PointerAction.Press ||
            !_isOpen())
        {
            return;
        }

        if (eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        if (_surfaceBounds().Contains(cells))
        {
            return;
        }

        if (_anchor is not null && _anchor.Bounds.Contains(cells))
        {
            return;
        }

        _dismiss();
    }
}
