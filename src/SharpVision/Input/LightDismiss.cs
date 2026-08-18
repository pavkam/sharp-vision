// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;


/// <summary>Registers a preview pointer handler that dismisses a popup on outside press.</summary>
/// <remarks>
/// This handler answers only while no modal plane is active. It registers on the application root,
/// and <c>Router.RouteCore</c> ends a route at the active plane's boundary - so with a dialog up,
/// a press inside that dialog routes no higher than the dialog and never reaches this handler.
/// Dismissal is not lost: an open popup joins the active scope as a secondary root, so
/// <c>ModalityManager.RequestDismiss</c> closes it through <c>Popup.OnModalDismissRequested</c>
/// instead. Do not "repair" this by re-registering below the boundary or by hoisting the route -
/// both mechanisms would then fire for one press. <c>LightDismissModalityTests</c> pins each
/// mechanism in its own configuration.
/// </remarks>
internal sealed class LightDismiss: IDisposable
{
    private readonly ControlBase? _anchor;
    private readonly Func<bool> _isOpen;
    private readonly Func<Rect> _surfaceBounds;
    private readonly Action _dismiss;
    private IDisposable? _registration;

    public LightDismiss(
        ControlBase surface,
        ControlBase? anchor,
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

        if (eventArgs.Phase != RoutingPhase.Preview ||
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
