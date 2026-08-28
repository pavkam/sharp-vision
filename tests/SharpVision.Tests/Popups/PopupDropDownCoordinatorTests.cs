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
        Action? beforeCloseFocusRestore = null,
        Action? beginSession = null,
        Func<KeyEventArgs, bool>? handleNavigationKey = null,
        Action? cancelSession = null,
        Action? acceptSession = null) =>
        new(
            owner,
            popup,
            content,
            requestFocus ?? (static () => true),
            raiseOpenedPropertyChanged ?? (static () => { }),
            raiseDropDownOpened ?? (static () => { }),
            raiseDropDownClosed ?? (static () => { }),
            beforeOpen,
            beforeCloseFocusRestore,
            ownerInitialFocus: null,
            beginSession: beginSession,
            handleNavigationKey: handleNavigationKey,
            cancelSession: cancelSession,
            acceptSession: acceptSession);

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

    /// <summary>Verifies the shared coordinator used by ComboBox, DateInput, and DateTimeInput can
    /// recover when owner PropertyChanged publication fails from the popup's Opened callback.</summary>
    [Fact]
    public async Task SetOpen_WhenOpenedPropertyPublicationFails_RollsBackAndRemainsReusableAsync()
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
            var fail = true;
            var begins = 0;
            var cancellations = 0;
            var expected = new InvalidOperationException("owner PropertyChanged failed");
            var coordinator = Create(
                owner,
                popup,
                content,
                raiseOpenedPropertyChanged: () =>
                {
                    if (fail)
                    {
                        throw expected;
                    }
                },
                beginSession: () => begins++,
                cancelSession: () => cancellations++);

            var thrown = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));
            thrown.ShouldBeSameAs(expected);

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            coordinator.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            begins.ShouldBe(1);
            cancellations.ShouldBe(1);
            coordinator.SessionGeneration.ShouldBe(2UL);
            fail = false;
            coordinator.SetOpen(true);
            popup.IsOpen.ShouldBeTrue();
            coordinator.IsOpen.ShouldBeTrue();
            _ = modality.Active.ShouldNotBeNull();
            begins.ShouldBe(2);
            cancellations.ShouldBe(1);
            coordinator.SessionGeneration.ShouldBe(3UL);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failing owner open notification closes the already-open popup, cancels
    /// that exact session once, and leaves a later opening independent.</summary>
    [Fact]
    public void SetOpen_WhenDropDownOpenedPublicationFails_RollsBackAndRemainsReusable()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var expected = new InvalidOperationException("DropDownOpened failed");
        var fail = true;
        var begins = 0;
        var cancellations = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownOpened: () =>
            {
                if (fail)
                {
                    throw expected;
                }
            },
            beginSession: () => begins++,
            cancelSession: () => cancellations++);

        var thrown = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));

        thrown.ShouldBeSameAs(expected);
        popup.IsOpen.ShouldBeFalse();
        begins.ShouldBe(1);
        cancellations.ShouldBe(1);
        coordinator.SessionGeneration.ShouldBe(2UL);

        fail = false;
        coordinator.SetOpen(true);

        popup.IsOpen.ShouldBeTrue();
        begins.ShouldBe(2);
        cancellations.ShouldBe(1);
        coordinator.SessionGeneration.ShouldBe(3UL);
    }

    /// <summary>Verifies failure cleanup from an older open call cannot exit the modal scope or
    /// cancel the session created when its DropDownOpened callback closes and reopens.</summary>
    [Fact]
    public async Task SetOpen_WhenDropDownOpenedReopensThenThrows_PreservesNewSessionAsync()
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
            var expected = new InvalidOperationException("The stale opening callback failed.");
            var reopen = true;
            var cancellations = 0;
            PopupDropDownCoordinator? coordinator = null;
            coordinator = Create(
                owner,
                popup,
                content,
                raiseDropDownOpened: () =>
                {
                    if (!reopen)
                    {
                        return;
                    }

                    reopen = false;
                    coordinator!.SetOpen(false);
                    coordinator.SetOpen(true);
                    throw expected;
                },
                cancelSession: () => cancellations++);

            var exception = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));

            exception.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeTrue();
            coordinator.IsOpen.ShouldBeTrue();
            coordinator.SessionGeneration.ShouldBe(3UL);
            cancellations.ShouldBe(1);
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
    /// therefore this coordinator's cancellation hook, from inside Exit), so Closing/Closed must
    /// fire exactly once each - never once from the reentrant path and again from the coordinator's
    /// own explicit assignment - while owner close completion waits until Exit has returned.</summary>
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

            // BeforeCloseFocusRestore runs from the reentrant Popup Closing fired inside Exit().
            // Owner property and close publication wait for that modal unwind to return, then run
            // exactly once from the popup's committed-close completion.
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

    /// <summary>Verifies OnOwnerAttached re-enters the modal scope for a popup that was already
    /// open before its owner ever attached to a dispatcher and modality manager, matching an
    /// owner constructed and opened programmatically before being added to a live tree.</summary>
    [Fact]
    public async Task OnOwnerAttached_WhenPopupWasOpenedBeforeAttachment_EntersModalScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl();
            using var popup = new Popup();
            owner.Children.Add(content);
            owner.Children.Add(popup);

            var coordinator = Create(owner, popup, content);

            // Open before the owner is ever attached: SetOpen's own modal-entry attempt is a
            // no-op here since ModalityOwner is null while detached.
            coordinator.SetOpen(true);
            coordinator.IsOpen.ShouldBeTrue();

            var root = new ProbeContainer { Children = { owner } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            modality.Active.ShouldBeNull();

            coordinator.OnOwnerAttached();

            _ = modality.Active.ShouldNotBeNull();
            modality.Active.Root.ShouldBeSameAs(owner);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies OnOwnerAttached is a no-op for a popup that is not currently open,
    /// matching an owner that simply attaches without ever having opened its drop-down.</summary>
    [Fact]
    public async Task OnOwnerAttached_WhenPopupIsClosed_DoesNothingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl();
            using var popup = new Popup();
            owner.Children.Add(content);
            owner.Children.Add(popup);
            var coordinator = Create(owner, popup, content);

            var root = new ProbeContainer { Children = { owner } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            Should.NotThrow(coordinator.OnOwnerAttached);

            modality.Active.ShouldBeNull();
            coordinator.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
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

    /// <summary>Verifies one active navigation session routes initial and repeated navigation
    /// strokes through its one owner-preview interception point exactly once, whether keyboard
    /// focus remains on the owner or moves into the popup content.</summary>
    [Fact]
    public async Task Route_WhenNavigationSessionIsOpen_DeliversOwnerAndContentStrokesExactlyOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl { IsFocusable = true };
            using var popup = new Popup { Content = content, ModalBehavior = PopupModalBehavior.None };
            owner.Children.Add(popup);
            root.Children.Add(owner);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var begins = 0;
            var routed = new List<Stroke>();
            var coordinator = Create(
                owner,
                popup,
                content,
                beginSession: () => begins++,
                handleNavigationKey: key =>
                {
                    routed.Add(key.Stroke);
                    return key.Stroke.Code == Code.Down;
                });

            coordinator.SetOpen(true);
            _ = focus.Focus(owner);
            var ownerKey = Key(Code.Down, KeyAction.Press);
            _ = Router.Route(focus.Focused.ShouldNotBeNull(), Events.Key, ownerKey);

            _ = focus.Focus(content);
            var contentRepeat = Key(Code.Down, KeyAction.Repeat);
            _ = Router.Route(focus.Focused.ShouldNotBeNull(), Events.Key, contentRepeat);

            begins.ShouldBe(1);
            routed.Count.ShouldBe(2);
            routed[0].Action.ShouldBe(KeyAction.Press);
            routed[1].Action.ShouldBe(KeyAction.Repeat);
            ownerKey.IsHandled.ShouldBeTrue();
            contentRepeat.IsHandled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies each non-accepting close path cancels the active session and publishes the
    /// owner's close completion exactly once, including Escape, owner-driven close, direct popup
    /// close, and modal light dismissal.</summary>
    [Fact]
    public async Task Close_WhenSessionWasNotAccepted_CancelsExactlyOnceForEveryClosePathAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Overlay();
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl { IsFocusable = true };
            using var popup = new Popup { Content = content, ModalBehavior = PopupModalBehavior.None };
            owner.Children.Add(popup);
            root.Children.Add(owner);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var cancellations = 0;
            var ownerCloses = 0;
            var coordinator = Create(
                owner,
                popup,
                content,
                raiseDropDownClosed: () => ownerCloses++,
                cancelSession: () => cancellations++);

            coordinator.SetOpen(true);
            _ = Router.Route(content, Events.Key, Key(Code.Escape, KeyAction.Press));
            cancellations.ShouldBe(1);
            ownerCloses.ShouldBe(1);

            coordinator.SetOpen(true);
            coordinator.SetOpen(false);
            cancellations.ShouldBe(2);
            ownerCloses.ShouldBe(2);

            coordinator.SetOpen(true);
            var directCloseScope = modality.Active.ShouldNotBeNull();
            popup.IsOpen = false;
            cancellations.ShouldBe(3);
            ownerCloses.ShouldBe(3);
            modality.Active.ShouldBeNull();

            coordinator.SetOpen(true);
            var lightDismissScope = modality.Active.ShouldNotBeNull();
            lightDismissScope.ShouldNotBeSameAs(directCloseScope);
            _ = pointer.Dispatch(new Pointer(
                new Point(100, 100),
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));
            cancellations.ShouldBe(4);
            ownerCloses.ShouldBe(4);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a begin callback failure rolls back the newly activated session before
    /// the exception leaves the opening call, leaving a later opening request independent.</summary>
    [Fact]
    public void SetOpen_WhenBeginSessionFails_CancelsFailedSessionAndRemainsReusable()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var expected = new InvalidOperationException("begin failed");
        var fail = true;
        var begins = 0;
        var cancellations = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            beginSession: () =>
            {
                begins++;

                if (fail)
                {
                    throw expected;
                }
            },
            cancelSession: () => cancellations++);

        var thrown = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));

        thrown.ShouldBeSameAs(expected);
        popup.IsOpen.ShouldBeFalse();
        cancellations.ShouldBe(1);

        fail = false;
        coordinator.SetOpen(true);

        begins.ShouldBe(2);
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies accepting a session runs its acceptance callback before closing and
    /// suppresses cancellation for that completed session.</summary>
    [Fact]
    public void AcceptAndClose_WhenSessionIsOpen_AcceptsWithoutCancellation()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var accepted = 0;
        var cancelled = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            acceptSession: () => accepted++,
            cancelSession: () => cancelled++);

        coordinator.SetOpen(true);
        coordinator.AcceptAndClose();

        accepted.ShouldBe(1);
        cancelled.ShouldBe(0);
        popup.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a cancellation failure cannot prevent the close hook and focused-content
    /// restoration attempt, and remains the failure rethrown after that cleanup completes.</summary>
    [Fact]
    public async Task SetOpen_WhenCancellationFails_CompletesFocusRestoreAndRethrowsEarliestFailureAsync()
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
            var expected = new InvalidOperationException("cancel failed");
            var events = new List<string>();
            var ownerCloses = 0;
            var coordinator = Create(
                owner,
                popup,
                content,
                requestFocus: () =>
                {
                    events.Add("RequestFocus");
                    return true;
                },
                raiseDropDownClosed: () => ownerCloses++,
                beforeCloseFocusRestore: () => events.Add("BeforeCloseFocusRestore"),
                cancelSession: () => throw expected);

            coordinator.SetOpen(true);
            _ = focus.Focus(content);

            var thrown = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(false));

            thrown.ShouldBeSameAs(expected);
            events.ShouldBe(["BeforeCloseFocusRestore", "RequestFocus"]);
            popup.IsOpen.ShouldBeFalse();
            ownerCloses.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an old close cannot close or cancel the distinct session opened from the
    /// completed popup close notification, which is the reentrant presentation boundary where the
    /// popup itself permits another open.</summary>
    [Fact]
    public async Task SetOpen_WhenClosedHandlerReopens_StartsOneDistinctSurvivingSessionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var owner = new ProbeContainer { IsFocusable = true };
            var content = new ProbeControl();
            using var popup = new Popup { Content = content };
            owner.Children.Add(popup);
            root.Children.Add(owner);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var begins = 0;
            var cancellations = 0;
            var reopen = true;
            PopupDropDownCoordinator coordinator = null!;
            coordinator = Create(
                owner,
                popup,
                content,
                beginSession: () => begins++,
                cancelSession: () => cancellations++,
                raiseDropDownClosed: () =>
                {
                    if (reopen)
                    {
                        reopen = false;
                        coordinator.SetOpen(true);
                    }
                });

            coordinator.SetOpen(true);
            coordinator.SetOpen(false);

            popup.IsOpen.ShouldBeTrue();
            begins.ShouldBe(2);
            cancellations.ShouldBe(1);
            coordinator.SessionGeneration.ShouldBe(3UL);

            coordinator.SetOpen(false);
            cancellations.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detaching the owner releases the preview registration, so later owner
    /// routes cannot reach a session callback retained by an owner that no longer owns the popup.</summary>
    [Fact]
    public void Detach_AfterDetaching_StopsRoutingNavigationKeys()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var navigations = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            handleNavigationKey: _ =>
            {
                navigations++;
                return true;
            });

        coordinator.SetOpen(true);
        coordinator.Detach();
        var eventArgs = Key(Code.Down, KeyAction.Press);

        _ = Router.Route(owner, Events.Key, eventArgs);

        navigations.ShouldBe(0);
        eventArgs.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies detaching makes the coordinator unavailable, so a retained reference
    /// cannot create a session after the preview and popup lifecycle registrations are released.</summary>
    [Fact]
    public void SetOpen_AfterDetach_RejectsFurtherSessions()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var begins = 0;
        var coordinator = Create(owner, popup, content, beginSession: () => begins++);

        coordinator.Detach();

        _ = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));
        begins.ShouldBe(0);
        popup.IsOpen.ShouldBeFalse();
    }

    private static KeyEventArgs Key(Code code, KeyAction action) => new(new Stroke(
        code,
        character: null,
        nativeCode: 0,
        Modifiers.None,
        action));
}
