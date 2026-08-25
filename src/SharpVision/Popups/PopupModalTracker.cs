// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using System.Runtime.ExceptionServices;

/// <summary>
/// Manages the modal scope lifetime for a control that owns a private popup.
/// Replaces the duplicated EnterModalScope/ExitModalScope/OnModalDismissRequested/
/// OnModalScopeExited/ClearModalScope pattern used by ComboBox, DateInput, and DateTimeInput.
/// </summary>
/// <remarks>
/// This tracker owns only the composite owner's modal policy: its popup uses
/// <see cref="PopupModalBehavior.None"/>, while the scope is rooted at the public owner.
/// Popup-owned surface modality remains exclusively in <see cref="Surfaces.FloatingSurfaceBase"/>.
/// </remarks>
internal sealed class PopupModalTracker
{
    private readonly Popup _popup;
    private readonly Action _close;
    private ModalScope? _scope;

    /// <summary>Initializes a tracker for one popup with a close callback invoked on dismiss.</summary>
    /// <param name="popup">The popup whose <see cref="Popup.IsOpen"/> state is tracked.</param>
    /// <param name="close">Called when the modal scope requests dismissal (typically sets IsOpen = false).</param>
    public PopupModalTracker(Popup popup, Action close)
    {
        _popup = popup;
        _close = close;
    }

    /// <summary>Enters a dismiss-mode modal scope on the owner's modality manager.</summary>
    /// <param name="owner">The control that owns the popup and serves as modal root, and the
    /// fallback initial focus when nothing more specific already holds it.</param>
    /// <param name="ownerInitialFocus">The optional focusable retained descendant used instead
    /// of a non-focusable composite owner.</param>
    public void Enter(ControlBase owner, ControlBase? ownerInitialFocus = null)
    {
        if (_scope?.IsActive == true || owner.ModalityOwner is not { } modality)
        {
            return;
        }

        // Popup.IsOpen (set just before this call in PopupDropDownCoordinator.Open) already ran
        // Popup.FocusOnOpen synchronously, which - for a control such as DateInput that requests
        // it - moves focus onto the popup's own content (its Calendar) before this method ever
        // runs. Passing owner here unconditionally would have ModalityManager.Enter re-commit
        // focus to owner regardless, silently discarding what FocusOnOpen just did and leaving
        // the popup content never genuinely focused for the rest of the time it stays open.
        // Preserving an already-focused descendant of the popup's own content keeps FocusOnOpen
        // meaningful while leaving every FocusOnOpen: false control (ComboBox, DateTimeInput)
        // exactly as before, since nothing there is ever focused at this point.
        var initialFocus = _popup.Content is { } content &&
            owner.FocusOwner?.Focused is { } focused &&
            ControlBase.ContainsFocused(content)
            ? focused
            : ownerInitialFocus ?? owner;

        ModalScope? scope = null;

        try
        {
            scope = modality.Enter(owner, OutsideInteraction.Dismiss, initialFocus);

            if (!scope.IsActive || !_popup.IsOpen)
            {
                if (scope.IsActive)
                {
                    scope.Dispose();
                }

                return;
            }

            _scope = scope;
            scope.DismissRequested += OnDismissRequested;
            scope.Exited += OnExited;
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);

            if (scope?.IsActive == true)
            {
                try
                {
                    scope.Dispose();
                }
                catch
                {
                    // The initiating entry failure remains authoritative.
                }
            }

            if (_popup.IsOpen)
            {
                try
                {
                    _popup.IsOpen = false;
                }
                catch
                {
                    // Visual rollback cannot replace the initiating entry failure.
                }
            }

            failure.Throw();
        }
    }

    /// <summary>Exits and clears the tracked modal scope if one is active.</summary>
    public void Exit()
    {
        if (_scope is not { } scope)
        {
            return;
        }

        if (scope.IsActive)
        {
            scope.Dispose();
        }

        Clear(scope);
    }

    private void OnDismissRequested(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope &&
            ReferenceEquals(_scope, scope) &&
            scope.IsActive &&
            _popup.IsOpen)
        {
            _close();
        }
    }

    private void OnExited(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is not ModalScope scope || !ReferenceEquals(_scope, scope))
        {
            return;
        }

        Clear(scope);

        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
        }
    }

    private void Clear(ModalScope scope)
    {
        if (!ReferenceEquals(_scope, scope))
        {
            return;
        }

        _scope = null;
        scope.DismissRequested -= OnDismissRequested;
        scope.Exited -= OnExited;
    }
}
