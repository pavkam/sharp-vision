// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies modal pointer targeting, state isolation, capture, and outside dismissal.</summary>
public sealed class ModalityPointerTests
{
    #region Targeting and outside interaction

    /// <summary>Verifies an outside move clears modal hover without entering background hover or routing.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerMovesOutsidePlane_ClearsModalHoverWithoutBackgroundEntryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var backgroundRoutes = 0;
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    backgroundRoutes++;
                }
            });
            using var scope = modality.Enter(plane);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move)).ShouldBeSameAs(plane);

            pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Move)).ShouldBeNull();

            root.HitTest(new Point(14, 2)).ShouldBeSameAs(background);
            pointer.Hovered.ShouldBeNull();
            plane.IsPointerOver.ShouldBeFalse();
            background.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
            backgroundRoutes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal hover begins at the plane boundary rather than background ancestors.</summary>
    [Fact]
    public async Task Enter_WhenPointerAlreadyHoversInsidePlane_RetainsPlaneHoverAndClearsBackgroundAncestorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeContainer { Bounds = new Rect(0, 0, 8, 6) };
            var leaf = new ProbeControl { Bounds = new Rect(1, 1, 4, 3) };
            plane.Children.Add(leaf);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move)).ShouldBeSameAs(leaf);
            root.IsPointerOver.ShouldBeTrue();
            var planeEntries = 0;
            var planeExits = 0;
            var leafEntries = 0;
            var leafExits = 0;
            plane.PointerEntered += (_, _) => planeEntries++;
            plane.PointerExited += (_, _) => planeExits++;
            leaf.PointerEntered += (_, _) => leafEntries++;
            leaf.PointerExited += (_, _) => leafExits++;

            using var scope = modality.Enter(plane);

            pointer.Hovered.ShouldBeSameAs(leaf);
            plane.IsPointerOver.ShouldBeTrue();
            plane.IsPointerDirectlyOver.ShouldBeFalse();
            leaf.IsPointerOver.ShouldBeTrue();
            leaf.IsPointerDirectlyOver.ShouldBeTrue();
            root.IsPointerOver.ShouldBeFalse();
            planeEntries.ShouldBe(0);
            planeExits.ShouldBe(0);
            leafEntries.ShouldBe(0);
            leafExits.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies same-target reentrant dispatch completes hover publication before routing.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerEntryReentersSameTarget_CompletesHoverPathBeforeNestedRouteAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeContainer { Bounds = new Rect(0, 0, 20, 6) };
            var branch = new ProbeContainer { Bounds = new Rect(1, 1, 16, 4) };
            var leaf = new ProbeControl { Bounds = new Rect(2, 1, 8, 3) };
            branch.Children.Add(leaf);
            plane.Children.Add(branch);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane);
            var input = CreatePointer(new Point(3, 2), PointerAction.Move);
            var order = new List<string>();
            var reentered = false;
            var nestedDispatch = false;
            var nestedRoutes = 0;
            plane.PointerEntered += (_, _) =>
            {
                order.Add("plane-enter");

                if (reentered)
                {
                    return;
                }

                reentered = true;
                nestedDispatch = true;

                try
                {
                    pointer.Dispatch(input).ShouldBeSameAs(leaf);
                }
                finally
                {
                    nestedDispatch = false;
                }
            };
            branch.PointerEntered += (_, _) => order.Add("branch-enter");
            leaf.PointerEntered += (_, _) => order.Add("leaf-enter");
            _ = leaf.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase != RoutingPhase.Bubble || !nestedDispatch)
                {
                    return;
                }

                nestedRoutes++;
                plane.IsPointerOver.ShouldBeTrue();
                branch.IsPointerOver.ShouldBeTrue();
                leaf.IsPointerOver.ShouldBeTrue();
                leaf.IsPointerDirectlyOver.ShouldBeTrue();
                order.Add("nested-route");
                order.ShouldBe(["plane-enter", "branch-enter", "leaf-enter", "nested-route"]);
            });

            pointer.Dispatch(input).ShouldBeSameAs(leaf);

            nestedRoutes.ShouldBe(1);
            order.ShouldBe(["plane-enter", "branch-enter", "leaf-enter", "nested-route"]);
            root.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a nested scope entered from hover cleanup supersedes the outer reconciliation.</summary>
    [Fact]
    public async Task Enter_WhenHoverCallbackEntersNestedScope_AppliesOnlyYoungestHoverPathAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var outerRoot = new ProbeContainer { Bounds = new Rect(0, 0, 24, 6) };
            var hovered = new ProbeControl { Bounds = new Rect(1, 1, 8, 4) };
            var innerRoot = new ProbeControl { Bounds = new Rect(14, 1, 8, 4) };
            outerRoot.Children.Add(hovered);
            outerRoot.Children.Add(innerRoot);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            pointer.Dispatch(CreatePointer(new Point(3, 2), PointerAction.Move)).ShouldBeSameAs(hovered);
            ModalScope? nested = null;
            root.PointerExited += (_, _) => nested ??= modality.Enter(innerRoot);

            using var outer = modality.Enter(outerRoot);

            nested.ShouldNotBeNull().IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(nested);
            pointer.Hovered.ShouldBeNull();
            root.IsPointerOver.ShouldBeFalse();
            outerRoot.IsPointerOver.ShouldBeFalse();
            hovered.IsPointerOver.ShouldBeFalse();
            innerRoot.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant modal entry owns hover-cleanup failures before its handle can escape.</summary>
    [Fact]
    public async Task Enter_WhenReentrantHoverCleanupThrows_RollsBackBeforeEnterReturnsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var trigger = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var modalRoot = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(trigger);
            root.Children.Add(modalRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var failure = new InvalidOperationException("The reentrant hover cleanup failed.");
            var enterReturned = false;
            trigger.PointerExited += (_, _) => throw failure;
            trigger.PointerEntered += (_, _) =>
            {
                _ = modality.Enter(modalRoot);
                enterReturned = true;
            };

            Should.Throw<InvalidOperationException>(() =>
                pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move)))
                .ShouldBeSameAs(failure);

            enterReturned.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            trigger.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies scope entry during hover consumes the already-targeted press without routing or focus leakage.</summary>
    [Fact]
    public async Task Dispatch_WhenHoverCallbackEntersScope_ConsumesCapturedRecordAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 28, 8) };
            var modalRoot = new ProbeContainer { Bounds = new Rect(0, 0, 10, 6) };
            var modalFocus = new ProbeControl
            {
                Bounds = new Rect(1, 1, 6, 4),
                Focusable = true,
            };
            var background = new ProbeControl
            {
                Bounds = new Rect(16, 0, 8, 6),
                Focusable = true,
            };
            modalRoot.Children.Add(modalFocus);
            root.Children.Add(modalRoot);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            ModalScope? scope = null;
            var routes = 0;
            background.PointerEntered += (_, _) =>
                scope ??= modality.Enter(modalRoot, initialFocus: modalFocus);
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });

            pointer.Dispatch(CreatePointer(new Point(18, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();

            scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(modalFocus);
            background.IsFocused.ShouldBeFalse();
            pointer.PressOrigin.ShouldBeNull();
            routes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies scope exit during hover cannot expose the initiating press to the restored background.</summary>
    [Fact]
    public async Task Dispatch_WhenHoverCallbackExitsScope_ConsumesCapturedRecordAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var plane = new ProbeContainer { Bounds = new Rect(0, 0, 18, 6) };
            var first = new ProbeControl
            {
                Bounds = new Rect(1, 1, 6, 4),
                Focusable = true,
            };
            var second = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                Focusable = true,
            };
            var saved = new ProbeControl
            {
                Bounds = new Rect(22, 0, 6, 6),
                Focusable = true,
            };
            plane.Children.Add(first);
            plane.Children.Add(second);
            root.Children.Add(plane);
            root.Children.Add(saved);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(saved).ShouldBeTrue();
            var scope = modality.Enter(plane, initialFocus: first);
            pointer.Dispatch(CreatePointer(new Point(3, 2), PointerAction.Move)).ShouldBeSameAs(first);
            var routes = 0;
            first.PointerExited += (_, _) => scope.Dispose();
            _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });

            pointer.Dispatch(CreatePointer(new Point(12, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();

            scope.IsActive.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(saved);
            second.IsFocused.ShouldBeFalse();
            pointer.PressOrigin.ShouldBeNull();
            routes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hover callbacks cannot route a target that detaches or disposes before delivery.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_WhenHoverCallbackRemovesTarget_ConsumesWithoutRoutingAsync(bool dispose)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeContainer { Bounds = new Rect(0, 0, 20, 6) };
            var modalFocus = new ProbeControl
            {
                Bounds = new Rect(1, 1, 6, 4),
                Focusable = true,
            };
            var target = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                Focusable = true,
            };
            plane.Children.Add(modalFocus);
            plane.Children.Add(target);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: modalFocus);
            var routes = 0;
            _ = target.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });
            target.PointerEntered += (_, _) =>
            {
                if (dispose)
                {
                    target.Dispose();
                }
                else
                {
                    _ = plane.Children.Remove(target);
                }
            };

            pointer.Dispatch(CreatePointer(new Point(12, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();

            target.Parent.ShouldBeNull();
            target.IsDisposed.ShouldBe(dispose);
            focus.Focused.ShouldBeSameAs(modalFocus);
            pointer.PressOrigin.ShouldBeNull();
            routes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a focus callback that changes the plane prevents the captured pointer route.</summary>
    [Fact]
    public async Task Dispatch_WhenFocusCallbackChangesScope_ConsumesBeforeRoutingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var outerRoot = new ProbeContainer { Bounds = new Rect(0, 0, 18, 6) };
            var initial = new ProbeControl
            {
                Bounds = new Rect(1, 1, 6, 4),
                Focusable = true,
            };
            var target = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                Focusable = true,
            };
            var nestedRoot = new ProbeContainer { Bounds = new Rect(22, 0, 8, 6) };
            var nestedFocus = new ProbeControl
            {
                Bounds = new Rect(23, 1, 4, 4),
                Focusable = true,
            };
            outerRoot.Children.Add(initial);
            outerRoot.Children.Add(target);
            nestedRoot.Children.Add(nestedFocus);
            root.Children.Add(outerRoot);
            root.Children.Add(nestedRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: initial);
            pointer.Dispatch(CreatePointer(new Point(12, 2), PointerAction.Move)).ShouldBeSameAs(target);
            ModalScope? nested = null;
            var routes = 0;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, target))
                {
                    nested ??= modality.Enter(nestedRoot, initialFocus: nestedFocus);
                }
            };
            _ = target.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });

            pointer.Dispatch(CreatePointer(new Point(12, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();

            nested.ShouldNotBeNull().IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(nested);
            focus.Focused.ShouldBeSameAs(nestedFocus);
            pointer.PressOrigin.ShouldBeNull();
            routes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies release and leave clear press bookkeeping even when hover-exit publication fails.</summary>
    [Theory]
    [InlineData(PointerAction.Release)]
    [InlineData(PointerAction.Leave)]
    public async Task Dispatch_WhenHoverExitThrowsDuringReleaseOrLeave_ClearsPressBeforeRethrowAsync(
        PointerAction action)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(plane);
            pointer.PressOrigin.ShouldBeSameAs(plane);
            var failure = new InvalidOperationException("The hover exit callback failed.");
            plane.PointerExited += (_, _) => throw failure;
            var routes = 0;
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });
            var input = action == PointerAction.Leave
                ? CreateLeavePointer()
                : CreatePointer(new Point(14, 2), PointerAction.Release, Buttons.Primary);

            Should.Throw<InvalidOperationException>(() => pointer.Dispatch(input)).ShouldBeSameAs(failure);

            pointer.PressOrigin.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            plane.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
            routes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hover failure cannot suppress a qualifying dismissal attempt or replace its exception.</summary>
    [Theory]
    [InlineData(PointerAction.Press)]
    [InlineData(PointerAction.Wheel)]
    public async Task Dispatch_WhenHoverExitAndDismissCallbacksThrow_CompletesDismissThenRethrowsHoverAsync(
        PointerAction action)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move)).ShouldBeSameAs(plane);
            var hoverFailure = new InvalidOperationException("The hover exit callback failed.");
            var dismissFailure = new InvalidOperationException("The dismissal callback failed.");
            plane.PointerExited += (_, _) => throw hoverFailure;
            var dismissals = 0;
            scope.DismissRequested += (_, _) =>
            {
                dismissals++;
                throw dismissFailure;
            };
            var input = action == PointerAction.Press
                ? CreatePointer(new Point(14, 2), action, Buttons.Primary)
                : CreatePointer(new Point(14, 2), action, wheelY: 1);

            Should.Throw<InvalidOperationException>(() => pointer.Dispatch(input))
                .ShouldBeSameAs(hoverFailure);

            dismissals.ShouldBe(1);
            scope.IsActive.ShouldBeTrue();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            background.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unhandled in-plane wheel completes against the active scope's outside policy.</summary>
    [Theory]
    [InlineData(OutsideInteraction.Ignore, 0)]
    [InlineData(OutsideInteraction.Dismiss, 1)]
    public async Task Dispatch_WhenInPlaneWheelRemainsUnhandled_AppliesScopePolicyAsync(
        OutsideInteraction policy,
        int expectedDismissals)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, policy);
            var dismissals = 0;
            scope.DismissRequested += (_, _) => dismissals++;

            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Wheel, wheelY: -1))
                .ShouldBeSameAs(plane);

            dismissals.ShouldBe(expectedDismissals);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a handled in-plane wheel remains owned by its control without dismissing the plane.</summary>
    [Fact]
    public async Task Dispatch_WhenInPlaneWheelIsHandled_DoesNotRequestDismissalAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            var dismissals = 0;
            scope.DismissRequested += (_, _) => dismissals++;
            _ = plane.AddHandler(Events.Pointer, (_, eventArgs) => eventArgs.Handled = true);

            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Wheel, wheelY: -1))
                .ShouldBeSameAs(plane);

            dismissals.ShouldBe(0);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ignored outside transitions never route or create background press bookkeeping.</summary>
    [Fact]
    public async Task Dispatch_WhenOutsideInteractionIsIgnored_ConsumesPressReleaseAndWheelAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            List<PointerAction> routed = [];
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routed.Add(eventArgs.Pointer.Action);
                }
            });
            using var scope = modality.Enter(plane);
            var outside = new Point(14, 2);

            pointer.Dispatch(CreatePointer(outside, PointerAction.Press, Buttons.Primary)).ShouldBeNull();
            pointer.Dispatch(CreatePointer(outside, PointerAction.Release, Buttons.Primary)).ShouldBeNull();
            pointer.Dispatch(CreatePointer(outside, PointerAction.Wheel, wheelY: 1)).ShouldBeNull();

            routed.ShouldBeEmpty();
            pointer.PressOrigin.ShouldBeNull();
            background.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies only qualifying outside records request one dismissal for a multi-root plane.</summary>
    [Fact]
    public async Task Dispatch_WhenOutsideInteractionDismisses_RequestsOncePerPrimaryPressOrWheelAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 36, 8) };
            var primary = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var included = new ProbeControl { Bounds = new Rect(10, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(24, 0, 8, 6) };
            root.Children.Add(primary);
            root.Children.Add(included);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary, OutsideInteraction.Dismiss);
            scope.Include(included);
            var dismissals = 0;
            scope.DismissRequested += (_, _) => dismissals++;
            var outside = new Point(26, 2);

            _ = pointer.Dispatch(CreatePointer(outside, PointerAction.Move));
            _ = pointer.Dispatch(CreatePointer(outside, PointerAction.Press, Buttons.Secondary));
            _ = pointer.Dispatch(CreatePointer(outside, PointerAction.Release, Buttons.Secondary));
            dismissals.ShouldBe(0);
            _ = pointer.Dispatch(CreatePointer(outside, PointerAction.Press, Buttons.Primary));
            dismissals.ShouldBe(1);
            _ = pointer.Dispatch(CreatePointer(outside, PointerAction.Wheel, wheelY: -1));

            dismissals.ShouldBe(2);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failing dismissal callback retains the scope and future records may request again.</summary>
    [Fact]
    public async Task Dispatch_WhenDismissCallbackThrows_ConsumesRecordAndKeepsScopeActiveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            var failure = new InvalidOperationException("The dismissal callback failed.");
            var dismissals = 0;
            var backgroundRoutes = 0;
            scope.DismissRequested += (_, _) =>
            {
                dismissals++;
                throw failure;
            };
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    backgroundRoutes++;
                }
            });
            var input = CreatePointer(new Point(14, 2), PointerAction.Press, Buttons.Primary);

            Should.Throw<InvalidOperationException>(() => pointer.Dispatch(input)).ShouldBeSameAs(failure);
            scope.IsActive.ShouldBeTrue();
            pointer.PressOrigin.ShouldBeNull();
            backgroundRoutes.ShouldBe(0);
            Should.Throw<InvalidOperationException>(() => pointer.Dispatch(input)).ShouldBeSameAs(failure);

            dismissals.ShouldBe(2);
            backgroundRoutes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies eligible modal capture wins outside geometry and suppresses dismissal.</summary>
    [Fact]
    public async Task Dispatch_WhenPlaneOwnsCapture_RoutesOutsideRecordsToCaptureWithoutDismissalAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            var dismissals = 0;
            var modalRoutes = 0;
            var backgroundRoutes = 0;
            scope.DismissRequested += (_, _) => dismissals++;
            _ = plane.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    modalRoutes++;
                }
            });
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    backgroundRoutes++;
                }
            });
            pointer.Capture(plane).ShouldBeTrue();
            var outside = new Point(14, 2);

            pointer.Dispatch(CreatePointer(outside, PointerAction.Move)).ShouldBeSameAs(plane);
            pointer.Dispatch(CreatePointer(outside, PointerAction.Press, Buttons.Primary)).ShouldBeSameAs(plane);
            pointer.Dispatch(CreatePointer(outside, PointerAction.Wheel, wheelY: 1)).ShouldBeSameAs(plane);

            pointer.Captured.ShouldBeSameAs(plane);
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeSameAs(plane);
            dismissals.ShouldBe(0);
            modalRoutes.ShouldBe(3);
            backgroundRoutes.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Capture reconciliation and lifetime

    /// <summary>Verifies blocked capture cannot replace an eligible in-plane capture owner.</summary>
    [Fact]
    public async Task Capture_WhenTargetIsOutsideActivePlane_ReturnsFalseAndPreservesOwnerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane);
            pointer.Capture(plane).ShouldBeTrue();

            pointer.Capture(background).ShouldBeFalse();

            pointer.Captured.ShouldBeSameAs(plane);
            plane.ProbeHasPointerCapture.ShouldBeTrue();
            background.ProbeHasPointerCapture.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies exiting a child scope reconfines retained pointer state to its reactivated parent plane.</summary>
    [Fact]
    public async Task Dispose_WhenChildPointerStateIsOutsideParentPlane_ReconfinesBeforeParentResumesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 32, 8) };
            var parentRoot = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var childRoot = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(parentRoot);
            pointer.Capture(parentRoot).ShouldBeTrue();
            var child = modality.Enter(childRoot);
            pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(childRoot);
            pointer.Capture(childRoot).ShouldBeTrue();
            PointerCaptureLossReason? reason = null;
            childRoot.LostPointerCapture += (_, eventArgs) => reason = eventArgs.Reason;

            child.Dispose();

            modality.Active.ShouldBeSameAs(parent);
            parent.IsActive.ShouldBeTrue();
            child.IsActive.ShouldBeFalse();
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            parentRoot.ProbeHasPointerCapture.ShouldBeFalse();
            childRoot.ProbeHasPointerCapture.ShouldBeFalse();
            childRoot.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
            reason.ShouldBe(PointerCaptureLossReason.ModalScopeChanged);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant disposal during initial pointer reconfinement cannot finish the unwind early.</summary>
    [Fact]
    public async Task Dispose_WhenPointerReconfinementDisposesParent_PreservesLateFailureAndExitOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 32, 8) };
            var parentRoot = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var childRoot = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var parent = modality.Enter(parentRoot);
            var child = modality.Enter(childRoot);
            pointer.Capture(childRoot).ShouldBeTrue();
            var expected = new InvalidOperationException("Pointer reconfinement failed after reentrant disposal.");
            var order = new List<string>();
            child.Exited += (_, _) => order.Add("child");
            parent.Exited += (_, _) => order.Add("parent");
            childRoot.LostPointerCapture += (_, _) =>
            {
                parent.Dispose();
                parent.IsActive.ShouldBeFalse();
                child.IsActive.ShouldBeFalse();
                modality.Active.ShouldBeNull();
                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(child.Dispose);

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeNull();
            parent.IsActive.ShouldBeFalse();
            child.IsActive.ShouldBeFalse();
            pointer.Captured.ShouldBeNull();
            order.ShouldBe(["child", "parent"]);
            parent.Dispose();
            child.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope entered by pump-time pointer reconciliation commits inactive before publication.</summary>
    [Fact]
    public async Task Dispose_WhenPointerReconciliationEntersAnotherScope_CommitsItBeforePublicationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 48, 8) };
            var outerRoot = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var innerRoot = new ProbeControl { Bounds = new Rect(10, 0, 8, 6) };
            var reentrantRoot = new ProbeControl { Bounds = new Rect(20, 0, 8, 6) };
            var replacementRoot = new ProbeControl { Bounds = new Rect(30, 0, 8, 6) };
            root.Children.Add(outerRoot);
            root.Children.Add(innerRoot);
            root.Children.Add(reentrantRoot);
            root.Children.Add(replacementRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var outer = modality.Enter(outerRoot);
            var inner = modality.Enter(innerRoot);
            ModalScope? reentrant = null;
            ModalScope? replacement = null;
            var replacementWasInactiveDuringExit = false;
            var order = new List<string>();
            outer.Exited += (_, _) => order.Add("outer");
            inner.Exited += (_, _) =>
            {
                order.Add("inner");
                reentrant = modality.Enter(reentrantRoot);
                reentrant.Exited += (_, _) => order.Add("reentrant");
                pointer.Dispatch(CreatePointer(new Point(22, 2), PointerAction.Move))
                    .ShouldBeSameAs(reentrantRoot);
            };
            root.PointerEntered += (_, _) =>
            {
                if (replacement is not null)
                {
                    return;
                }

                replacement = modality.Enter(replacementRoot);
                replacement.Exited += (_, _) =>
                {
                    replacementWasInactiveDuringExit = !replacement.IsActive;
                    order.Add("replacement");
                };
            };

            outer.Dispose();

            outer.IsActive.ShouldBeFalse();
            inner.IsActive.ShouldBeFalse();
            reentrant.ShouldNotBeNull().IsActive.ShouldBeFalse();
            replacement.ShouldNotBeNull().IsActive.ShouldBeFalse();
            replacementWasInactiveDuringExit.ShouldBeTrue();
            modality.Active.ShouldBeNull();
            order.ShouldBe(["inner", "replacement", "reentrant", "outer"]);
            inner.Dispose();
            reentrant.Dispose();
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies exiting a nested child preserves pointer state that remains inside the parent plane.</summary>
    [Fact]
    public async Task Dispose_WhenChildPointerStateRemainsInsideParentPlane_PreservesStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var parentRoot = new ProbeContainer { Bounds = new Rect(0, 0, 20, 6) };
            var childRoot = new ProbeControl { Bounds = new Rect(4, 1, 8, 4) };
            parentRoot.Children.Add(childRoot);
            root.Children.Add(parentRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot);
            var child = modality.Enter(childRoot);
            pointer.Dispatch(CreatePointer(new Point(6, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(childRoot);
            pointer.Capture(childRoot).ShouldBeTrue();
            var captureLosses = 0;
            childRoot.LostPointerCapture += (_, _) => captureLosses++;

            child.Dispose();

            modality.Active.ShouldBeSameAs(parent);
            parent.IsActive.ShouldBeTrue();
            child.IsActive.ShouldBeFalse();
            pointer.Captured.ShouldBeSameAs(childRoot);
            pointer.Hovered.ShouldBeSameAs(childRoot);
            pointer.PressOrigin.ShouldBeSameAs(childRoot);
            childRoot.ProbeHasPointerCapture.ShouldBeTrue();
            childRoot.IsPointerOver.ShouldBeTrue();
            parentRoot.IsPointerOver.ShouldBeTrue();
            root.IsPointerOver.ShouldBeFalse();
            captureLosses.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies failed child entry reconfines reentrant pointer state to the surviving parent plane.</summary>
    [Fact]
    public async Task Enter_WhenChildEntryRollsBack_ReconfinesPointerStateToParentPlaneAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 40, 8) };
            var parentRoot = new ProbeContainer { Bounds = new Rect(0, 0, 8, 6) };
            var parentFocus = new ProbeControl { Focusable = true };
            var childRoot = new ProbeContainer { Bounds = new Rect(12, 0, 16, 6) };
            var childPointer = new ProbeControl { Bounds = new Rect(13, 1, 6, 4) };
            var childFocus = new ProbeControl
            {
                Bounds = new Rect(21, 1, 6, 4),
                Focusable = true,
            };
            parentRoot.Children.Add(parentFocus);
            childRoot.Children.Add(childPointer);
            childRoot.Children.Add(childFocus);
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot, initialFocus: parentFocus);
            var expected = new InvalidOperationException("The child entry failed.");
            PointerCaptureLossReason? reason = null;
            childPointer.LostPointerCapture += (_, eventArgs) => reason = eventArgs.Reason;
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, childFocus))
                {
                    return;
                }

                pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Press, Buttons.Primary))
                    .ShouldBeSameAs(childPointer);
                pointer.Capture(childPointer).ShouldBeTrue();
                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                modality.Enter(childRoot, initialFocus: childFocus));

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeSameAs(parent);
            parent.IsActive.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(parentFocus);
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            childPointer.ProbeHasPointerCapture.ShouldBeFalse();
            childPointer.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
            reason.ShouldBe(PointerCaptureLossReason.ModalScopeChanged);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies pointer-reconfinement failure cannot suppress focus restoration or exit publication.</summary>
    [Fact]
    public async Task Dispose_WhenPointerReconfinementCallbackThrows_CompletesExitAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 32, 8) };
            var parentRoot = new ProbeContainer { Bounds = new Rect(0, 0, 8, 6) };
            var parentFocus = new ProbeControl { Focusable = true };
            var childRoot = new ProbeContainer { Bounds = new Rect(12, 0, 8, 6) };
            var childFocus = new ProbeControl
            {
                Bounds = new Rect(13, 1, 6, 4),
                Focusable = true,
            };
            parentRoot.Children.Add(parentFocus);
            childRoot.Children.Add(childFocus);
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot, initialFocus: parentFocus);
            var child = modality.Enter(childRoot, initialFocus: childFocus);
            pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(childFocus);
            pointer.Capture(childFocus).ShouldBeTrue();
            var pointerFailure = new InvalidOperationException("The child pointer exit failed.");
            var focusFailure = new InvalidOperationException("The parent focus notification failed.");
            var exitFailure = new InvalidOperationException("The child exit notification failed.");
            var exited = 0;
            childFocus.PointerExited += (_, _) => throw pointerFailure;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, parentFocus))
                {
                    throw focusFailure;
                }
            };
            child.Exited += (_, _) => exited++;
            child.Exited += (_, _) => throw exitFailure;

            var thrown = Should.Throw<InvalidOperationException>(child.Dispose);

            thrown.ShouldBeSameAs(pointerFailure);
            modality.Active.ShouldBeSameAs(parent);
            parent.IsActive.ShouldBeTrue();
            child.IsActive.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(parentFocus);
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            childFocus.ProbeHasPointerCapture.ShouldBeFalse();
            childFocus.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
            exited.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ending the last scope expands retained hover ancestry before exit observers run.</summary>
    [Fact]
    public async Task Dispose_WhenLastScopeEnds_ExpandsRetainedHoverPathBeforeExitedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 28, 8) };
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 24, 7) };
            var plane = new ProbeContainer { Bounds = new Rect(2, 1, 18, 5) };
            var leaf = new ProbeControl { Bounds = new Rect(4, 2, 8, 3) };
            plane.Children.Add(leaf);
            ancestor.Children.Add(plane);
            root.Children.Add(ancestor);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(plane);
            pointer.Dispatch(CreatePointer(new Point(6, 3), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(leaf);
            pointer.Capture(leaf).ShouldBeTrue();
            var order = new List<string>();
            var planeEntries = 0;
            var planeExits = 0;
            var leafEntries = 0;
            var leafExits = 0;
            var captureLosses = 0;
            var routes = 0;
            root.PointerEntered += (_, _) => order.Add("root-enter");
            ancestor.PointerEntered += (_, _) => order.Add("ancestor-enter");
            plane.PointerEntered += (_, _) => planeEntries++;
            plane.PointerExited += (_, _) => planeExits++;
            leaf.PointerEntered += (_, _) => leafEntries++;
            leaf.PointerExited += (_, _) => leafExits++;
            leaf.LostPointerCapture += (_, _) => captureLosses++;
            _ = leaf.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes++;
                }
            });
            scope.Exited += (_, _) => AssertExpanded();

            scope.Dispose();

            AssertExpanded();
            return;

            void AssertExpanded()
            {
                modality.Active.ShouldBeNull();
                scope.IsActive.ShouldBeFalse();
                pointer.Captured.ShouldBeSameAs(leaf);
                pointer.Hovered.ShouldBeSameAs(leaf);
                pointer.PressOrigin.ShouldBeSameAs(leaf);
                root.IsPointerOver.ShouldBeTrue();
                ancestor.IsPointerOver.ShouldBeTrue();
                plane.IsPointerOver.ShouldBeTrue();
                leaf.IsPointerOver.ShouldBeTrue();
                root.IsPointerDirectlyOver.ShouldBeFalse();
                ancestor.IsPointerDirectlyOver.ShouldBeFalse();
                plane.IsPointerDirectlyOver.ShouldBeFalse();
                leaf.IsPointerDirectlyOver.ShouldBeTrue();
                order.ShouldBe(["root-enter", "ancestor-enter"]);
                planeEntries.ShouldBe(0);
                planeExits.ShouldBe(0);
                leafEntries.ShouldBe(0);
                leafExits.ShouldBe(0);
                captureLosses.ShouldBe(0);
                routes.ShouldBe(0);
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies final entry rollback restores the retained unrestricted hover ancestry.</summary>
    [Fact]
    public async Task Enter_WhenLastScopeRollsBack_RestoresRetainedUnrestrictedHoverPathAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 28, 8) };
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 24, 7) };
            var plane = new ProbeContainer { Bounds = new Rect(2, 1, 18, 5) };
            var leaf = new ProbeControl { Bounds = new Rect(4, 2, 6, 3) };
            var initial = new ProbeControl
            {
                Bounds = new Rect(12, 2, 6, 3),
                Focusable = true,
            };
            plane.Children.Add(leaf);
            plane.Children.Add(initial);
            ancestor.Children.Add(plane);
            root.Children.Add(ancestor);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            pointer.Dispatch(CreatePointer(new Point(6, 3), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(leaf);
            pointer.Capture(leaf).ShouldBeTrue();
            var expected = new InvalidOperationException("The sole scope entry failed.");
            var rootEntries = 0;
            var rootExits = 0;
            var ancestorEntries = 0;
            var ancestorExits = 0;
            var planeEntries = 0;
            var planeExits = 0;
            var leafEntries = 0;
            var leafExits = 0;
            var captureLosses = 0;
            root.PointerEntered += (_, _) => rootEntries++;
            root.PointerExited += (_, _) => rootExits++;
            ancestor.PointerEntered += (_, _) => ancestorEntries++;
            ancestor.PointerExited += (_, _) => ancestorExits++;
            plane.PointerEntered += (_, _) => planeEntries++;
            plane.PointerExited += (_, _) => planeExits++;
            leaf.PointerEntered += (_, _) => leafEntries++;
            leaf.PointerExited += (_, _) => leafExits++;
            leaf.LostPointerCapture += (_, _) => captureLosses++;
            focus.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, initial))
                {
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                modality.Enter(plane, initialFocus: initial));

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeNull();
            pointer.Captured.ShouldBeSameAs(leaf);
            pointer.Hovered.ShouldBeSameAs(leaf);
            pointer.PressOrigin.ShouldBeSameAs(leaf);
            root.IsPointerOver.ShouldBeTrue();
            ancestor.IsPointerOver.ShouldBeTrue();
            plane.IsPointerOver.ShouldBeTrue();
            leaf.IsPointerOver.ShouldBeTrue();
            rootEntries.ShouldBe(1);
            rootExits.ShouldBe(1);
            ancestorEntries.ShouldBe(1);
            ancestorExits.ShouldBe(1);
            planeEntries.ShouldBe(0);
            planeExits.ShouldBe(0);
            leafEntries.ShouldBe(0);
            leafExits.ShouldBe(0);
            captureLosses.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unrestricted hover expansion failure cannot suppress focus restoration or exit publication.</summary>
    [Fact]
    public async Task Dispose_WhenUnrestrictedHoverExpansionThrows_CompletesExitAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 32, 8) };
            var background = new ProbeControl
            {
                Bounds = new Rect(24, 0, 6, 6),
                Focusable = true,
            };
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 20, 7) };
            var plane = new ProbeContainer { Bounds = new Rect(2, 1, 16, 5) };
            var leaf = new ProbeControl
            {
                Bounds = new Rect(4, 2, 8, 3),
                Focusable = true,
            };
            plane.Children.Add(leaf);
            ancestor.Children.Add(plane);
            root.Children.Add(ancestor);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = modality.Enter(plane, initialFocus: leaf);
            pointer.Dispatch(CreatePointer(new Point(6, 3), PointerAction.Move))
                .ShouldBeSameAs(leaf);
            var pointerFailure = new InvalidOperationException("The unrestricted hover expansion failed.");
            var focusFailure = new InvalidOperationException("The background focus notification failed.");
            var exitFailure = new InvalidOperationException("The final scope exit notification failed.");
            var exited = 0;
            root.PointerEntered += (_, _) => throw pointerFailure;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, background))
                {
                    throw focusFailure;
                }
            };
            scope.Exited += (_, _) => exited++;
            scope.Exited += (_, _) => throw exitFailure;

            var thrown = Should.Throw<InvalidOperationException>(scope.Dispose);

            thrown.ShouldBeSameAs(pointerFailure);
            modality.Active.ShouldBeNull();
            scope.IsActive.ShouldBeFalse();
            pointer.Hovered.ShouldBeSameAs(leaf);
            root.IsPointerOver.ShouldBeTrue();
            ancestor.IsPointerOver.ShouldBeTrue();
            plane.IsPointerOver.ShouldBeTrue();
            leaf.IsPointerOver.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(background);
            exited.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies final outside dismissal with no retained hover never enters background ancestry.</summary>
    [Fact]
    public async Task Dispatch_WhenOutsideDismissalEndsLastScope_DoesNotEnterBackgroundHoverAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 28, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var background = new ProbeControl { Bounds = new Rect(14, 0, 8, 6) };
            root.Children.Add(plane);
            root.Children.Add(background);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move))
                .ShouldBeSameAs(plane);
            var rootEntries = 0;
            var backgroundEntries = 0;
            var backgroundRoutes = 0;
            root.PointerEntered += (_, _) => rootEntries++;
            background.PointerEntered += (_, _) => backgroundEntries++;
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    backgroundRoutes++;
                }
            });
            scope.DismissRequested += (_, _) => scope.Dispose();
            scope.Exited += (_, _) => AssertNoBackgroundHover();

            pointer.Dispatch(CreatePointer(new Point(16, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();

            AssertNoBackgroundHover();
            return;

            void AssertNoBackgroundHover()
            {
                modality.Active.ShouldBeNull();
                scope.IsActive.ShouldBeFalse();
                pointer.Hovered.ShouldBeNull();
                pointer.PressOrigin.ShouldBeNull();
                root.IsPointerOver.ShouldBeFalse();
                plane.IsPointerOver.ShouldBeFalse();
                background.IsPointerOver.ShouldBeFalse();
                rootEntries.ShouldBe(0);
                backgroundEntries.ShouldBe(0);
                backgroundRoutes.ShouldBe(0);
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entry independently clears outside capture, hover, and press state with a truthful reason.</summary>
    [Fact]
    public async Task Enter_WhenPointerStateIsOutsidePlane_CancelsEveryStateWithModalReasonAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 40, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 6, 6) };
            var captureOwner = new ProbeControl { Bounds = new Rect(8, 0, 6, 6) };
            var pressOwner = new ProbeControl { Bounds = new Rect(16, 0, 6, 6) };
            var hoverOwner = new ProbeControl { Bounds = new Rect(24, 0, 6, 6) };
            root.Children.Add(plane);
            root.Children.Add(captureOwner);
            root.Children.Add(pressOwner);
            root.Children.Add(hoverOwner);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            pointer.Dispatch(CreatePointer(new Point(18, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(pressOwner);
            pointer.Capture(captureOwner).ShouldBeTrue();
            pointer.Dispatch(CreatePointer(new Point(26, 2), PointerAction.Move)).ShouldBeSameAs(captureOwner);
            pointer.Captured.ShouldBeSameAs(captureOwner);
            pointer.Hovered.ShouldBeSameAs(hoverOwner);
            pointer.PressOrigin.ShouldBeSameAs(pressOwner);
            PointerCaptureLossReason? reason = null;
            captureOwner.LostPointerCapture += (_, eventArgs) => reason = eventArgs.Reason;

            using var scope = modality.Enter(plane);

            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            captureOwner.PointerStateWasClearDuringCancellation.ShouldBeTrue();
            reason.ShouldBe(PointerCaptureLossReason.ModalScopeChanged);
            captureOwner.IsPointerOver.ShouldBeFalse();
            pressOwner.IsPointerOver.ShouldBeFalse();
            hoverOwner.IsPointerOver.ShouldBeFalse();
            root.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cleanup callback failure cannot suppress later cancellation publication or strand the scope.</summary>
    [Fact]
    public async Task Enter_WhenHoverCleanupCallbackThrows_CompletesPointerCleanupAndRollsBackScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 32, 8) };
            var plane = new ProbeControl { Bounds = new Rect(0, 0, 6, 6) };
            var captureOwner = new ProbeControl { Bounds = new Rect(8, 0, 6, 6) };
            var pressOwner = new ProbeControl { Bounds = new Rect(16, 0, 6, 6) };
            var hoverOwner = new ProbeControl { Bounds = new Rect(24, 0, 6, 6) };
            root.Children.Add(plane);
            root.Children.Add(captureOwner);
            root.Children.Add(pressOwner);
            root.Children.Add(hoverOwner);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            pointer.Dispatch(CreatePointer(new Point(18, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeSameAs(pressOwner);
            pointer.Capture(captureOwner).ShouldBeTrue();
            pointer.Dispatch(CreatePointer(new Point(26, 2), PointerAction.Move)).ShouldBeSameAs(captureOwner);
            var failure = new InvalidOperationException("The hover exit callback failed.");
            hoverOwner.PointerExited += (_, _) => throw failure;
            var captureLosses = 0;
            PointerCaptureLossReason? reason = null;
            captureOwner.LostPointerCapture += (_, eventArgs) =>
            {
                captureLosses++;
                reason = eventArgs.Reason;
            };

            Should.Throw<InvalidOperationException>(() => modality.Enter(plane)).ShouldBeSameAs(failure);

            modality.Active.ShouldBeNull();
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            captureLosses.ShouldBe(1);
            reason.ShouldBe(PointerCaptureLossReason.ModalScopeChanged);
            root.IsPointerOver.ShouldBeFalse();
            hoverOwner.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Input replay semantics

    /// <summary>Verifies synchronous close cannot replay the dismissal press into the exposed background.</summary>
    [Fact]
    public async Task Dispatch_WhenDismissHandlerClosesScope_DoesNotReplayPressAsync()
    {
        var plane = new Button
        {
            Content = new ControlText("Modal"),
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(12));
        var root = new Overlay { Children = { plane, background } };
        var backgroundPresses = 0;
        _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Press)
            {
                backgroundPresses++;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 6),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        await surface.UpdateAsync(() =>
        {
            scope = surface.Application.Modality.Enter(plane, OutsideInteraction.Dismiss);
            scope.DismissRequested += (_, _) => scope.Dispose();
        }, "enter modal pointer scope");

        await surface.Pointer.ClickAsync(background);

        backgroundPresses.ShouldBe(0);
        scope.ShouldNotBeNull().IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies the application snapshot retains outside physical coordinates while delivery is blocked.</summary>
    [Fact]
    public async Task Dispatch_WhenOutsideMoveIsConsumed_PreservesApplicationPointerPositionAsync()
    {
        var plane = new Button
        {
            Content = new ControlText("Modal"),
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(10),
            Height = Length.Cells(3),
        };
        Overlay.SetLeft(background, Length.Cells(12));
        var root = new Overlay { Children = { plane, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => _ = surface.Application.Modality.Enter(plane),
            "enter modal pointer scope");
        var position = await surface.ResolvePointAsync(background);

        await surface.Pointer.MoveToAsync(background);

        surface.Application.Pointer.Position.ShouldBe(position);
        surface.Application.Pointer.Hovered.ShouldBeNull();
        background.IsPointerOver.ShouldBeFalse();
    }

    #endregion

    #region Test helpers

    private static Pointer CreatePointer(
        Point cells,
        PointerAction action,
        Buttons buttons = Buttons.None,
        int wheelX = 0,
        int wheelY = 0) =>
        new(
            cells,
            pixels: null,
            buttons,
            action,
            wheelX,
            wheelY,
            Modifiers.None,
            isMotion: action == PointerAction.Move,
            isCellPositionInferred: false);

    private static Pointer CreateLeavePointer() =>
        new(
            cells: null,
            pixels: null,
            Buttons.None,
            PointerAction.Leave,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false);

    #endregion
}
