// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>Consumes a configured outside press and dismisses one non-modal floating surface.</summary>
/// <remarks>
/// Routed presses register at the active plane boundary containing the anchor. Presses outside that
/// plane are intercepted by modality before its older outside-dismiss policy runs. Both paths consume
/// the press, close only this younger surface, and restore focus captured before opening.
/// </remarks>
internal sealed class LightDismiss: IDisposable
{
    private readonly ControlBase? _anchor;
    private readonly Func<bool> _isOpen;
    private readonly Func<Rect> _surfaceBounds;
    private readonly Action _dismiss;
    private readonly Terminal.Input.Buttons _buttons;
    private readonly FocusManager? _focusOwner;
    private readonly ControlBase? _focusBeforeOpen;
    private readonly ModalityManager? _modality;
    private readonly ControlBase? _routeBoundary;
    private IDisposable? _registration;

    public LightDismiss(
        ControlBase surface,
        ControlBase? anchor,
        Func<bool> isOpen,
        Func<Rect> surfaceBounds,
        Action dismiss,
        Terminal.Input.Buttons buttons = Terminal.Input.Buttons.Primary,
        bool interceptAtModalBoundary = false)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(isOpen);
        ArgumentNullException.ThrowIfNull(surfaceBounds);
        ArgumentNullException.ThrowIfNull(dismiss);
        Debug.Assert(buttons != Terminal.Input.Buttons.None, "Light dismiss requires at least one button.");
        _anchor = anchor;
        _isOpen = isOpen;
        _surfaceBounds = surfaceBounds;
        _dismiss = dismiss;
        _buttons = buttons;
        _focusOwner = surface.FocusOwner;
        _focusBeforeOpen = _focusOwner?.Focused;

        var root = surface;
        while (root.Parent is { } parent)
        {
            root = parent;
        }

        _modality = surface.ModalityOwner;
        _routeBoundary = anchor is null
            ? null
            : _modality?.BoundaryFor(anchor);
        _registration = (_routeBoundary ?? root).AddHandler(Events.Pointer, OnPointer);

        if (_routeBoundary is not null || interceptAtModalBoundary)
        {
            _modality?.RegisterLightDismiss(this);
        }
    }

    public void Dispose()
    {
        _modality?.UnregisterLightDismiss(this);
        _registration?.Dispose();
        _registration = null;
    }

    /// <summary>Dismisses for one eligible press while this registration still belongs to the active plane.</summary>
    /// <param name="pointer">The decoded pointer input.</param>
    /// <returns>True when the input was consumed by this surface.</returns>
    internal bool TryDismiss(Terminal.Input.Pointer pointer)
    {
        if (_routeBoundary is not null &&
            (_anchor is null || !ReferenceEquals(_modality?.BoundaryFor(_anchor), _routeBoundary)))
        {
            return false;
        }

        if (pointer.Action != Terminal.Input.PointerAction.Press ||
            (pointer.Buttons & _buttons) == 0 ||
            !_isOpen() ||
            pointer.Cells is not { } cells ||
            _surfaceBounds().Contains(cells) ||
            (_anchor is not null && _anchor.Bounds.Contains(cells)))
        {
            return false;
        }

        DismissAndRestoreFocus();
        return true;
    }

    private void OnPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview || !TryDismiss(eventArgs.Pointer))
        {
            return;
        }

        eventArgs.IsHandled = true;
    }

    private void DismissAndRestoreFocus()
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(_dismiss, ref failure);

        if (_focusBeforeOpen is not null &&
            !_focusBeforeOpen.IsDisposed &&
            _focusBeforeOpen.Dispatcher is not null &&
            _focusBeforeOpen.EffectiveIsVisible &&
            _focusBeforeOpen.EffectiveIsEnabled)
        {
            ExceptionAggregation.Capture(() => _ = _focusOwner?.Focus(_focusBeforeOpen), ref failure);
        }

        failure?.Throw();
    }
}
