// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Verifies modal Popup presentation lifetime, focus, and failure recovery.</summary>
public sealed class PopupModalityTests
{
    /// <summary>Verifies ordinary attached opening enters the default dismissing modal presentation.</summary>
    [Fact]
    public async Task IsOpen_WhenAttached_EntersDefaultDismissPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            popup.IsOpen = true;

            popup.IsOpen.ShouldBeTrue();
            var scope = modality.Active.ShouldNotBeNull();
            scope.Root.ShouldBeSameAs(popup);
            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a retained Popup can delegate modal lifetime to its logical framework owner.</summary>
    [Fact]
    public async Task IsOpen_WhenModalityIsOwnerManaged_DoesNotEnterASecondScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup
            {
                Content = new ProbeControl { Focusable = true },
                ModalBehavior = PopupModalBehavior.None
            };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            popup.IsOpen = true;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one popup cannot own two simultaneous modal presentations.</summary>
    [Fact]
    public async Task OpenModal_WhenPresentationIsAlreadyLive_RejectsDuplicateWithoutDisturbingFirstAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = popup.OpenModal();

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.Message.ShouldBe("The Popup already has an active modal presentation.");
            popup.IsOpen.ShouldBeTrue();
            action.Visibility.ShouldBe(Visibility.Visible);
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            focus.Focused.ShouldBeSameAs(action);
            scope.Dispose();

            using var replacement = popup.OpenModal();

            replacement.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a duplicate attempted from modal entry callbacks cannot disturb the entering presentation.</summary>
    [Fact]
    public async Task OpenModal_WhenFocusCallbackReenters_RejectsNestedCallAndKeepsOuterPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            InvalidOperationException? nested = null;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    nested = Should.Throw<InvalidOperationException>(() => popup.OpenModal());
                }
            };

            using var scope = popup.OpenModal();

            nested.ShouldNotBeNull().Message.ShouldBe("Popup modal presentations cannot be reentered.");
            popup.IsOpen.ShouldBeTrue();
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            focus.Focused.ShouldBeSameAs(action);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies external disposal closes the default presentation instead of leaving a modeless Popup.</summary>
    [Fact]
    public async Task IsOpen_WhenDefaultScopeExits_ClosesAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            first.Dispose();

            first.IsActive.ShouldBeFalse();
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;
            var second = modality.Active.ShouldNotBeNull();

            second.ShouldNotBeSameAs(first);
            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant replacement from an exit callback cannot be cleared as the old scope unwinds.</summary>
    [Fact]
    public async Task OpenModal_WhenExternalExitCallbackReopens_TracksReplacementByIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var first = popup.OpenModal();
            ModalScope? replacement = null;
            first.Exited += (_, _) => replacement = popup.OpenModal();

            first.Dispose();

            first.IsActive.ShouldBeFalse();
            replacement.ShouldNotBeNull().IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
            popup.IsOpen.ShouldBeTrue();
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope disposed from entry callbacks is returned inactive without stale Popup tracking.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryCallbackDisposesScope_ReturnsInactiveAndAllowsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var disposeOnEntry = true;
            focus.Gained += (_, eventArgs) =>
            {
                if (disposeOnEntry && ReferenceEquals(eventArgs.Current, action))
                {
                    modality.Active.ShouldNotBeNull().Dispose();
                }
            };

            var first = popup.OpenModal();

            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            popup.IsOpen.ShouldBeTrue();
            disposeOnEntry = false;

            using var second = popup.OpenModal();

            second.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a popup closed from entry callbacks also disposes the untracked returned scope.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryCallbackClosesPopup_ReturnsInactiveWithoutStrandedScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var closeOnEntry = true;
            focus.Gained += (_, eventArgs) =>
            {
                if (closeOnEntry && ReferenceEquals(eventArgs.Current, action))
                {
                    popup.IsOpen = false;
                }
            };

            var scope = popup.OpenModal();

            scope.IsActive.ShouldBeFalse();
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            closeOnEntry = false;

            using var recovered = popup.OpenModal(initialFocus: action);

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ordinary close publishes Closing before modality exit and content collapse.</summary>
    [Fact]
    public async Task IsOpen_WhenModalPopupCloses_PublishesClosingBeforeExitAndRestoresBackgroundFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = popup.OpenModal(initialFocus: action);
            var closingCalls = 0;
            popup.Closing += (_, _) =>
            {
                closingCalls++;
                popup.IsOpen.ShouldBeFalse();
                action.Visibility.ShouldBe(Visibility.Visible);
                scope.IsActive.ShouldBeTrue();
                modality.Active.ShouldBeSameAs(scope);
                focus.Focused.ShouldBeSameAs(action);
            };

            popup.IsOpen = false;

            closingCalls.ShouldBe(1);
            action.Visibility.ShouldBe(Visibility.Collapsed);
            focus.Focused.ShouldBeSameAs(background);
            scope.IsActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an earlier Closing failure outranks modal exit failure without suppressing cleanup.</summary>
    [Fact]
    public async Task IsOpen_WhenClosingAndModalExitCallbacksFail_CompletesCloseAndPreservesClosingFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal exit callback failed.");
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = popup.OpenModal();
            scope.Exited += (_, _) => throw expected;
            popup.Closing += (_, _) => throw new InvalidOperationException("The closing callback failed.");
            var closed = 0;
            popup.Closed += (_, _) => closed++;

            var exception = Should.Throw<InvalidOperationException>(() => popup.IsOpen = false);

            exception.Message.ShouldBe("The closing callback failed.");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            closed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies invalid focus rolls back only a popup exposed by the failing call.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsOutsideNewPopup_ReclosesExposedPopupAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var exception = Should.Throw<ArgumentException>(() => popup.OpenModal(initialFocus: background));

            exception.ParamName.ShouldBe("initialFocus");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);

            using var recovered = popup.OpenModal(initialFocus: action);

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies failed modal promotion does not close a pre-existing modeless presentation.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsOutsideModelessPopup_PreservesOpenPresentationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action, FocusOnOpen = false, IsOpen = true };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            _ = Should.Throw<ArgumentException>(() => popup.OpenModal(initialFocus: background));

            popup.IsOpen.ShouldBeTrue();
            action.Visibility.ShouldBe(Visibility.Visible);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rollback failure cannot replace the initiating open-transition failure.</summary>
    [Fact]
    public async Task OpenModal_WhenExposureAndRollbackCallbacksFail_PreservesInitiatingExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The opening callback failed.");
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Popup.IsOpen) && popup.IsOpen)
                {
                    throw expected;
                }
            };
            popup.Closing += (_, _) => throw new InvalidOperationException("The rollback callback failed.");

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeFalse();
            popup.Content.ShouldNotBeNull().Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal-entry failure remains authoritative when visual rollback also fails.</summary>
    [Fact]
    public async Task OpenModal_WhenEntryAndClosingCallbacksFail_PreservesEntryExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var expected = new InvalidOperationException("The modal focus callback failed.");
            var background = new ProbeControl { Focusable = true };
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, action))
                {
                    throw expected;
                }
            };
            popup.Closing += (_, _) => throw new InvalidOperationException("The visual rollback callback failed.");

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(expected);
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies policy validation occurs before a closed popup is exposed.</summary>
    [Fact]
    public async Task OpenModal_WhenOutsideInteractionIsUndefined_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var action = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = action };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
                popup.OpenModal((OutsideInteraction) int.MaxValue));

            exception.ParamName.ShouldBe("outsideInteraction");
            popup.IsOpen.ShouldBeFalse();
            action.Visibility.ShouldBe(Visibility.Collapsed);
            modality.Active.ShouldBeNull();

            using var recovered = popup.OpenModal();

            recovered.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(recovered);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a caller-selected eligible descendant receives modal entry focus.</summary>
    [Fact]
    public async Task OpenModal_WhenInitialFocusIsProvided_FocusesThatDescendantAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new ProbeControl { Focusable = true };
            var second = new ProbeControl { Focusable = true };
            var content = new Overlay { Children = { first, second } };
            var popup = new Popup { Content = content };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            using var scope = popup.OpenModal(OutsideInteraction.Ignore, second);

            scope.OutsideInteraction.ShouldBe(OutsideInteraction.Ignore);
            focus.Focused.ShouldBeSameAs(second);
            popup.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant modal opening is rejected and the outer failed exposure rolls back.</summary>
    [Fact]
    public async Task OpenModal_WhenOpenNotificationReenters_RejectsNestedPresentationAndReclosesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            Exception? nested = null;
            popup.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(Popup.IsOpen) || !popup.IsOpen || nested is not null)
                {
                    return;
                }

                nested = Should.Throw<InvalidOperationException>(() => popup.OpenModal());
                throw nested;
            };

            var exception = Should.Throw<InvalidOperationException>(() => popup.OpenModal());

            exception.ShouldBeSameAs(nested);
            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a popup opened from within another popup's modal content stacks scopes correctly.</summary>
    [Fact]
    public async Task OpenModal_WhenInnerPopupOpensInsideOuterModal_StacksScopesAndUnwindsInOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var outerAction = new ProbeControl { Focusable = true };
            var innerAction = new ProbeControl { Focusable = true };
            var innerPopup = new Popup { Content = innerAction };
            var outerPopup = new Popup { Content = new Overlay { Children = { outerAction, innerPopup } } };
            var root = new Overlay { Children = { background, outerPopup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var outerScope = outerPopup.OpenModal(initialFocus: outerAction);

            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(outerScope);
            focus.Focused.ShouldBeSameAs(outerAction);

            var innerScope = innerPopup.OpenModal(initialFocus: innerAction);

            innerScope.IsActive.ShouldBeTrue();
            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(innerScope);
            focus.Focused.ShouldBeSameAs(innerAction);

            innerScope.Dispose();

            innerScope.IsActive.ShouldBeFalse();
            outerScope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(outerScope);
            focus.Focused.ShouldBeSameAs(outerAction);

            outerScope.Dispose();

            outerScope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rapid modal open/close cycles don't accumulate stale scope state.</summary>
    [Fact]
    public async Task OpenModal_WhenCycledRapidly_DoesNotAccumulateStaleStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            for (var i = 0; i < 20; i++)
            {
                var scope = popup.OpenModal();
                scope.Dispose();
            }

            modality.Active.ShouldBeNull();

            using var final = popup.OpenModal();

            final.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(final);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the popup's owner while the popup is modal cleanly exits the scope.</summary>
    [Fact]
    public async Task Dispose_WhenOwnerIsDisposedDuringModal_ExitsScopeWithoutCrashAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var background = new ProbeControl { Focusable = true };
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { background, popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();

            var scope = popup.OpenModal();

            scope.IsActive.ShouldBeTrue();

            popup.Dispose();

            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detachment reconciles open state and permits one explicit presentation after reattachment.</summary>
    [Fact]
    public async Task Detach_WhenOpenPopupIsReattached_ReopensOnePresentationWithoutLifecycleCloseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            var closing = 0;
            var closed = 0;
            popup.Closing += (_, _) => closing++;
            popup.Closed += (_, _) => closed++;

            root.Children.Remove(popup).ShouldBeTrue();

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            closing.ShouldBe(0);
            closed.ShouldBe(0);

            root.Children.Add(popup);

            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;

            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
            closing.ShouldBe(0);
            closed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing an ancestor of an open popup — not the popup itself — still releases
    /// presentation, so the popup can reopen after the ancestor is reattached instead of permanently
    /// failing FloatingSurface's already-open guard.</summary>
    [Fact]
    public async Task Detach_WhenAncestorOfOpenPopupIsRemoved_ReleasesPresentationAndPermitsReopenAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var holder = new Overlay { Children = { popup } };
            var root = new Overlay { Children = { holder } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            _ = modality.Active.ShouldNotBeNull();

            root.Children.Remove(holder).ShouldBeTrue();

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            modality.Active.ShouldBeNull();

            root.Children.Add(holder);

            popup.IsOpen.ShouldBeFalse();

            _ = Should.NotThrow(() => popup.IsOpen = true);

            popup.IsOpen.ShouldBeTrue();
            var scope = modality.Active.ShouldNotBeNull();
            scope.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disabling preserves presentation while ending and later restoring automatic modality.</summary>
    [Fact]
    public async Task IsEnabled_WhenOpenPopupIsDisabled_PreservesPresentationAndRestoresAutomaticModalityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            new LayoutEngine().Layout(root, new Size(12, 6));
            var bounds = popup.SurfaceBounds;

            popup.IsEnabled = false;

            popup.IsOpen.ShouldBeTrue();
            popup.SurfaceBounds.ShouldBe(bounds);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsEnabled = true;

            popup.IsOpen.ShouldBeTrue();
            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hiding reconciles open state and showing permits an explicit new presentation.</summary>
    [Fact]
    public async Task Visibility_WhenOpenPopupIsHiddenAndShown_ReconcilesPresentationAndModalityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var root = new Overlay { Children = { popup } };
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();
            new LayoutEngine().Layout(root, new Size(12, 6));
            popup.SurfaceBounds.ShouldNotBe(default);

            popup.Visibility = Visibility.Hidden;

            popup.IsOpen.ShouldBeFalse();
            popup.SurfaceBounds.ShouldBe(default);
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.Visibility = Visibility.Visible;

            popup.IsOpen.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            popup.IsOpen = true;

            var second = modality.Active.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Root.ShouldBeSameAs(popup);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies automatic modality follows the nearest disabled ancestor until the full chain recovers.</summary>
    [Fact]
    public async Task IsEnabled_WhenParentAndGrandparentRecover_RestoresExactlyOneAutomaticScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var parent = new Overlay { Children = { popup } };
            var grandparent = new Overlay { Children = { parent } };
            grandparent.Attach(dispatcher);
            using var focus = new FocusManager(grandparent);
            using var pointer = new PointerManager(grandparent);
            using var modality = new ModalityManager(grandparent, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            parent.IsEnabled = false;

            popup.IsOpen.ShouldBeTrue();
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            grandparent.IsEnabled = false;
            parent.IsEnabled = true;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();

            grandparent.IsEnabled = true;

            var restored = modality.Active.ShouldNotBeNull();
            restored.ShouldNotBeSameAs(first);
            restored.Root.ShouldBeSameAs(popup);
            restored.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies automatic modality follows the nearest hidden ancestor until the full chain recovers.</summary>
    [Fact]
    public async Task Visibility_WhenParentAndGrandparentRecover_RestoresExactlyOneAutomaticScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var popup = new Popup { Content = new ProbeControl { Focusable = true } };
            var parent = new Overlay { Children = { popup } };
            var grandparent = new Overlay { Children = { parent } };
            grandparent.Attach(dispatcher);
            using var focus = new FocusManager(grandparent);
            using var pointer = new PointerManager(grandparent);
            using var modality = new ModalityManager(grandparent, focus, pointer);
            popup.IsOpen = true;
            var first = modality.Active.ShouldNotBeNull();

            parent.Visibility = Visibility.Hidden;

            popup.IsOpen.ShouldBeTrue();
            first.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            grandparent.Visibility = Visibility.Hidden;
            parent.Visibility = Visibility.Visible;

            popup.IsOpen.ShouldBeTrue();
            modality.Active.ShouldBeNull();

            grandparent.Visibility = Visibility.Visible;

            var restored = modality.Active.ShouldNotBeNull();
            restored.ShouldNotBeSameAs(first);
            restored.Root.ShouldBeSameAs(popup);
            restored.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }
}
