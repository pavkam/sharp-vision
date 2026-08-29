// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

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
    private readonly ModalSession _session;
    private ControlBase? _owner;
    private ControlBase? _ownerInitialFocus;
    private ControlBase? _availabilityAncestor;

    /// <summary>Initializes a tracker for one popup with a close callback invoked on dismiss.</summary>
    /// <param name="popup">The popup whose <see cref="Popup.IsOpen"/> state is tracked.</param>
    /// <param name="close">Called when the modal scope requests dismissal (typically sets IsOpen = false).</param>
    public PopupModalTracker(Popup popup, Action close)
    {
        _popup = popup;
        _close = close;
        _session = new ModalSession(OnDismissRequested, OnExited);
        _popup.PropertyChanged += OnPopupPropertyChanged;
    }

    /// <summary>Enters a dismiss-mode modal scope on the owner's modality manager.</summary>
    /// <param name="owner">The control that owns the popup and serves as modal root, and the
    /// fallback initial focus when nothing more specific already holds it.</param>
    /// <param name="ownerInitialFocus">The optional focusable retained descendant used instead
    /// of a non-focusable composite owner.</param>
    public void Enter(ControlBase owner, ControlBase? ownerInitialFocus = null)
    {
        _owner = owner;
        _ownerInitialFocus = ownerInitialFocus;

        if (_session.IsActive || owner.ModalityOwner is not { } modality)
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

        _ = _session.Enter(
            () => modality.Enter(owner, OutsideInteraction.Dismiss, initialFocus),
            () => _popup.IsOpen &&
                owner.EffectiveIsEnabled &&
                owner.EffectiveIsVisible &&
                ReferenceEquals(owner.ModalityOwner, modality),
            RollbackOpenState);
    }

    /// <summary>Exits and clears the tracked modal scope if one is active.</summary>
    public void Exit()
    {
        StopAwaitingAvailability();

        _session.Exit();
    }

    /// <summary>Releases the tracker subscriptions when its composite owner is disposed.</summary>
    public void Detach()
    {
        StopAwaitingAvailability();
        _popup.PropertyChanged -= OnPopupPropertyChanged;
        _owner = null;
        _ownerInitialFocus = null;
    }

    private void OnDismissRequested(ModalScope scope)
    {
        if (scope.IsActive &&
            _popup.IsOpen)
        {
            _close();
        }
    }

    private void OnExited(ModalScope scope)
    {
        _ = scope;

        var preserveOpen = _popup.ModalityOwner?.IsUnavailable(_popup) == true;

        if (!preserveOpen && _popup.IsOpen)
        {
            _popup.IsOpen = false;
        }
        else if (preserveOpen && _popup.IsOpen)
        {
            AwaitAvailability();
        }
    }

    private void RollbackOpenState()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
        }
    }

    private void AwaitAvailability()
    {
        var unavailable = FindUnavailableAncestor();

        if (ReferenceEquals(_availabilityAncestor, unavailable))
        {
            return;
        }

        StopAwaitingAvailability();

        if (unavailable is not null)
        {
            _availabilityAncestor = unavailable;
            unavailable.PropertyChanged += OnAvailabilityAncestorPropertyChanged;
            return;
        }

        if (_popup.IsOpen && _owner is { EffectiveIsEnabled: true, EffectiveIsVisible: true } owner)
        {
            Enter(owner, _ownerInitialFocus);
        }
    }

    [Pure]
    private ControlBase? FindUnavailableAncestor()
    {
        for (var current = _popup.Parent; current is not null; current = current.Parent)
        {
            if (current.Visibility != Visibility.Visible || !current.IsEnabled)
            {
                return current;
            }
        }

        return null;
    }

    private void OnAvailabilityAncestorPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(ControlBase.IsEnabled) or nameof(ControlBase.Visibility))
        {
            AwaitAvailability();
        }
    }

    private void OnPopupPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(Popup.IsOpen) && !_popup.IsOpen)
        {
            StopAwaitingAvailability();
        }
    }

    private void StopAwaitingAvailability()
    {
        if (_availabilityAncestor is not { } ancestor)
        {
            return;
        }

        _availabilityAncestor = null;
        ancestor.PropertyChanged -= OnAvailabilityAncestorPropertyChanged;
    }

}
