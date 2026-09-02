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
    private readonly ControlBase _surface;
    private IDisposable? _registration;
    private ControlBase? _focusDisplacedByPointer;

    /// <summary>Registers one outside-press dismissal handler for an already committed surface.</summary>
    /// <param name="surface">The attached surface that owns the registration.</param>
    /// <param name="anchor">The optional control treated as inside the surface.</param>
    /// <param name="isOpen">Reports whether dismissal remains eligible.</param>
    /// <param name="surfaceBounds">Returns the current surface bounds.</param>
    /// <param name="dismiss">Requests family-specific closure.</param>
    /// <param name="focusBeforeOpen">The focus identity captured before the surface opened.</param>
    /// <param name="buttons">The non-empty set of dismissing pointer buttons.</param>
    /// <param name="interceptAtModalBoundary">Whether dismissal participates at a modal boundary.</param>
    public LightDismiss(
        ControlBase surface,
        ControlBase? anchor,
        Func<bool> isOpen,
        Func<Rect> surfaceBounds,
        Action dismiss,
        ControlBase? focusBeforeOpen,
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
        _focusBeforeOpen = focusBeforeOpen;
        _surface = surface;

        if (_focusOwner is { } focusOwner)
        {
            focusOwner.Lost += OnFocusLost;
        }

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
        _focusDisplacedByPointer = null;

        if (_focusOwner is { } focusOwner)
        {
            focusOwner.Lost -= OnFocusLost;
        }
    }

    private void OnFocusLost(object? sender, FocusChangedEventArgs eventArgs)
    {
        _ = sender;

        // The pointer manager moves focus toward the pressed background control before the press
        // is routed, so by the time an outside press reaches this handler the surface has already
        // lost whatever focus it owned. Remember exactly that displaced control (and nothing
        // else): a vetoed dismiss has to hand focus back to it, since the surface remains open.
        _focusDisplacedByPointer =
            eventArgs.Reason == FocusReason.Pointer &&
            eventArgs.Previous is { } previous &&
            ModalityManager.IsWithin(previous, _surface) &&
            !ModalityManager.IsWithin(eventArgs.Current, _surface)
                ? previous
                : null;
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

        var displaced = _focusDisplacedByPointer;
        _focusDisplacedByPointer = null;

        // The dismiss request is vetoable (a CloseRequested handler may cancel it). A surface that
        // is still open after the request must keep the focus it owned before the press displaced
        // it; restoring the pre-open focus here would instead pull focus out from under a
        // presentation the owner just refused to close. The outside press itself stays consumed
        // either way, exactly as an ignored or vetoed modal dismissal never replays to the
        // background.
        var restoreTarget = _isOpen() ? displaced : _focusBeforeOpen;

        if (restoreTarget is not null &&
            !restoreTarget.IsDisposed &&
            restoreTarget.Dispatcher is not null &&
            restoreTarget.EffectiveIsVisible &&
            restoreTarget.EffectiveIsEnabled)
        {
            ExceptionAggregation.Capture(() => _ = _focusOwner?.Focus(restoreTarget), ref failure);
        }

        failure?.Throw();
    }
}
