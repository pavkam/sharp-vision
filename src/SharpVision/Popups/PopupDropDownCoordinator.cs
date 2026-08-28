// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>
/// Owns the private-popup open/close lifecycle shared by every composite drop-down field
/// (<see cref="Controls.Input.ComboBox"/>, <see cref="Controls.Input.DateInput"/>,
/// <see cref="Controls.Input.DateTimeInput"/>). Replaces the formerly triplicated
/// Opened/OpenDropDown/CloseDropDown/OnPopupOpened/OnPopupClosing/OnPopupClosed group that each of
/// those controls hand-rolled around its own private <see cref="PopupModalTracker"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Close-path reentrancy.</strong> The close path calls <see cref="PopupModalTracker.Exit"/>
/// before assigning <c>Popup.IsOpen = false</c>, and that order is load-bearing, not incidental.
/// <see cref="PopupModalTracker.Exit"/> can synchronously flip the popup's <c>IsOpen</c> to false
/// itself, from inside <c>PopupModalTracker.OnExited</c>, once the modal scope's own unwind
/// completes - which fires the popup's Closing/Closed events, and therefore this coordinator's own
/// handlers, before the explicit assignment below ever runs. By the time control returns here, the
/// explicit <c>Popup.IsOpen = false</c> is already a no-op. Reordering it ahead of
/// <see cref="PopupModalTracker.Exit"/> would instead fire Closing/Closed once from the explicit
/// assignment and then find the modal scope already gone by the time <c>Exit</c> runs - the same
/// event, but sourced from the wrong side of the reentrancy and liable to drift out of step with
/// the open path's own ordering. The statement order here must be preserved exactly.
/// </para>
/// <para>
/// <strong>A failed modal entry skips DropDownOpened.</strong> The open path calls
/// <see cref="PopupModalTracker.Enter"/> after flipping the popup open but before raising
/// DropDownOpened. <see cref="PopupModalTracker.Enter"/> force-closes the popup and rethrows when
/// the underlying <see cref="ModalityManager"/> entry itself fails (for example an owner that
/// is not an eligible modal root), so that failure propagates straight out of the open path and
/// DropDownOpened is never raised for a drop-down that never actually finished opening.
/// </para>
/// </remarks>
internal sealed class PopupDropDownCoordinator
{
    private readonly ControlBase _owner;
    private readonly Popup _popup;
    private readonly ControlBase _focusScopeContent;
    private readonly Func<bool> _requestFocus;
    private readonly Action _raiseIsOpenPropertyChanged;
    private readonly Action _raiseDropDownOpened;
    private readonly Action _raiseDropDownClosed;
    private readonly Action? _beforeOpen;
    private readonly Action? _beforeCloseFocusRestore;
    private readonly ControlBase? _ownerInitialFocus;
    private readonly PopupModalTracker _modalTracker;

    /// <summary>Initializes a coordinator for one owner's private popup.</summary>
    /// <param name="owner">The composite control that owns <paramref name="popup"/> and serves as the modal root.</param>
    /// <param name="popup">The owned popup whose open state this coordinator drives.</param>
    /// <param name="focusScopeContent">The popup's content control searched for focus when the popup closes.</param>
    /// <param name="requestFocus">Requests focus back on <paramref name="owner"/>; typically its protected RequestFocus seam.</param>
    /// <param name="raiseIsOpenPropertyChanged">Publishes the owner's IsOpen PropertyChanged notification.</param>
    /// <param name="raiseDropDownOpened">Raises the owner's public DropDownOpened event.</param>
    /// <param name="raiseDropDownClosed">Raises the owner's public DropDownClosed event.</param>
    /// <param name="beforeOpen">Optional owner-specific work run before the popup opens, such as seeding a value or syncing a calendar.</param>
    /// <param name="beforeCloseFocusRestore">Optional owner-specific work run before the closing focus-restore check, such as discarding type-ahead state.</param>
    /// <param name="ownerInitialFocus">Optional focusable retained descendant used when the
    /// public composite owner is not itself focusable.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public PopupDropDownCoordinator(
        ControlBase owner,
        Popup popup,
        ControlBase focusScopeContent,
        Func<bool> requestFocus,
        Action raiseIsOpenPropertyChanged,
        Action raiseDropDownOpened,
        Action raiseDropDownClosed,
        Action? beforeOpen = null,
        Action? beforeCloseFocusRestore = null,
        ControlBase? ownerInitialFocus = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(popup);
        ArgumentNullException.ThrowIfNull(focusScopeContent);
        ArgumentNullException.ThrowIfNull(requestFocus);
        ArgumentNullException.ThrowIfNull(raiseIsOpenPropertyChanged);
        ArgumentNullException.ThrowIfNull(raiseDropDownOpened);
        ArgumentNullException.ThrowIfNull(raiseDropDownClosed);

        _owner = owner;
        _popup = popup;
        _focusScopeContent = focusScopeContent;
        _requestFocus = requestFocus;
        _raiseIsOpenPropertyChanged = raiseIsOpenPropertyChanged;
        _raiseDropDownOpened = raiseDropDownOpened;
        _raiseDropDownClosed = raiseDropDownClosed;
        _beforeOpen = beforeOpen;
        _beforeCloseFocusRestore = beforeCloseFocusRestore;
        _ownerInitialFocus = ownerInitialFocus;

        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => SetOpen(false));
    }

    /// <summary>Gets whether the owned popup is currently open.</summary>
    public bool IsOpen => _popup.IsOpen;

    /// <summary>Gets the monotonically increasing public or internal open-state request version.</summary>
    internal ulong TransitionVersion { get; private set; }

    /// <summary>Opens or closes the owned popup, no-op when already at the requested state.</summary>
    /// <param name="value">True to open the popup; false to close it.</param>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public void SetOpen(bool value)
    {
        _owner.VerifyMutable();
        TransitionVersion++;

        if (_popup.IsOpen != value)
        {
            if (value)
            {
                Open();
            }
            else
            {
                Close();
            }
        }
    }

    /// <summary>Re-enters the modal scope for an already-open popup once the owner becomes attached.</summary>
    /// <remarks>An owner constructed and opened before attachment defers modal entry until a
    /// dispatcher and modality manager actually exist to enter.</remarks>
    public void OnOwnerAttached()
    {
        if (_popup.IsOpen)
        {
            _modalTracker.Enter(_owner, _ownerInitialFocus);
        }
    }

    /// <summary>Unsubscribes from the owned popup's lifecycle events. Called from the owner's disposal path.</summary>
    public void Detach()
    {
        _modalTracker.Detach();
        _popup.Opened -= OnPopupOpened;
        _popup.Closing -= OnPopupClosing;
        _popup.Closed -= OnPopupClosed;
    }

    private void Open()
    {
        _beforeOpen?.Invoke();
        _popup.IsOpen = true;
        _modalTracker.Enter(_owner, _ownerInitialFocus);
        _raiseDropDownOpened();
    }

    private void Close()
    {
        // See the type remarks: Exit() can synchronously close the popup before the explicit
        // assignment below runs, making that assignment a no-op. Do not reorder these statements.
        _modalTracker.Exit();
        _popup.IsOpen = false;
        _raiseDropDownClosed();
    }

    private void OnPopupOpened(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _raiseIsOpenPropertyChanged();
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _beforeCloseFocusRestore?.Invoke();

        if (ControlBase.ContainsFocused(_focusScopeContent))
        {
            _ = _requestFocus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _raiseIsOpenPropertyChanged();
    }
}
