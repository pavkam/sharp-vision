// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Verifies the shared drop-down popup open/close lifecycle - the composed replacement for
/// the Opened/OpenDropDown/CloseDropDown/OnPopupOpened/OnPopupClosing/OnPopupClosed group formerly
/// hand-rolled by ComboBox, DateInput, and DateTimeInput.</summary>
public sealed class PopupDropDownCoordinatorTests
{
    private static PopupDropDownCoordinator Create(
        ControlBase owner,
        Popup popup,
        ControlBase content,
        Func<bool>? requestFocus = null,
        Action? raiseOpenedPropertyChanged = null,
        Action? raiseDropDownOpened = null,
        Action? raiseDropDownClosed = null,
        Action? beforeOpen = null,
        Action? beforeCloseFocusRestore = null) =>
        new(
            owner,
            popup,
            content,
            requestFocus ?? (static () => true),
            raiseOpenedPropertyChanged ?? (static () => { }),
            raiseDropDownOpened ?? (static () => { }),
            raiseDropDownClosed ?? (static () => { }),
            beforeOpen,
            beforeCloseFocusRestore);

    /// <summary>Verifies opening invokes the optional pre-open hook before the popup's own IsOpen
    /// flips, matching DateInput's EnsureSeeded/SyncCalendar ordering requirement.</summary>
    [Fact]
    public void SetOpen_WhenOpening_InvokesBeforeOpenBeforePopupFlips()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var events = new List<string>();
        var popupWasOpenDuringBeforeOpen = true;

        var coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownOpened: () => events.Add("DropDownOpened"),
            beforeOpen: () =>
            {
                popupWasOpenDuringBeforeOpen = popup.IsOpen;
                events.Add("BeforeOpen");
            });

        coordinator.SetOpen(true);

        popupWasOpenDuringBeforeOpen.ShouldBeFalse();
        popup.IsOpen.ShouldBeTrue();
        coordinator.IsOpen.ShouldBeTrue();

        // An unattached, never-presented popup never raises its own Opened event (only a
        // presented popup does), so PropertyChanged is intentionally excluded here; the presented
        // path is covered separately below.
        events.ShouldBe(["BeforeOpen", "DropDownOpened"]);
    }

    /// <summary>Verifies SetOpen is a true no-op - no popup mutation, no hooks, no events - when the
    /// requested state already matches, matching the IsOpen setter's original compare-before-dispatch.</summary>
    [Fact]
    public void SetOpen_WhenValueUnchanged_DoesNothing()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var opens = 0;
        var closes = 0;

        var coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownOpened: () => opens++,
            raiseDropDownClosed: () => closes++);

        coordinator.SetOpen(false);

        opens.ShouldBe(0);
        closes.ShouldBe(0);
        popup.IsOpen.ShouldBeFalse();

        coordinator.SetOpen(true);
        opens.ShouldBe(1);

        coordinator.SetOpen(true);
        opens.ShouldBe(1);
        closes.ShouldBe(0);
    }

    /// <summary>Verifies DropDownOpened is raised only after the modal scope has actually been
    /// entered, not merely after the popup's own IsOpen flips, and that a fully presented (attached)
    /// popup's own Opened event forwards through to the PropertyChanged hook - the counterpart to
    /// the unattached-popup case above, where Popup never raises Opened at all.</summary>
    [Fact]
    public async Task SetOpen_WhenOpenedSuccessfully_RaisesDropDownOpenedAfterModalEntryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl();
            using var popup = new Popup();
            owner.Children.Add(content);
            owner.Children.Add(popup);
            root.Children.Add(owner);
            root.Attach(dispatcher);

            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var activeScopeDuringRaise = false;
            var propertyChangedCalls = 0;

            var coordinator = Create(
                owner,
                popup,
                content,
                raiseOpenedPropertyChanged: () => propertyChangedCalls++,
                raiseDropDownOpened: () => activeScopeDuringRaise = modality.Active is not null);

            coordinator.SetOpen(true);

            activeScopeDuringRaise.ShouldBeTrue();
            popup.IsOpen.ShouldBeTrue();
            propertyChangedCalls.ShouldBe(1);
            _ = modality.Active.ShouldNotBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a modal-entry failure - here an owner that is not an eligible initial-focus
    /// target - force-closes the popup it had just opened and propagates the failure before
    /// DropDownOpened is ever raised, instead of reporting an open drop-down that never actually
    /// entered its modal scope.</summary>
    [Fact]
    public async Task SetOpen_WhenModalEntryFails_ClosesPopupAndSkipsDropDownOpenedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            // Not IsFocusable: ValidatePlaneRoot accepts it as an attached, visible, enabled plane
            // root, but ValidateInitialFocus then rejects it as the (also requested) initial focus
            // target, so ModalityManager.Enter throws deep inside PopupModalTracker.Enter.
            var owner = new ProbeContainer { IsFocusable = false };
            var content = new ProbeControl();
            owner.Children.Add(content);
            root.Children.Add(owner);
            root.Attach(dispatcher);

            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var popup = new Popup();
            var dropDownOpenedRaised = false;

            var coordinator = Create(
                owner,
                popup,
                content,
                raiseDropDownOpened: () => dropDownOpenedRaised = true);

            _ = Should.Throw<ArgumentException>(() => coordinator.SetOpen(true));

            dropDownOpenedRaised.ShouldBeFalse();
            popup.IsOpen.ShouldBeFalse();
            coordinator.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the critical close-path reentrancy ordering: PopupModalTracker.Exit
    /// synchronously restores focus and can itself close the popup (firing Closing/Closed, and
    /// therefore this coordinator's own hooks, from inside Exit), so Closing/Closed must fire
    /// exactly once each - never once from the reentrant path and again from the coordinator's own
    /// explicit assignment - while DropDownClosed still fires last, from the coordinator's own
    /// close path, after the reentrant Closed handler already ran.</summary>
    [Fact]
    public async Task SetOpen_WhenClosing_ExitsModalBeforePopupFlipsAndNeverDoubleFiresPopupEventsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl { IsFocusable = true };
            owner.Children.Add(content);
            root.Children.Add(owner);
            root.Attach(dispatcher);

            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var popup = new Popup();
            var closingCount = 0;
            var closedCount = 0;
            popup.Closing += (_, _) => closingCount++;
            popup.Closed += (_, _) => closedCount++;

            var events = new List<string>();

            var coordinator = Create(
                owner,
                popup,
                content,
                raiseOpenedPropertyChanged: () => events.Add("PropertyChanged"),
                raiseDropDownOpened: () => events.Add("DropDownOpened"),
                raiseDropDownClosed: () => events.Add("DropDownClosed"),
                beforeCloseFocusRestore: () => events.Add("BeforeCloseFocusRestore"));

            coordinator.SetOpen(true);
            events.Clear();

            coordinator.SetOpen(false);

            popup.IsOpen.ShouldBeFalse();
            coordinator.IsOpen.ShouldBeFalse();
            closingCount.ShouldBe(1);
            closedCount.ShouldBe(1);

            // BeforeCloseFocusRestore always runs first, from inside the reentrant Closing fired
            // by Exit(); the Closed handler's PropertyChanged fires next, also from inside Exit();
            // DropDownClosed fires last of all, from the coordinator's own close path, proving it
            // is not raised a second time by the already-closed popup.
            events.ShouldBe(["BeforeCloseFocusRestore", "PropertyChanged", "DropDownClosed"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Closing's ContainsFocused check requests focus back on the owner when the
    /// popup's content still holds focus at close time, and that the optional pre-restore hook
    /// still runs first - exercised on a plain attached-with-focus-manager owner so focus is not
    /// proactively restored elsewhere before Closing observes it, unlike the full modal-unwind
    /// path exercised above.</summary>
    [Fact]
    public async Task SetOpen_WhenClosingWithFocusInsideContent_InvokesBeforeCloseFocusRestoreThenRequestFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var owner = new ProbeControl();
            var content = new ProbeControl { IsFocusable = true };
            root.Children.Add(owner);
            root.Children.Add(content);
            root.Attach(dispatcher);

            using var focus = new FocusManager(root);
            using var popup = new Popup();
            var events = new List<string>();

            var coordinator = Create(
                owner,
                popup,
                content,
                requestFocus: () =>
                {
                    events.Add("RequestFocus");
                    return true;
                },
                beforeCloseFocusRestore: () => events.Add("BeforeCloseFocusRestore"));

            coordinator.SetOpen(true);
            _ = focus.Focus(content);
            content.IsFocused.ShouldBeTrue();

            coordinator.SetOpen(false);

            events.ShouldBe(["BeforeCloseFocusRestore", "RequestFocus"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Closing's optional pre-restore hook runs even when nothing inside the
    /// popup's content currently holds focus, and that the RequestFocus seam is left untouched in
    /// that case.</summary>
    [Fact]
    public void SetOpen_WhenClosingWithoutFocusInsideContent_InvokesBeforeCloseFocusRestoreButNotRequestFocus()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var requestFocusCalls = 0;
        var beforeCloseCalls = 0;

        var coordinator = Create(
            owner,
            popup,
            content,
            requestFocus: () =>
            {
                requestFocusCalls++;
                return true;
            },
            beforeCloseFocusRestore: () => beforeCloseCalls++);

        coordinator.SetOpen(true);
        coordinator.SetOpen(false);

        beforeCloseCalls.ShouldBe(1);
        requestFocusCalls.ShouldBe(0);
    }

    /// <summary>Verifies the constructor rejects every required dependency, leaving no partially
    /// constructed coordinator subscribed to popup events.</summary>
    [Fact]
    public void Constructor_WhenRequiredArgumentIsNull_ThrowsArgumentNullException()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        static bool RequestFocus() => true;
        static void NoOp() { }

        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(null!, popup, content, RequestFocus, NoOp, NoOp, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, null!, content, RequestFocus, NoOp, NoOp, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, popup, null!, RequestFocus, NoOp, NoOp, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, popup, content, null!, NoOp, NoOp, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, popup, content, RequestFocus, null!, NoOp, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, popup, content, RequestFocus, NoOp, null!, NoOp));
        _ = Should.Throw<ArgumentNullException>(() =>
            new PopupDropDownCoordinator(owner, popup, content, RequestFocus, NoOp, NoOp, null!));
    }

    /// <summary>Verifies Detach unsubscribes from the popup's lifecycle events, so a disposed
    /// owner's coordinator no longer republishes PropertyChanged for a popup it no longer owns.
    /// Opens the popup before detaching (an unattached, never-presented popup never raises its own
    /// Opened event, but does still raise Closing/Closed even unpresented), then closes it directly
    /// through the popup itself - bypassing the coordinator entirely - to prove the coordinator's
    /// own handlers no longer observe that transition.</summary>
    [Fact]
    public void Detach_AfterDetaching_StopsObservingPopupEvents()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var propertyChangedCalls = 0;

        var coordinator = Create(
            owner,
            popup,
            content,
            raiseOpenedPropertyChanged: () => propertyChangedCalls++);

        coordinator.SetOpen(true);
        coordinator.Detach();
        popup.IsOpen = false;

        propertyChangedCalls.ShouldBe(0);
    }
}
