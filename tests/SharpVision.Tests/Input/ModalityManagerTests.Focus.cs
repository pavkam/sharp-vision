// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies focus remains confined to, traverses within, and restores across modal planes.</summary>
public sealed partial class ModalityManagerTests
{
    #region Targeting, traversal, and entry

    /// <summary>Verifies explicit requests cannot escape an active plane and foreign targets still fail validation.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsOutsideActivePlane_RejectsWithoutCallbacksOrMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var inside = new ProbeControl { IsFocusable = true };
            var foreign = new ProbeControl { IsFocusable = true };
            plane.Children.Add(inside);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: inside);
            var callbacks = 0;
            focus.Changing += (_, _) => callbacks++;
            focus.Lost += (_, _) => callbacks++;
            focus.Gained += (_, _) => callbacks++;

            focus.Focus(background).ShouldBeFalse();
            focus.Focus(background, FocusReason.Pointer, cancellable: true).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => focus.Focus(foreign));

            focus.Focused.ShouldBeSameAs(inside);
            callbacks.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies null remains a valid explicit release while a plane is active.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsNull_ReleasesInsideActivePlaneAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            var inside = new ProbeControl { IsFocusable = true };
            plane.Children.Add(inside);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: inside);

            focus.Focus(null).ShouldBeTrue();

            focus.Focused.ShouldBeNull();
            inside.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies primary and included roots form one deterministic wrapping traversal plane.</summary>
    [Fact]
    public async Task MoveNext_WhenPlaneHasMultipleRoots_WrapsInDeclaredOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var firstRoot = new ProbeContainer();
            var first = new ProbeControl { IsFocusable = true };
            var secondRoot = new ProbeContainer();
            var second = new ProbeControl { IsFocusable = true };
            firstRoot.Children.Add(first);
            secondRoot.Children.Add(second);
            root.Children.Add(background);
            root.Children.Add(firstRoot);
            root.Children.Add(secondRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(firstRoot, initialFocus: first);
            scope.Include(secondRoot);
            var reasons = new List<FocusReason>();
            focus.Gained += (_, eventArgs) => reasons.Add(eventArgs.Reason);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);

            reasons.ShouldBe([FocusReason.Keyboard, FocusReason.Keyboard, FocusReason.Keyboard]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a local cycle remains authoritative without escaping the captured modal boundary.</summary>
    [Fact]
    public async Task MoveNext_WhenPlaneContainsLocalCycle_WrapsInsideLocalScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var outside = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var cycle = new ProbeContainer { TabNavigation = TabNavigation.Cycle };
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var after = new ProbeControl { IsFocusable = true };
            cycle.Children.Add(first);
            cycle.Children.Add(second);
            plane.Children.Add(cycle);
            plane.Children.Add(after);
            root.Children.Add(outside);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: second);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a local Once scope contributes one entry that participates in the enclosing
    /// plane's ordinary traversal instead of trapping Tab and Shift+Tab on it forever - Once is a
    /// contribution rule, not a traversal boundary, so <c>once</c> is not itself a scope root and
    /// its single contributed entry (<c>first</c>) cycles with <c>after</c> like any other pair of
    /// plane candidates. This test previously enshrined the pre-fix trap: it asserted every step
    /// landed back on <c>first</c>, including the very first MoveNext from the initially focused
    /// <c>second</c> - a control Once never contributes, which is a distinct, related defect this
    /// scope directly interacts with. Fixing that related defect first means <c>second</c>, not
    /// itself a candidate, resolves to the nearest following candidate (<c>after</c>) by tree
    /// order.</summary>
    [Fact]
    public async Task MoveNext_WhenPlaneContainsLocalOnce_CyclesTheContributedEntryWithinThePlaneAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            var once = new ProbeContainer { TabNavigation = TabNavigation.Once };
            var first = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var after = new ProbeControl { IsFocusable = true };
            once.Children.Add(first);
            once.Children.Add(second);
            plane.Children.Add(once);
            plane.Children.Add(after);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, initialFocus: second);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(after);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an empty active plane cannot traverse into application controls.</summary>
    [Fact]
    public async Task MoveNext_WhenActivePlaneHasNoCandidates_ReturnsFalseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane);

            focus.MoveNext().ShouldBeFalse();

            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entering a zero-focus plane clears an outside focus through observable non-cancellable events.</summary>
    [Fact]
    public async Task Enter_WhenPlaneHasNoFocusTarget_ClearsOutsideFocusNonCancellablyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var order = new List<string>();
            var observeEntry = true;
            focus.Changing += (_, eventArgs) =>
            {
                if (observeEntry)
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                    eventArgs.Cancel = true;
                    order.Add("changing");
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (observeEntry)
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                    order.Add("lost");
                }
            };

            using var scope = modality.Enter(plane);
            observeEntry = false;

            focus.Focused.ShouldBeNull();
            background.IsFocused.ShouldBeFalse();
            order.ShouldBe(["changing", "lost"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal entry ignores cancellation and publishes the complete reasoned event sequence.</summary>
    [Fact]
    public async Task Enter_WhenChangingCancels_CommitsInitialFocusWithProgrammaticReasonAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var order = new List<string>();
            var observeEntry = true;
            focus.Changing += (_, eventArgs) =>
            {
                if (observeEntry)
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                    eventArgs.Cancel = true;
                    order.Add("changing");
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (observeEntry)
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                    order.Add("lost");
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                if (observeEntry)
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Programmatic);
                    order.Add("gained");
                }
            };

            using var scope = modality.Enter(plane, initialFocus: initial);
            observeEntry = false;

            focus.Focused.ShouldBeSameAs(initial);
            order.ShouldBe(["changing", "lost", "gained"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entry selects a valid fallback when a callback invalidates the requested target.</summary>
    [Fact]
    public async Task Enter_WhenChangingInvalidatesInitialFocus_UsesValidFallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var fallback = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            plane.Children.Add(fallback);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            focus.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, initial))
                {
                    initial.IsFocusable = false;
                }
            };

            using var scope = modality.Enter(plane, initialFocus: initial);

            focus.Focused.ShouldBeSameAs(fallback);
            modality.Allows(focus.Focused).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed entry rolls committed focus back with Restore while preserving the first failure.</summary>
    [Fact]
    public async Task Enter_WhenGainedThrows_RollsBackFocusWithRestoreReasonAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var expected = new InvalidOperationException("entry failed");
            var order = new List<string>();
            focus.Changing += (_, eventArgs) => order.Add($"changing-{eventArgs.Reason}");
            focus.Lost += (_, eventArgs) => order.Add($"lost-{eventArgs.Reason}");
            focus.Gained += (_, eventArgs) =>
            {
                order.Add($"gained-{eventArgs.Reason}");

                if (ReferenceEquals(eventArgs.Current, initial))
                {
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                modality.Enter(plane, initialFocus: initial));

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            order.ShouldBe([
                "changing-Programmatic",
                "lost-Programmatic",
                "gained-Programmatic",
                "changing-Restore",
                "lost-Restore",
                "gained-Restore",
            ]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a deferred entry failure removes that scope and every younger scope before rethrow.</summary>
    [Fact]
    public async Task Enter_WhenQueuedFocusCallbackThrows_RollsBackCompleteEntryStackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var requested = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var youngerRoot = new ProbeContainer();
            var youngerFocus = new ProbeControl { IsFocusable = true };
            youngerRoot.Children.Add(youngerFocus);
            plane.Children.Add(initial);
            plane.Children.Add(youngerRoot);
            root.Children.Add(background);
            root.Children.Add(requested);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            pointer.Capture(background).ShouldBeTrue();
            var expected = new InvalidOperationException("queued entry failed");
            var later = new InvalidOperationException("younger exit failed");
            ModalScope? scope = null;
            ModalScope? younger = null;
            var gained = new List<ControlBase>();
            var exitOrder = new List<string>();
            var outerTracker = true;
            var youngerTracker = true;
            ModalScope? activeDuringYoungerExit = null;
            ControlBase? focusDuringYoungerExit = null;
            var outerWasInactiveDuringYoungerExit = false;
            var youngerWasInactiveDuringYoungerExit = false;
            var requestedWasFocusedDuringYoungerExit = false;
            var initialWasFocusedDuringYoungerExit = false;
            var youngerWasFocusedDuringYoungerExit = false;
            focus.Gained += (_, eventArgs) => gained.Add(eventArgs.Current.ShouldNotBeNull());
            focus.Changing += (_, eventArgs) =>
            {
                if (scope is null && ReferenceEquals(eventArgs.Next, requested))
                {
                    scope = modality.Enter(plane, initialFocus: initial);
                    scope.Exited += (_, _) =>
                    {
                        scope.IsActive.ShouldBeFalse();
                        modality.Active.ShouldBeNull();
                        focus.Focused.ShouldBeSameAs(background);
                        background.IsFocused.ShouldBeTrue();
                        initial.IsFocused.ShouldBeFalse();
                        outerTracker = false;
                        exitOrder.Add("outer");
                    };
                }
                else if (younger is null && ReferenceEquals(eventArgs.Next, initial))
                {
                    younger = modality.Enter(youngerRoot, initialFocus: youngerFocus);
                    younger.Exited += (_, _) =>
                    {
                        youngerWasInactiveDuringYoungerExit = !younger.IsActive;
                        outerWasInactiveDuringYoungerExit = !scope.ShouldNotBeNull().IsActive;
                        activeDuringYoungerExit = modality.Active;
                        focusDuringYoungerExit = focus.Focused;
                        requestedWasFocusedDuringYoungerExit = requested.IsFocused;
                        initialWasFocusedDuringYoungerExit = initial.IsFocused;
                        youngerWasFocusedDuringYoungerExit = youngerFocus.IsFocused;
                        youngerTracker = false;
                        exitOrder.Add("younger");
                        throw later;
                    };
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(() => focus.Focus(requested));

            thrown.ShouldBeSameAs(expected);
            var failed = scope.ShouldNotBeNull();
            var failedYounger = younger.ShouldNotBeNull();
            failed.IsActive.ShouldBeFalse();
            failedYounger.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            requested.IsFocused.ShouldBeFalse();
            initial.IsFocused.ShouldBeFalse();
            youngerFocus.IsFocused.ShouldBeFalse();
            gained.ShouldNotContain(youngerFocus);
            pointer.Captured.ShouldBeNull();
            outerTracker.ShouldBeFalse();
            youngerTracker.ShouldBeFalse();
            youngerWasInactiveDuringYoungerExit.ShouldBeTrue();
            outerWasInactiveDuringYoungerExit.ShouldBeTrue();
            activeDuringYoungerExit.ShouldBeNull();
            focusDuringYoungerExit.ShouldBeSameAs(background);
            requestedWasFocusedDuringYoungerExit.ShouldBeFalse();
            initialWasFocusedDuringYoungerExit.ShouldBeFalse();
            youngerWasFocusedDuringYoungerExit.ShouldBeFalse();
            exitOrder.ShouldBe(["younger", "outer"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ordinary queued focus cannot enter a rolled-back scope while a parent request survives.</summary>
    [Fact]
    public async Task Focus_WhenOrdinaryRequestOutlivesRolledBackEntry_RejectsStaleScopeContextAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var parentRoot = new ProbeContainer();
            var parentFocus = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            var parentAfter = new ProbeControl { IsFocusable = true };
            var childRoot = new ProbeContainer();
            var childInitial = new ProbeControl { IsFocusable = true };
            var childQueued = new ProbeControl { IsFocusable = true };
            childRoot.Children.Add(childInitial);
            childRoot.Children.Add(childQueued);
            parentRoot.Children.Add(parentFocus);
            parentRoot.Children.Add(trigger);
            parentRoot.Children.Add(parentAfter);
            parentRoot.Children.Add(childRoot);
            root.Children.Add(parentRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot, initialFocus: parentFocus);
            var expected = new InvalidOperationException("child entry failed");
            var gained = new List<ControlBase>();
            ModalScope? child = null;
            var parentRequestQueued = false;
            focus.Changing += (_, eventArgs) =>
            {
                if (child is null && ReferenceEquals(eventArgs.Next, trigger))
                {
                    child = modality.Enter(childRoot, initialFocus: childInitial);
                }
                else if (!parentRequestQueued && ReferenceEquals(eventArgs.Next, parentFocus))
                {
                    parentRequestQueued = true;
                    focus.Focus(parentAfter).ShouldBeFalse();
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                var current = eventArgs.Current.ShouldNotBeNull();
                gained.Add(current);

                if (ReferenceEquals(current, childInitial))
                {
                    focus.Focus(childQueued).ShouldBeFalse();
                    throw expected;
                }
            };

            var thrown = Should.Throw<InvalidOperationException>(() => focus.Focus(trigger));

            thrown.ShouldBeSameAs(expected);
            var failed = child.ShouldNotBeNull();
            failed.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeSameAs(parent);
            parentRequestQueued.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(parentAfter);
            childQueued.IsFocused.ShouldBeFalse();
            gained.ShouldBe([childInitial, parentFocus, parentAfter]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a queued modal target that detaches completes through the next eligible plane target.</summary>
    [Fact]
    public async Task Enter_WhenQueuedInitialFocusDetaches_CompletesWithFallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var requested = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var fallback = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            plane.Children.Add(fallback);
            root.Children.Add(background);
            root.Children.Add(requested);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            ModalScope? scope = null;
            FocusReason? gainedReason = null;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, fallback))
                {
                    gainedReason = eventArgs.Reason;
                }
            };
            focus.Changing += (_, eventArgs) =>
            {
                if (scope is null && ReferenceEquals(eventArgs.Next, requested))
                {
                    scope = modality.Enter(plane, initialFocus: initial);
                    plane.Children.Remove(initial).ShouldBeTrue();
                }
            };

            focus.Focus(requested).ShouldBeFalse();

            var active = scope.ShouldNotBeNull();
            active.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(active);
            focus.Focused.ShouldBeSameAs(fallback);
            background.IsFocused.ShouldBeFalse();
            gainedReason.ShouldBe(FocusReason.Programmatic);
            active.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a queued modal target that becomes ineligible clears outside focus when no fallback exists.</summary>
    [Fact]
    public async Task Enter_WhenQueuedInitialFocusBecomesIneligible_ClearsOutsideFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var requested = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            root.Children.Add(background);
            root.Children.Add(requested);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            ModalScope? scope = null;
            focus.Changing += (_, eventArgs) =>
            {
                if (scope is null && ReferenceEquals(eventArgs.Next, requested))
                {
                    scope = modality.Enter(plane, initialFocus: initial);
                    initial.IsFocusable = false;
                }
            };

            focus.Focus(requested).ShouldBeFalse();

            var active = scope.ShouldNotBeNull();
            active.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(active);
            focus.Focused.ShouldBeNull();
            background.IsFocused.ShouldBeFalse();
            active.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal fallback tries each candidate once while retaining descendant search.</summary>
    [Fact]
    public async Task Enter_WhenFallbackCandidatesToggle_DoesNotRetryAttemptedTargetsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            var first = new ProbeContainer { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            var final = new ProbeControl { IsFocusable = true };
            first.Children.Add(second);
            plane.Children.Add(first);
            plane.Children.Add(final);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var observed = new List<ControlBase>();
            focus.Changing += (_, eventArgs) =>
            {
                if (eventArgs.Next is not { } target)
                {
                    return;
                }

                observed.ShouldNotContain(target);
                observed.Add(target);

                if (ReferenceEquals(target, first))
                {
                    first.IsFocusable = false;
                }
                else if (ReferenceEquals(target, second))
                {
                    first.IsFocusable = true;
                    second.IsFocusable = false;
                }
            };

            using var scope = modality.Enter(plane, initialFocus: first);

            focus.Focused.ShouldBeSameAs(final);
            observed.ShouldBe([first, second, final]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant entry into an empty plane defers one null request and clears outside focus.</summary>
    [Fact]
    public async Task Enter_WhenReentrantPlaneHasNoFocusTargets_ClearsOutsideFocusAfterDrainAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var requested = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            root.Children.Add(background);
            root.Children.Add(requested);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            ModalScope? scope = null;
            var entering = false;
            focus.Changing += (_, eventArgs) =>
            {
                if (!entering && ReferenceEquals(eventArgs.Next, requested))
                {
                    entering = true;
                    scope = modality.Enter(plane);
                }
            };

            focus.Focus(requested).ShouldBeFalse();

            var active = scope.ShouldNotBeNull();
            active.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(active);
            focus.Focused.ShouldBeNull();
            background.IsFocused.ShouldBeFalse();
            active.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Restoration and teardown

    /// <summary>Verifies nested exit restores before Exited and cancellation cannot retain child focus.</summary>
    [Fact]
    public async Task Dispose_WhenNestedScopeExits_RestoresParentBeforeExitedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            focus.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, outerFocus))
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Restore);
                    eventArgs.Cancel = true;
                    order.Add("changing");
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Previous, innerFocus))
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Restore);
                    order.Add("lost");
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, outerFocus))
                {
                    eventArgs.Reason.ShouldBe(FocusReason.Restore);
                    order.Add("gained");
                }
            };
            inner.Exited += (_, _) =>
            {
                focus.Focused.ShouldBeSameAs(outerFocus);
                order.Add("exited");
            };

            inner.Dispose();

            focus.Focused.ShouldBeSameAs(outerFocus);
            order.ShouldBe(["changing", "lost", "gained", "exited"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed restore preview repairs manager and control facts before committed exit publication.</summary>
    [Fact]
    public async Task Dispose_WhenRestoreChangingThrows_RepairsFocusBeforeExitedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var expected = new InvalidOperationException("The restore preview failed.");
            var later = new InvalidOperationException("The later exit callback failed.");
            var order = new List<string>();
            focus.Changing += (_, eventArgs) =>
            {
                if (eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Next, outerFocus))
                {
                    order.Add("changing");
                    throw expected;
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (eventArgs.Reason == FocusReason.Restore)
                {
                    order.Add("lost");
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                if (eventArgs.Reason == FocusReason.Restore)
                {
                    order.Add("gained");
                }
            };
            inner.Exited += (_, _) =>
            {
                inner.IsActive.ShouldBeFalse();
                modality.Active.ShouldBeSameAs(outer);
                focus.Focused.ShouldBeSameAs(outerFocus);
                outerFocus.IsFocused.ShouldBeTrue();
                innerFocus.IsFocused.ShouldBeFalse();
                order.Add("exited");
                throw later;
            };

            var thrown = Should.Throw<InvalidOperationException>(inner.Dispose);

            thrown.ShouldBeSameAs(expected);
            focus.Focused.ShouldBeSameAs(outerFocus);
            outerFocus.IsFocused.ShouldBeTrue();
            innerFocus.IsFocused.ShouldBeFalse();
            order.ShouldBe(["changing", "lost", "gained", "exited"]);
            inner.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant disposal waits for a queued restore before publishing the committed exit.</summary>
    /// <param name="callback">The focus callback that requests disposal.</param>
    [Theory]
    [InlineData("changing")]
    [InlineData("lost")]
    [InlineData("gained")]
    public async Task Dispose_WhenCalledFromFocusCallback_DefersExitedUntilRestoreCompletesAsync(
        string callback)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            var nextInner = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            innerRoot.Children.Add(nextInner);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            var exited = 0;

            void DisposeFromCallback()
            {
                order.Add("dispose");
                inner.Dispose();
                inner.IsActive.ShouldBeFalse();
                exited.ShouldBe(0);
                order.Add("returned");
            }

            focus.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, nextInner) && callback == "changing")
                {
                    DisposeFromCallback();
                }
                else if (eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Next, outerFocus))
                {
                    order.Add("restore-changing");
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, nextInner) && callback == "lost")
                {
                    DisposeFromCallback();
                }
                else if (eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Current, outerFocus))
                {
                    order.Add("restore-lost");
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, nextInner) && callback == "gained")
                {
                    DisposeFromCallback();
                }
                else if (eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Current, outerFocus))
                {
                    order.Add("restore-gained");
                }
            };
            inner.Exited += (_, _) =>
            {
                exited++;
                focus.Focused.ShouldBeSameAs(outerFocus);
                outerFocus.IsFocused.ShouldBeTrue();
                innerFocus.IsFocused.ShouldBeFalse();
                nextInner.IsFocused.ShouldBeFalse();
                order.Add("exited");
            };

            _ = focus.Focus(nextInner);

            exited.ShouldBe(1);
            inner.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeSameAs(outer);
            focus.Focused.ShouldBeSameAs(outerFocus);
            outerFocus.IsFocused.ShouldBeTrue();
            innerFocus.IsFocused.ShouldBeFalse();
            nextInner.IsFocused.ShouldBeFalse();
            order.IndexOf("dispose").ShouldBeGreaterThanOrEqualTo(0);
            order.IndexOf("returned").ShouldBeGreaterThan(order.IndexOf("dispose"));
            order.IndexOf("restore-changing").ShouldBeGreaterThan(order.IndexOf("returned"));
            order.IndexOf("restore-lost").ShouldBeGreaterThan(order.IndexOf("restore-changing"));
            order.IndexOf("restore-gained").ShouldBeGreaterThan(order.IndexOf("restore-lost"));
            order.IndexOf("exited").ShouldBeGreaterThan(order.IndexOf("restore-gained"));
            inner.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing an older scope commits the complete requested batch before a focus callback returns.</summary>
    [Fact]
    public async Task Dispose_WhenOlderScopeEndsFromFocusCallback_CommitsBatchBeforeReturningAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            innerRoot.Children.Add(trigger);
            root.Children.Add(background);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            var innerExited = 0;
            var outerExited = 0;
            inner.Exited += (_, _) =>
            {
                innerExited++;
                order.Add("inner");
            };
            outer.Exited += (_, _) =>
            {
                outerExited++;
                order.Add("outer");
            };
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger))
                {
                    return;
                }

                outer.Dispose();
                outer.IsActive.ShouldBeFalse();
                inner.IsActive.ShouldBeFalse();
                modality.Active.ShouldBeNull();
                innerExited.ShouldBe(0);
                outerExited.ShouldBe(0);
                order.Add("returned");
            };

            _ = focus.Focus(trigger);

            outer.IsActive.ShouldBeFalse();
            inner.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            innerExited.ShouldBe(1);
            outerExited.ShouldBe(1);
            order.ShouldBe(["returned", "inner", "outer"]);
            focus.Focused.ShouldBeSameAs(background);
            background.IsFocused.ShouldBeTrue();
            outerFocus.IsFocused.ShouldBeFalse();
            innerFocus.IsFocused.ShouldBeFalse();
            trigger.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing focus during modal restoration still completes scope publication coherently.</summary>
    [Fact]
    public async Task Dispose_WhenFocusManagerEndsDuringRestoreChanging_PublishesExitWithClearedFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var modality = new ModalityManager(root, focus, pointer);
            var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var exited = 0;
            var disposing = false;
            inner.Exited += (_, _) => exited++;
            focus.Changing += (_, eventArgs) =>
            {
                if (!disposing &&
                    eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Next, outerFocus))
                {
                    disposing = true;
                    focus.Dispose();
                }
            };

            inner.Dispose();

            exited.ShouldBe(1);
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(outer);
            focus.Focused.ShouldBeNull();
            outerFocus.IsFocused.ShouldBeFalse();
            innerFocus.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();
            outerRoot.FocusOwner.ShouldBeNull();
            innerRoot.FocusOwner.ShouldBeNull();
            _ = Should.Throw<ObjectDisposedException>(() => focus.Focus(outerFocus));

            modality.Shutdown();
            outer.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an earlier deferred unwind failure outranks a later focus-disposal callback failure.</summary>
    [Fact]
    public async Task Dispose_WhenFocusCleanupFailsAfterDeferredUnwind_PreservesEarlierFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var parentRoot = new ProbeContainer();
            var parentFocus = new ProbeControl { IsFocusable = true };
            var childRoot = new ProbeContainer();
            var childFocus = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            parentRoot.Children.Add(parentFocus);
            childRoot.Children.Add(childFocus);
            childRoot.Children.Add(trigger);
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var parent = modality.Enter(parentRoot, initialFocus: parentFocus);
            var child = modality.Enter(childRoot, initialFocus: childFocus);
            pointer.Capture(childFocus).ShouldBeTrue();
            var pointerFailure = new InvalidOperationException("Pointer reconciliation failed first.");
            var disposalFailure = new InvalidOperationException("Focus disposal failed later.");
            var exited = 0;
            var disposalFailures = 0;
            child.Exited += (_, _) => exited++;
            childFocus.LostPointerCapture += (_, _) => throw pointerFailure;
            childFocus.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.IsFocused) && !childFocus.IsFocused)
                {
                    disposalFailures++;
                    throw disposalFailure;
                }
            };
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger))
                {
                    return;
                }

                child.Dispose();
                focus.Dispose();
            };

            var thrown = Should.Throw<InvalidOperationException>(() => focus.Focus(trigger));

            thrown.ShouldBeSameAs(pointerFailure);
            exited.ShouldBe(1);
            disposalFailures.ShouldBe(1);
            child.IsActive.ShouldBeFalse();
            parent.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(parent);
            pointer.Captured.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            parentFocus.IsFocused.ShouldBeFalse();
            childFocus.IsFocused.ShouldBeFalse();
            trigger.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();

            modality.Shutdown();
            parent.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a deferred unwind failure outranks a later exception from its enclosing focus callback.</summary>
    [Fact]
    public async Task Dispose_WhenEnclosingFocusCallbackFailsLater_PreservesDeferredUnwindFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var parentRoot = new ProbeContainer();
            var parentFocus = new ProbeControl { IsFocusable = true };
            var childRoot = new ProbeContainer();
            var childFocus = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            parentRoot.Children.Add(parentFocus);
            childRoot.Children.Add(childFocus);
            childRoot.Children.Add(trigger);
            root.Children.Add(parentRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var parent = modality.Enter(parentRoot, initialFocus: parentFocus);
            var child = modality.Enter(childRoot, initialFocus: childFocus);
            pointer.Capture(childFocus).ShouldBeTrue();
            var pointerFailure = new InvalidOperationException("Pointer reconciliation failed first.");
            var handlerFailure = new InvalidOperationException("The enclosing focus callback failed later.");
            var exited = 0;
            child.Exited += (_, _) => exited++;
            childFocus.LostPointerCapture += (_, _) => throw pointerFailure;
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger))
                {
                    return;
                }

                child.Dispose();
                throw handlerFailure;
            };

            var thrown = Should.Throw<InvalidOperationException>(() => focus.Focus(trigger));

            thrown.ShouldBeSameAs(pointerFailure);
            exited.ShouldBe(1);
            child.IsActive.ShouldBeFalse();
            parent.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(parent);
            pointer.Captured.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(parentFocus);
            parentFocus.IsFocused.ShouldBeTrue();
            childFocus.IsFocused.ShouldBeFalse();
            trigger.IsFocused.ShouldBeFalse();

            parent.Dispose();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope entered by restore notification drains before the requested exit publishes.</summary>
    /// <param name="callback">The restore callback that enters the reentrant scope.</param>
    [Theory]
    [InlineData("changing")]
    [InlineData("state")]
    [InlineData("lost")]
    [InlineData("gained")]
    public async Task Dispose_WhenRestoreCallbackEntersScope_DrainsItBeforeRequestedExitAsync(
        string callback)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var reentrantRoot = new ProbeContainer();
            plane.Children.Add(initial);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Children.Add(reentrantRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var requested = modality.Enter(plane, initialFocus: initial);
            ModalScope? reentrant = null;
            var reentrantWasInactiveDuringExit = false;
            var order = new List<string>();
            requested.Exited += (_, _) => order.Add("requested");

            void EnterReentrant()
            {
                if (reentrant is not null)
                {
                    return;
                }

                reentrant = modality.Enter(reentrantRoot);
                reentrant.Exited += (_, _) =>
                {
                    reentrantWasInactiveDuringExit = !reentrant.IsActive;
                    order.Add("reentrant");
                };
            }

            focus.Changing += (_, eventArgs) =>
            {
                if (callback == "changing" &&
                    eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Next, background))
                {
                    EnterReentrant();
                }
            };
            background.PropertyChanged += (_, eventArgs) =>
            {
                if (callback == "state" &&
                    eventArgs.PropertyName == nameof(ControlBase.IsFocused) &&
                    background.IsFocused)
                {
                    EnterReentrant();
                }
            };
            focus.Lost += (_, eventArgs) =>
            {
                if (callback == "lost" &&
                    eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Current, background))
                {
                    EnterReentrant();
                }
            };
            focus.Gained += (_, eventArgs) =>
            {
                if (callback == "gained" &&
                    eventArgs.Reason == FocusReason.Restore &&
                    ReferenceEquals(eventArgs.Current, background))
                {
                    EnterReentrant();
                }
            };

            requested.Dispose();

            requested.IsActive.ShouldBeFalse();
            reentrant.ShouldNotBeNull().IsActive.ShouldBeFalse();
            reentrantWasInactiveDuringExit.ShouldBeTrue();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            background.IsFocused.ShouldBeTrue();
            initial.IsFocused.ShouldBeFalse();
            order.ShouldBe(["reentrant", "requested"]);
            reentrant.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies focus disposal cancels and rolls back a modal entry queued by the enclosing transaction.</summary>
    [Fact]
    public async Task Enter_WhenQueuedEntryFocusIsCanceledByFocusDisposal_RollsBackScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            root.Children.Add(background);
            root.Children.Add(trigger);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            ModalScope? scope = null;
            var exited = 0;
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger) || scope is not null)
                {
                    return;
                }

                scope = modality.Enter(plane, initialFocus: initial);
                scope.Exited += (_, _) => exited++;
                focus.Dispose();
            };

            focus.Focus(trigger).ShouldBeFalse();

            var rolledBack = scope.ShouldNotBeNull();
            rolledBack.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            exited.ShouldBe(1);
            focus.Focused.ShouldBeNull();
            background.IsFocused.ShouldBeFalse();
            trigger.IsFocused.ShouldBeFalse();
            initial.IsFocused.ShouldBeFalse();
            root.FocusOwner.ShouldBeNull();
            rolledBack.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies shutdown can strengthen a manager disposal while its focus restore remains queued.</summary>
    [Fact]
    public async Task Dispose_WhenDeferredRestoreIsStrengthenedByShutdown_DoesNotRestoreBackgroundAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            plane.Children.Add(trigger);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = modality.Enter(plane, initialFocus: initial);
            var exited = 0;
            var backgroundRestorations = 0;
            scope.Exited += (_, _) => exited++;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, background))
                {
                    backgroundRestorations++;
                }
            };
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger))
                {
                    return;
                }

                modality.Dispose();
                scope.IsActive.ShouldBeFalse();
                exited.ShouldBe(0);
                modality.Shutdown();
            };

            _ = focus.Focus(trigger);

            exited.ShouldBe(1);
            backgroundRestorations.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            root.ModalityOwner.ShouldBeNull();
            background.ModalityOwner.ShouldBeNull();
            background.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies manager-root unavailability strengthens a pending exit even after its scope was popped.</summary>
    [Fact]
    public async Task Dispose_WhenRootUnavailableStrengthensPendingExit_DoesNotRestoreReenabledBackgroundAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            var trigger = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            plane.Children.Add(trigger);
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            var scope = modality.Enter(plane, initialFocus: initial);
            var exited = 0;
            var backgroundRestorations = 0;
            scope.Exited += (_, _) => exited++;
            focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, background))
                {
                    backgroundRestorations++;
                }
            };
            focus.Changing += (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Next, trigger))
                {
                    return;
                }

                scope.Dispose();
                scope.IsActive.ShouldBeFalse();
                exited.ShouldBe(0);
                root.Visibility = Visibility.Hidden;
                root.Visibility = Visibility.Visible;
            };

            _ = focus.Focus(trigger);

            exited.ShouldBe(1);
            backgroundRestorations.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(trigger);
            trigger.IsFocused.ShouldBeTrue();
            background.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an invalid saved target falls back only within the newly active parent plane.</summary>
    [Fact]
    public async Task Dispose_WhenSavedTargetIsUnavailable_UsesParentPlaneFallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var unrelated = new ProbeControl { IsFocusable = true };
            var outerRoot = new ProbeContainer();
            var saved = new ProbeControl { IsFocusable = true };
            var fallback = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(saved);
            outerRoot.Children.Add(fallback);
            outerRoot.Children.Add(innerRoot);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(unrelated);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: saved);
            var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            saved.Visibility = Visibility.Hidden;

            inner.Dispose();

            focus.Focused.ShouldBeSameAs(fallback);
            focus.Focused.ShouldNotBeSameAs(unrelated);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies final exit clears focus rather than selecting unrelated application focus.</summary>
    [Fact]
    public async Task Dispose_WhenSavedApplicationTargetIsUnavailable_ClearsFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var saved = new ProbeControl { IsFocusable = true };
            var unrelated = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            root.Children.Add(saved);
            root.Children.Add(unrelated);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(saved).ShouldBeTrue();
            var scope = modality.Enter(plane, initialFocus: initial);
            saved.IsFocusable = false;

            scope.Dispose();

            focus.Focused.ShouldBeNull();
            unrelated.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary press reports pointer as the focus reason inside the active plane.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerFocusesInsideActivePlane_ReportsPointerReasonAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var plane = new ProbeContainer { Bounds = new Rect(5, 2, 10, 6) };
            var inside = new ProbeControl
            {
                Bounds = new Rect(6, 3, 4, 2),
                IsFocusable = true,
            };
            plane.Children.Add(inside);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane);
            focus.Focus(null).ShouldBeTrue();
            FocusReason? reason = null;
            focus.Gained += (_, eventArgs) => reason = eventArgs.Reason;
            var input = new Pointer(
                new Point(7, 4),
                null,
                Buttons.Primary,
                PointerAction.Press,
                0,
                0,
                Modifiers.None,
                true,
                false);

            pointer.Dispatch(input).ShouldBeSameAs(inside);

            focus.Focused.ShouldBeSameAs(inside);
            reason.ShouldBe(FocusReason.Pointer);
        }, TestContext.Current.CancellationToken);
    }

    #endregion
}
