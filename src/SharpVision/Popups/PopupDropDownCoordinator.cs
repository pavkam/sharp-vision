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
    private readonly Action? _beginSession;
    private readonly Func<KeyEventArgs, bool>? _handleNavigationKey;
    private readonly Action? _cancelSession;
    private readonly Action? _acceptSession;
    private readonly IDisposable _ownerKeyRegistration;
    private bool _hasActiveSession;
    private bool _sessionAccepted;
    private bool _isDetached;

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
    /// <param name="beginSession">Optional owner-specific callback that snapshots committed state
    /// and seeds provisional popup state for each newly opened session.</param>
    /// <param name="handleNavigationKey">Optional canonical navigation callback invoked once from
    /// the owner's preview route for each key while the session remains current. It returns whether
    /// the coordinator must consume the stroke.</param>
    /// <param name="cancelSession">Optional owner-specific callback that restores the opening
    /// state when the session closes without acceptance.</param>
    /// <param name="acceptSession">Optional owner-specific callback that commits provisional
    /// state before an accepted session closes.</param>
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
        ControlBase? ownerInitialFocus = null,
        Action? beginSession = null,
        Func<KeyEventArgs, bool>? handleNavigationKey = null,
        Action? cancelSession = null,
        Action? acceptSession = null)
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
        _beginSession = beginSession;
        _handleNavigationKey = handleNavigationKey;
        _cancelSession = cancelSession;
        _acceptSession = acceptSession;

        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => SetOpen(false));
        _ownerKeyRegistration = _owner.AddHandler(Events.Key, OnOwnerPreviewKey, handledEventsToo: true);
    }

    /// <summary>Gets whether the owned popup is currently open.</summary>
    public bool IsOpen => _popup.IsOpen;

    /// <summary>Gets the monotonically increasing public or internal open-state request version.</summary>
    internal ulong TransitionVersion { get; private set; }

    /// <summary>Gets the monotonically increasing identity of the current or most recently ended
    /// navigation session. Tests use this seam to prove stale close callbacks cannot affect a newer
    /// session.</summary>
    internal ulong SessionGeneration { get; private set; }

    /// <summary>Opens or closes the owned popup, no-op when already at the requested state.</summary>
    /// <param name="value">True to open the popup; false to close it.</param>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or this coordinator is detached.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public void SetOpen(bool value)
    {
        VerifyAvailable();
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

    /// <summary>Commits the active session's provisional state and closes its owned popup.</summary>
    /// <remarks>Acceptance is committed before close begins, so the closing transaction cannot
    /// treat this session as cancelled even when it reenters through modal cleanup.</remarks>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or this coordinator is detached.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    /// <exception cref="Exception">An acceptance or close callback fails after close cleanup completes.</exception>
    public void AcceptAndClose()
    {
        VerifyAvailable();
        _owner.VerifyMutable();

        if (!_popup.IsOpen || !_hasActiveSession)
        {
            return;
        }

        var generation = SessionGeneration;
        _sessionAccepted = true;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => _acceptSession?.Invoke(), ref failure);

        if (IsCurrentSession(generation))
        {
            ExceptionAggregation.Capture(() => SetOpen(false), ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Re-enters the modal scope for an already-open popup once the owner becomes attached.</summary>
    /// <remarks>An owner constructed and opened before attachment defers modal entry until a
    /// dispatcher and modality manager actually exist to enter.</remarks>
    public void OnOwnerAttached()
    {
        if (!_isDetached && _popup.IsOpen)
        {
            _modalTracker.Enter(_owner, _ownerInitialFocus);
        }
    }

    /// <summary>Unsubscribes from the owned popup lifecycle and owner preview route. Called from
    /// the owner's disposal path.</summary>
    /// <exception cref="Exception">A cancellation or modal-scope cleanup callback fails after every release step runs.</exception>
    public void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        _popup.Opened -= OnPopupOpened;
        _popup.Closing -= OnPopupClosing;
        _popup.Closed -= OnPopupClosed;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(_ownerKeyRegistration.Dispose, ref failure);
        ExceptionAggregation.Capture(() => EndSession(restoreOpeningState: true), ref failure);
        ExceptionAggregation.Capture(_modalTracker.Exit, ref failure);
        failure?.Throw();
    }

    private void Open()
    {
        _beforeOpen?.Invoke();
        BeginSession();
        _popup.IsOpen = true;
        _modalTracker.Enter(_owner, _ownerInitialFocus);
        _raiseDropDownOpened();
    }

    private void Close()
    {
        // See the type remarks: Exit() can synchronously close the popup before the explicit
        // assignment below runs, making that assignment a no-op. Do not reorder these statements.
        var sessionGeneration = SessionGeneration;
        _modalTracker.Exit();

        if (IsCurrentSession(sessionGeneration))
        {
            _popup.IsOpen = false;
        }

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
        var closingGeneration = SessionGeneration;
        var hadActiveSession = _hasActiveSession;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => EndSession(restoreOpeningState: true), ref failure);
        ExceptionAggregation.Capture(_modalTracker.Exit, ref failure);

        // A cancellation callback can synchronously start another session from an outer lifecycle
        // callback. That newer session owns its focus; an old close must not pull it back to owner.
        if (!hadActiveSession || SessionGeneration == closingGeneration + 1)
        {
            ExceptionAggregation.Capture(() => _beforeCloseFocusRestore?.Invoke(), ref failure);

            if (ControlBase.ContainsFocused(_focusScopeContent))
            {
                ExceptionAggregation.Capture(() => _ = _requestFocus(), ref failure);
            }
        }

        failure?.Throw();
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _raiseIsOpenPropertyChanged();
    }

    private void BeginSession()
    {
        SessionGeneration++;
        _hasActiveSession = true;
        _sessionAccepted = false;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => _beginSession?.Invoke(), ref failure);

        if (failure is not null)
        {
            ExceptionAggregation.Capture(() => EndSession(restoreOpeningState: true), ref failure);
            failure!.Throw();
        }
    }

    private void EndSession(bool restoreOpeningState)
    {
        if (!_hasActiveSession)
        {
            return;
        }

        var accepted = _sessionAccepted;
        _hasActiveSession = false;
        _sessionAccepted = false;
        SessionGeneration++;

        if (restoreOpeningState && !accepted)
        {
            _cancelSession?.Invoke();
        }
    }

    private bool IsCurrentSession(ulong generation) =>
        _hasActiveSession &&
        SessionGeneration == generation &&
        _popup.IsOpen &&
        !_isDetached;

    private void VerifyAvailable()
    {
        if (_isDetached)
        {
            throw new InvalidOperationException("The popup drop-down coordinator is detached.");
        }
    }

    private void OnOwnerPreviewKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview ||
            _handleNavigationKey is null ||
            !IsCurrentSession(SessionGeneration))
        {
            return;
        }

        var generation = SessionGeneration;

        if (_handleNavigationKey(eventArgs) && IsCurrentSession(generation))
        {
            eventArgs.IsHandled = true;
        }
    }
}
