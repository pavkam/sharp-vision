// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies modal-plane validation, ownership, stacking, and cleanup.</summary>
public sealed class ModalityManagerTests
{
    /// <summary>Verifies unhandled in-plane wheel input completes the active outside-interaction policy.</summary>
    [Theory]
    [InlineData(OutsideInteraction.Ignore, false, 0)]
    [InlineData(OutsideInteraction.Ignore, true, 0)]
    [InlineData(OutsideInteraction.Dismiss, false, 1)]
    [InlineData(OutsideInteraction.Dismiss, true, 0)]
    public async Task Pointer_WhenInPlaneWheelCompletesRoute_AppliesPolicyOnlyIfUnhandledAsync(
        OutsideInteraction policy,
        bool handleWheel,
        int expectedDismissals)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var plane = new ProbeControl { Bounds = new Rect(2, 2, 10, 5) };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, policy);
            var dismissals = 0;
            scope.DismissRequested += (_, _) => dismissals++;
            _ = plane.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Wheel)
                {
                    eventArgs.IsHandled = handleWheel;
                }
            });

            _ = pointer.Dispatch(new Pointer(
                new Point(3, 3),
                pixels: null,
                Buttons.None,
                PointerAction.Wheel,
                wheelX: 0,
                wheelY: 1,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));

            dismissals.ShouldBe(expectedDismissals);
        }, TestContext.Current.CancellationToken);
    }

    #region Construction and plane validation

    /// <summary>Verifies constructor dependencies must describe one attached application tree.</summary>
    [Fact]
    public async Task Constructor_WhenDependenciesAreInvalid_RejectsBeforeOwnershipChangesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var other = new ProbeContainer();
            root.Attach(dispatcher);
            other.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var otherFocus = new FocusManager(other);
            using var otherPointer = new PointerManager(other);

            _ = Should.Throw<ArgumentNullException>(() => new ModalityManager(null!, focus, pointer));
            _ = Should.Throw<ArgumentNullException>(() => new ModalityManager(root, null!, pointer));
            _ = Should.Throw<ArgumentNullException>(() => new ModalityManager(root, focus, null!));
            _ = Should.Throw<ArgumentException>(() => new ModalityManager(root, otherFocus, pointer));
            _ = Should.Throw<ArgumentException>(() => new ModalityManager(root, focus, otherPointer));

            root.ModalityOwner.ShouldBeNull();
            other.ModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disjoint scopes stack and disposing an older scope unwinds youngest first.</summary>
    [Fact]
    public async Task Enter_WhenRootsAreDisjoint_TracksYoungestScopeAndUnwindsInReverseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeContainer { IsFocusable = true };
            var second = new ProbeContainer { IsFocusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var order = new List<string>();
            using var outer = modality.Enter(first);
            outer.Exited += (_, _) => order.Add("outer");
            using var inner = modality.Enter(second, OutsideInteraction.Dismiss);
            inner.Exited += (_, _) => order.Add("inner");

            outer.Dispose();

            modality.Active.ShouldBeNull();
            outer.IsActive.ShouldBeFalse();
            inner.IsActive.ShouldBeFalse();
            order.ShouldBe(["inner", "outer"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies manager disposal from pointer reconfinement ends modal entry observably
    /// instead of returning an already-inactive scope as a successful transaction.</summary>
    [Fact]
    public async Task Enter_WhenCaptureLossDisposesManager_DoesNotReturnInactiveScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var background = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var plane = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var modality = new ModalityManager(root, focus, pointer);
            pointer.Capture(background).ShouldBeTrue();
            background.LostPointerCapture += (_, _) => modality.Dispose();

            // Act and assert
            _ = Should.Throw<ObjectDisposedException>(() => modality.Enter(plane));
            modality.Active.ShouldBeNull();
            root.ModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies manager disposal from focus-changing publication ends modal entry
    /// observably instead of returning the inactive scope cleared by that callback.</summary>
    [Fact]
    public async Task Enter_WhenFocusChangingDisposesManager_DoesNotReturnInactiveScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeControl { Bounds = new Rect(12, 0, 8, 6), IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var modality = new ModalityManager(root, focus, pointer);
            focus.Changing += (_, _) => modality.Dispose();

            // Act and assert
            _ = Should.Throw<ObjectDisposedException>(() => modality.Enter(plane, initialFocus: plane));
            modality.Active.ShouldBeNull();
            root.ModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a nested scope may root inside a suspended outer plane and restores outer focus.</summary>
    [Fact]
    public async Task Enter_WhenRootIsInsideOlderPlane_AllowsNestedScopeAndRestoresOuterFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            innerRoot.Children.Add(innerFocus);
            outerRoot.Children.Add(outerFocus);
            outerRoot.Children.Add(innerRoot);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);

            using var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            focus.Focused.ShouldBeSameAs(innerFocus);

            inner.Dispose();

            modality.Active.ShouldBeSameAs(outer);
            outer.IsActive.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(outerFocus);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies enter rejects every invalid root and policy before stack mutation.</summary>
    [Fact]
    public async Task Enter_WhenRootIsForeignHiddenDisabledOrDisposed_RejectsBeforeStateChangesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var valid = new ProbeContainer();
            var hidden = new ProbeContainer { Visibility = Visibility.Hidden };
            var disabled = new ProbeContainer { IsEnabled = false };
            var duplicate = new ProbeContainer();
            var foreign = new ProbeContainer();
            var disposed = new ProbeContainer();
            root.Children.Add(valid);
            root.Children.Add(hidden);
            root.Children.Add(disabled);
            root.Children.Add(duplicate);
            root.Attach(dispatcher);
            foreign.Attach(dispatcher);
            disposed.Dispose();
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            _ = Should.Throw<ArgumentNullException>(() => modality.Enter(null!));
            _ = Should.Throw<ArgumentOutOfRangeException>(() =>
                modality.Enter(valid, (OutsideInteraction) int.MaxValue));
            _ = Should.Throw<ArgumentException>(() => modality.Enter(foreign));
            _ = Should.Throw<ArgumentException>(() => modality.Enter(hidden));
            _ = Should.Throw<ArgumentException>(() => modality.Enter(disabled));
            _ = Should.Throw<ObjectDisposedException>(() => modality.Enter(disposed));
            using var scope = modality.Enter(duplicate);
            _ = Should.Throw<ArgumentException>(() => modality.Enter(duplicate));

            modality.Active.ShouldBeSameAs(scope);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies explicit initial focus must be an eligible member of the proposed plane.</summary>
    [Fact]
    public async Task Enter_WhenInitialFocusIsInvalid_RejectsBeforeStateChangesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            var eligible = new ProbeControl { IsFocusable = true };
            var ineligible = new ProbeControl();
            var foreign = new ProbeControl { IsFocusable = true };
            plane.Children.Add(eligible);
            plane.Children.Add(ineligible);
            root.Children.Add(plane);
            root.Children.Add(foreign);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            _ = Should.Throw<ArgumentException>(() => modality.Enter(plane, initialFocus: foreign));
            _ = Should.Throw<ArgumentException>(() => modality.Enter(plane, initialFocus: ineligible));

            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disjoint included root joins the active plane in insertion order.</summary>
    [Fact]
    public async Task Include_WhenRootIsDisjoint_AddsItInDeterministicOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var primary = new ProbeContainer();
            var included = new ProbeContainer();
            var leaf = new ProbeControl();
            included.Children.Add(leaf);
            root.Children.Add(primary);
            root.Children.Add(included);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary);

            scope.Include(included);

            modality.ActiveRootCount.ShouldBe(2);
            modality.ActiveRootAt(0).ShouldBeSameAs(primary);
            modality.ActiveRootAt(1).ShouldBeSameAs(included);
            modality.Allows(leaf).ShouldBeTrue();
            modality.BoundaryFor(leaf).ShouldBeSameAs(included);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies foreign, duplicate, overlapping, and cross-plane include roots are rejected atomically.</summary>
    [Fact]
    public async Task Include_WhenRootIsForeignOrOverlapping_RejectsBeforeStateChangesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var primary = new ProbeContainer();
            var child = new ProbeControl();
            var other = new ProbeContainer();
            var foreign = new ProbeContainer();
            primary.Children.Add(child);
            root.Children.Add(primary);
            root.Children.Add(other);
            root.Attach(dispatcher);
            foreign.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary);

            _ = Should.Throw<ArgumentNullException>(() => scope.Include(null!));
            _ = Should.Throw<ArgumentException>(() => scope.Include(foreign));
            _ = Should.Throw<ArgumentException>(() => scope.Include(primary));
            _ = Should.Throw<ArgumentException>(() => scope.Include(child));
            using var nested = modality.Enter(other);
            _ = Should.Throw<ArgumentException>(() => scope.Include(other));

            modality.Active.ShouldBeSameAs(nested);
            scope.IsActive.ShouldBeTrue();
            modality.ActiveRootCount.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Unavailable subtree handling

    /// <summary>Verifies an unavailable ancestor of a primary root unwinds that scope and every younger scope.</summary>
    [Fact]
    public async Task Unavailable_WhenAncestorContainsPrimaryRoot_UnwindsScopeAndYoungerScopesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var ancestor = new ProbeContainer();
            var primary = new ProbeContainer();
            var younger = new ProbeContainer();
            ancestor.Children.Add(primary);
            root.Children.Add(ancestor);
            root.Children.Add(younger);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(primary);
            using var inner = modality.Enter(younger);
            var order = new List<string>();
            outer.Exited += (_, _) => order.Add("outer");
            inner.Exited += (_, _) => order.Add("inner");

            ancestor.Visibility = Visibility.Hidden;

            modality.Active.ShouldBeNull();
            outer.IsActive.ShouldBeFalse();
            inner.IsActive.ShouldBeFalse();
            order.ShouldBe(["inner", "outer"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies losing a secondary included subtree removes only that root from the active plane.</summary>
    [Fact]
    public async Task Unavailable_WhenIncludedRootIsLost_RemovesOnlyThatRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var primary = new ProbeContainer();
            var includedAncestor = new ProbeContainer();
            var included = new ProbeContainer();
            var leaf = new ProbeControl();
            included.Children.Add(leaf);
            includedAncestor.Children.Add(included);
            root.Children.Add(primary);
            root.Children.Add(includedAncestor);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary);
            scope.Include(included);

            includedAncestor.IsEnabled = false;

            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            modality.ActiveRootCount.ShouldBe(1);
            modality.ActiveRootAt(0).ShouldBeSameAs(primary);
            modality.Allows(leaf).ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exit callback cannot resurrect a root during its complete unavailable transition.</summary>
    /// <param name="mutation">The unavailable transition applied to the modal root.</param>
    [Theory]
    [InlineData("removed")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task Unavailable_WhenExitedReentersDyingRoot_RejectsEntryAndCompletesMutationAsync(
        string mutation)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var dying = new OwnershipObserverControl { IsFocusable = true };
            var unrelated = new ProbeControl { IsFocusable = true };
            root.Children.Add(dying);
            root.Children.Add(unrelated);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(dying, initialFocus: dying);
            var expected = new InvalidOperationException("The exit callback failed.");
            Exception? reentryFailure = null;
            ModalScope? reentered = null;
            ModalScope? replacement = null;
            scope.Exited += (_, _) =>
            {
                scope.IsActive.ShouldBeFalse();
                RestoreAvailabilityForReentry(dying, mutation);

                try
                {
                    reentered = modality.Enter(dying, initialFocus: dying);
                }
                catch (Exception exception)
                {
                    reentryFailure = exception;
                }

                replacement = modality.Enter(unrelated, initialFocus: unrelated);
                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                MakeUnavailable(root, dying, mutation));

            thrown.ShouldBeSameAs(expected);
            _ = reentryFailure.ShouldBeOfType<ArgumentException>();
            reentered.ShouldBeNull();
            scope.IsActive.ShouldBeFalse();
            dying.IsFocused.ShouldBeFalse();
            dying.UnavailableReasons.ShouldBe([UnavailableReasonFor(mutation)]);
            var surviving = replacement.ShouldNotBeNull();
            modality.Active.ShouldBeSameAs(surviving);
            focus.Focused.ShouldBeSameAs(unrelated);
            unrelated.IsFocused.ShouldBeTrue();

            surviving.Dispose();

            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the manager-root guard remains active while a detach callback publishes scope exit.</summary>
    [Fact]
    public async Task Detach_WhenExitedReentersDyingTree_RejectsEntryBeforeContextSeversAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeControl { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(plane, initialFocus: plane);
            var expected = new InvalidOperationException("The detach exit callback failed.");
            Exception? reentryFailure = null;
            scope.Exited += (_, _) =>
            {
                try
                {
                    var reentered = modality.Enter(plane, initialFocus: plane);
                    reentered.Dispose();
                }
                catch (Exception exception)
                {
                    reentryFailure = exception;
                }

                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(root.Detach);

            thrown.ShouldBeSameAs(expected);
            _ = reentryFailure.ShouldBeOfType<ArgumentException>();
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            plane.IsFocused.ShouldBeFalse();
            plane.Dispatcher.ShouldBeNull();
            plane.FocusOwner.ShouldBeNull();
            plane.ModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies child disposal callbacks cannot resurrect their already unlinked disposing parent.</summary>
    [Fact]
    public async Task Dispose_WhenChildDisposingReentersParent_RejectsAfterOwnershipSeversAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var parent = new ProbeContainer { IsFocusable = true };
            var child = new OwnershipObserverControl();
            parent.Children.Add(child);
            root.Children.Add(parent);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(parent, initialFocus: parent);
            Exception? reentryFailure = null;
            child.Disposing = control =>
            {
                control.InheritedModalityOwner.ShouldBeNull();
                parent.Parent.ShouldBeNull();

                try
                {
                    _ = modality.Enter(parent, initialFocus: parent);
                }
                catch (Exception exception)
                {
                    reentryFailure = exception;
                }
            };

            parent.Dispose();

            _ = reentryFailure.ShouldBeOfType<ArgumentException>();
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            parent.IsFocused.ShouldBeFalse();
            parent.IsDisposed.ShouldBeTrue();
            child.IsDisposed.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a derived unavailable callback cannot include its dying root in a surviving plane.</summary>
    /// <param name="mutation">The unavailable transition applied to the candidate root.</param>
    [Theory]
    [InlineData("removed")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task Unavailable_WhenOnUnavailableIncludesDyingRoot_RejectsTransactionallyAsync(
        string mutation)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var primary = new ProbeControl { IsFocusable = true };
            var dying = new OwnershipObserverControl { IsFocusable = true };
            root.Children.Add(primary);
            root.Children.Add(dying);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary, initialFocus: primary);
            var expected = new InvalidOperationException("The unavailable callback failed.");
            Exception? includeFailure = null;
            dying.BecomingUnavailable = (control, _) =>
            {
                RestoreAvailabilityForReentry(control, mutation);

                try
                {
                    scope.Include(control);
                }
                catch (Exception exception)
                {
                    includeFailure = exception;
                }

                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(() =>
                MakeUnavailable(root, dying, mutation));

            thrown.ShouldBeSameAs(expected);
            _ = includeFailure.ShouldBeOfType<ArgumentException>();
            scope.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            modality.ActiveRootCount.ShouldBe(1);
            modality.ActiveRootAt(0).ShouldBeSameAs(primary);
            focus.Focused.ShouldBeSameAs(primary);
            primary.IsFocused.ShouldBeTrue();
            dying.IsFocused.ShouldBeFalse();
            dying.UnavailableReasons.ShouldBe([UnavailableReasonFor(mutation)]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ancestor-plane entry skips a guarded child while retaining an eligible sibling.</summary>
    [Fact]
    public async Task Unavailable_WhenAncestorPlaneEntersFromChildCallback_SkipsGuardedInitialFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            var guarded = new OwnershipObserverControl { IsFocusable = true };
            var safe = new ProbeControl { IsFocusable = true };
            plane.Children.Add(guarded);
            plane.Children.Add(safe);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            Exception? explicitFailure = null;
            ModalScope? forbidden = null;
            ModalScope? entered = null;
            guarded.BecomingUnavailable = (control, _) =>
            {
                try
                {
                    forbidden = modality.Enter(plane, initialFocus: control);
                }
                catch (Exception exception)
                {
                    explicitFailure = exception;
                }

                forbidden?.Dispose();
                entered = modality.Enter(plane);
            };

            plane.Children.Remove(guarded).ShouldBeTrue();

            _ = explicitFailure.ShouldBeOfType<ArgumentException>();
            forbidden.ShouldBeNull();
            var scope = entered.ShouldNotBeNull();
            modality.Active.ShouldBeSameAs(scope);
            scope.Root.ShouldBeSameAs(plane);
            focus.Focused.ShouldBeSameAs(safe);
            safe.IsFocused.ShouldBeTrue();
            guarded.IsFocused.ShouldBeFalse();
            guarded.Parent.ShouldBeNull();
            guarded.FocusOwner.ShouldBeNull();
            guarded.ModalityOwner.ShouldBeNull();
            scope.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disposed ancestor excludes its saved focus target from modal restoration.</summary>
    [Fact]
    public async Task Unavailable_WhenDisposedAncestorContainsSavedFocus_DoesNotRestoreIntoDyingSubtreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var ancestor = new ProbeContainer();
            var background = new ProbeControl { IsFocusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { IsFocusable = true };
            plane.Children.Add(initial);
            ancestor.Children.Add(background);
            ancestor.Children.Add(plane);
            root.Children.Add(ancestor);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            using var scope = modality.Enter(plane, initialFocus: initial);
            var restoredInside = 0;
            focus.Gained += (_, args) =>
            {
                if (ModalityManager.IsWithin(args.Current, ancestor))
                {
                    restoredInside++;
                }
            };

            ancestor.Dispose();

            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            restoredInside.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unavailable cleanup falls back outside the excluded subtree within the parent plane.</summary>
    [Fact]
    public async Task Unavailable_WhenParentPlaneHasOutsideTarget_RestoresOutsideDisposedAncestorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outerRoot = new ProbeContainer();
            var ancestor = new ProbeContainer();
            var savedInside = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            var outsideFallback = new ProbeControl { IsFocusable = true };
            innerRoot.Children.Add(innerFocus);
            ancestor.Children.Add(savedInside);
            ancestor.Children.Add(innerRoot);
            outerRoot.Children.Add(ancestor);
            outerRoot.Children.Add(outsideFallback);
            root.Children.Add(outerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: savedInside);
            using var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var restoredInside = 0;
            focus.Gained += (_, args) =>
            {
                if (ModalityManager.IsWithin(args.Current, ancestor))
                {
                    restoredInside++;
                }
            };

            ancestor.Dispose();

            modality.Active.ShouldBeSameAs(outer);
            outer.IsActive.ShouldBeTrue();
            inner.IsActive.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(outsideFallback);
            restoredInside.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Lifetime unwind and reentrancy

    /// <summary>Verifies disposing one scope repeatedly commits and publishes its exit only once.</summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_PublishesExitedOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(plane);
            var exited = 0;
            scope.Exited += (_, _) => exited++;

            scope.Dispose();
            scope.Dispose();

            exited.ShouldBe(1);
            scope.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entry callback failure rolls back the stack and focus but never reacquires capture.</summary>
    [Fact]
    public async Task Enter_WhenFocusCallbackThrows_RollsBackScopeWithoutRestoringCaptureAsync()
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
            pointer.Capture(background).ShouldBeTrue();
            var expected = new InvalidOperationException("focus callback failed");
            focus.Changing += (_, _) => throw expected;

            var thrown = Should.Throw<InvalidOperationException>(() =>
                modality.Enter(plane, initialFocus: initial));

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeSameAs(background);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies callback failures cannot prevent complete reverse unwind and the first failure wins.</summary>
    [Fact]
    public async Task Dispose_WhenExitCallbacksThrow_CompletesUnwindThenRethrowsFirstAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeContainer();
            var second = new ProbeContainer();
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var outer = modality.Enter(first);
            var inner = modality.Enter(second);
            var expected = new InvalidOperationException("inner exit failed");
            var innerExited = 0;
            var outerExited = 0;
            inner.Exited += (_, _) => throw expected;
            inner.Exited += (_, _) => innerExited++;
            outer.Exited += (_, _) =>
            {
                outerExited++;
                throw new InvalidOperationException("outer exit failed");
            };

            var thrown = Should.Throw<InvalidOperationException>(outer.Dispose);

            thrown.ShouldBeSameAs(expected);
            modality.Active.ShouldBeNull();
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeFalse();
            innerExited.ShouldBe(1);
            outerExited.ShouldBe(1);
            inner.Dispose();
            outer.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies redundant scope disposal during manager teardown is harmless and cannot replace later failure.</summary>
    [Fact]
    public async Task Dispose_WhenYoungerExitedDisposesOlderDuringManagerTeardown_RemainsHarmlessAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeContainer { IsFocusable = true };
            var second = new ProbeContainer { IsFocusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var modality = new ModalityManager(root, focus, pointer);
            var outer = modality.Enter(first, initialFocus: first);
            var inner = modality.Enter(second, initialFocus: second);
            var expected = new InvalidOperationException("The outer exit callback failed.");
            var order = new List<string>();
            Exception? reentrantFailure = null;
            var outerWasInactive = false;
            inner.Exited += (_, _) =>
            {
                order.Add("inner");
                outerWasInactive = !outer.IsActive;

                try
                {
                    outer.Dispose();
                    outer.Dispose();
                }
                catch (Exception exception)
                {
                    reentrantFailure = exception;
                }
            };
            outer.Exited += (_, _) =>
            {
                order.Add("outer");
                throw expected;
            };

            var thrown = Should.Throw<InvalidOperationException>(modality.Dispose);

            thrown.ShouldBeSameAs(expected);
            reentrantFailure.ShouldBeNull();
            outerWasInactive.ShouldBeTrue();
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            order.ShouldBe(["inner", "outer"]);
            inner.Dispose();
            outer.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope entered by a younger exit callback is also unwound before its older owner exits.</summary>
    [Fact]
    public async Task Dispose_WhenExitedCallbackEntersYoungerScope_DoesNotLeaveOrphanedScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeContainer();
            var second = new ProbeContainer();
            var third = new ProbeContainer();
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var outer = modality.Enter(first);
            var inner = modality.Enter(second);
            ModalScope? reentrant = null;
            var order = new List<string>();
            outer.Exited += (_, _) => order.Add("outer");
            inner.Exited += (_, _) =>
            {
                order.Add("inner");
                reentrant = modality.Enter(third);
                reentrant.Exited += (_, _) => order.Add("reentrant");
            };

            outer.Dispose();

            modality.Active.ShouldBeNull();
            outer.IsActive.ShouldBeFalse();
            inner.IsActive.ShouldBeFalse();
            _ = reentrant.ShouldNotBeNull();
            reentrant.IsActive.ShouldBeFalse();
            order.ShouldBe(["inner", "reentrant", "outer"]);
            inner.Dispose();
            reentrant.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the requested exit may replace its committed lifetime with the same root.</summary>
    [Fact]
    public async Task Dispose_WhenRequestedExitedCallbackReentersSameRoot_LeavesReplacementActiveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeContainer();
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var original = modality.Enter(plane);
            ModalScope? replacement = null;
            original.Exited += (_, _) => replacement = modality.Enter(plane);

            original.Dispose();

            original.IsActive.ShouldBeFalse();
            _ = replacement.ShouldNotBeNull();
            replacement.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(replacement);
            replacement.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant older disposal waits for the younger scope's complete exit publication.</summary>
    [Fact]
    public async Task Dispose_WhenFocusRestorationDisposesOlderScope_PublishesYoungestExitFirstAsync()
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
            outerRoot.Children.Add(outerFocus);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(background);
            root.Children.Add(outerRoot);
            root.Children.Add(innerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            using var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            inner.Exited += (_, _) => order.Add("inner");
            outer.Exited += (_, _) => order.Add("outer");
            focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, outerFocus))
                {
                    outer.Dispose();
                }
            };

            inner.Dispose();

            order.ShouldBe(["inner", "outer"]);
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant shutdown strengthens the remaining unwind to forbid focus restoration.</summary>
    [Fact]
    public async Task Dispose_WhenFocusRestorationRequestsShutdown_DoesNotRestoreRemainingScopesAsync()
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
            outerRoot.Children.Add(outerFocus);
            innerRoot.Children.Add(innerFocus);
            root.Children.Add(background);
            root.Children.Add(outerRoot);
            root.Children.Add(innerRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            focus.Focus(background).ShouldBeTrue();
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            using var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            var backgroundRestorations = 0;
            inner.Exited += (_, _) => order.Add("inner");
            outer.Exited += (_, _) => order.Add("outer");
            focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, outerFocus))
                {
                    modality.Shutdown();
                }
                else if (ReferenceEquals(args.Current, background))
                {
                    backgroundRestorations++;
                }
            };

            outer.Dispose();

            order.ShouldBe(["inner", "outer"]);
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            root.ModalityOwner.ShouldBeNull();
            background.ModalityOwner.ShouldBeNull();
            backgroundRestorations.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reentrant unavailability excludes its subtree before the remaining outer restoration.</summary>
    [Fact]
    public async Task Dispose_WhenFocusRestorationReportsUnavailableAncestor_UsesOutsideFallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var parentRoot = new ProbeContainer();
            var unavailableAncestor = new ProbeContainer();
            var savedInside = new ProbeControl { IsFocusable = true };
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { IsFocusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { IsFocusable = true };
            var outsideFallback = new ProbeControl { IsFocusable = true };
            outerRoot.Children.Add(outerFocus);
            innerRoot.Children.Add(innerFocus);
            unavailableAncestor.Children.Add(savedInside);
            unavailableAncestor.Children.Add(outerRoot);
            unavailableAncestor.Children.Add(innerRoot);
            parentRoot.Children.Add(unavailableAncestor);
            parentRoot.Children.Add(outsideFallback);
            root.Children.Add(parentRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var parent = modality.Enter(parentRoot, initialFocus: savedInside);
            using var outer = modality.Enter(outerRoot, initialFocus: outerFocus);
            using var inner = modality.Enter(innerRoot, initialFocus: innerFocus);
            var order = new List<string>();
            inner.Exited += (_, _) => order.Add("inner");
            outer.Exited += (_, _) => order.Add("outer");
            focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, outerFocus))
                {
                    modality.Unavailable(unavailableAncestor);
                }
            };

            outer.Dispose();

            order.ShouldBe(["inner", "outer"]);
            modality.Active.ShouldBeSameAs(parent);
            parent.IsActive.ShouldBeTrue();
            inner.IsActive.ShouldBeFalse();
            outer.IsActive.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(outsideFallback);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the manager root unwinds without refocusing a dying descendant.</summary>
    [Fact]
    public async Task RootDisposed_WhenScopeIsActive_UnwindsWithoutRestoringFocusAsync()
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
            var scope = modality.Enter(plane, initialFocus: initial);
            var restored = 0;
            focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, background))
                {
                    restored++;
                }
            };

            root.Dispose();

            scope.IsActive.ShouldBeFalse();
            restored.ShouldBe(0);
            background.IsFocused.ShouldBeFalse();
            root.ModalityOwner.ShouldBeNull();
            background.ModalityOwner.ShouldBeNull();
            scope.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detaching the manager root cannot refocus a subtree whose context is being severed.</summary>
    [Fact]
    public async Task Unavailable_WhenManagerRootDetaches_UnwindsWithoutRestoringFocusAsync()
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
            using var scope = modality.Enter(plane, initialFocus: initial);
            var restored = 0;
            focus.Gained += (_, args) =>
            {
                if (ReferenceEquals(args.Current, background))
                {
                    restored++;
                }
            };

            root.Detach();

            scope.IsActive.ShouldBeFalse();
            restored.ShouldBe(0);
            background.IsFocused.ShouldBeFalse();
            background.FocusOwner.ShouldBeNull();
            background.CaptureOwner.ShouldBeNull();
            background.ModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Ownership propagation

    /// <summary>Verifies initial, dynamic, removed, and isolated owned subtrees receive coherent modal context.</summary>
    [Fact]
    public async Task Ownership_WhenPrivateOrDynamicChildrenAttach_PropagatesModalityManagerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeOwnedControl();
            var retained = new ProbeOwnedControl();
            var initial = new OwnershipObserverControl();
            var dynamic = new OwnershipObserverControl();
            var isolatedRoot = new ProbeOwnedControl();
            var isolated = new OwnershipObserverControl();
            retained.AddPrimary(initial);
            root.AddPrimary(retained);
            isolatedRoot.AddPrimary(isolated);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);

            initial.InheritedModalityOwner.ShouldBeSameAs(modality);
            isolated.InheritedModalityOwner.ShouldBeNull();

            retained.AddPrimary(dynamic);
            dynamic.InheritedModalityOwner.ShouldBeSameAs(modality);
            root.PropagateTheme(new Theme());
            dynamic.InheritedModalityOwner.ShouldBeSameAs(modality);

            retained.RemovePrimary(dynamic).ShouldBeTrue();
            dynamic.InheritedModalityOwner.ShouldBeNull();
            modality.Dispose();
            initial.InheritedModalityOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Test helpers

    private static void MakeUnavailable(
        ProbeContainer owner,
        OwnershipObserverControl control,
        string mutation)
    {
        switch (mutation)
        {
            case "removed":
                owner.Children.Remove(control).ShouldBeTrue();
                break;
            case "hidden":
                control.Visibility = Visibility.Hidden;
                break;
            case "disabled":
                control.IsEnabled = false;
                break;
            case "disposed":
                control.Dispose();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "The mutation is unknown.");
        }
    }

    private static void RestoreAvailabilityForReentry(ControlBase control, string mutation)
    {
        if (mutation == "hidden")
        {
            control.Visibility = Visibility.Visible;
        }
        else if (mutation == "disabled")
        {
            control.IsEnabled = true;
        }
    }

    private static ReleaseReason UnavailableReasonFor(string mutation) => mutation switch
    {
        "removed" => ReleaseReason.Detached,
        "hidden" => ReleaseReason.Hidden,
        "disabled" => ReleaseReason.Disabled,
        "disposed" => ReleaseReason.Disposed,
        _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "The mutation is unknown."),
    };

    #endregion

    #region Dispatch

    /// <summary>Verifies an eligible focused control remains the key and terminal-focus route target.</summary>
    [Fact]
    public async Task Dispatch_WhenModalFocusIsAllowed_RoutesKeyAndTerminalFocusToFocusedControlAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var plane = new ProbeContainer();
        var focused = new ProbeControl { IsFocusable = true };
        plane.Children.Add(focused);
        root.Children.Add(plane);
        var routes = new List<string>();
        Record(root, "root");
        Record(plane, "plane");
        Record(focused, "focused");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                _ = application.Modality.Enter(plane, initialFocus: focused);
            },
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);
        var gainedFocus = new TerminalFocus(gained: true);

        application.Input(in stroke);
        application.Input(in gainedFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe([
            "focused-key",
            "plane-key",
            "focused-focus",
            "plane-focus",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(ControlBase control, string name)
        {
            _ = control.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-key");
                }
            });
            _ = control.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-focus");
                }
            });
        }
    }

    /// <summary>Verifies modal text and paste records reach an eligible focused editor.</summary>
    [Fact]
    public async Task Dispatch_WhenModalFocusIsAllowed_RoutesTextAndPasteToFocusedControlAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var input = new TextInput();
        var plane = new Stack { Children = { input } };
        var root = new Stack { Children = { plane } };
        var routes = new List<string>();
        Record(root, "root");
        Record(plane, "plane");
        Record(input, "input");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                _ = application.Modality.Enter(plane, initialFocus: input);
            },
            TestContext.Current.CancellationToken);
        var text = new TerminalText(new Rune('x'));

        application.Input(in text);
        application.Input(new Paste("y"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("xy");
        routes.ShouldBe([
            "plane-text-Preview",
            "input-text-Preview",
            "input-text-Bubble",
            "plane-paste-Preview",
            "input-paste-Preview",
            "input-paste-Bubble",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(ControlBase control, string name)
        {
            _ = control.AddHandler(
                Events.Text,
                (_, eventArgs) => routes.Add($"{name}-text-{eventArgs.Phase}"));
            _ = control.AddHandler(
                Events.Paste,
                (_, eventArgs) => routes.Add($"{name}-paste-{eventArgs.Phase}"));
        }
    }

    /// <summary>Verifies rejected background focus leaves key and terminal-focus fallback on the modal root.</summary>
    [Fact]
    public async Task Dispatch_WhenBackgroundFocusIsRejected_RoutesKeyAndTerminalFocusToPrimaryRootAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var background = new ProbeControl { IsFocusable = true };
        var plane = new ProbeContainer();
        root.Children.Add(background);
        root.Children.Add(plane);
        var routes = new List<string>();
        Record(root, "root");
        Record(background, "background");
        Record(plane, "plane");
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);
        var lostFocus = new TerminalFocus(gained: false);

        application.Input(in stroke);
        application.Input(in lostFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(background).ShouldBeFalse();
                application.Focus.Focused.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);
        var gainedFocus = new TerminalFocus(gained: true);
        application.Input(in stroke);
        application.Input(in gainedFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe([
            "plane-key",
            "plane-focus",
            "plane-key",
            "plane-focus",
        ]);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(ControlBase control, string name)
        {
            _ = control.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-key");
                }
            });
            _ = control.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routes.Add($"{name}-focus");
                }
            });
        }
    }

    /// <summary>Verifies rejected background focus leaves modal text and paste without a recipient.</summary>
    [Fact]
    public async Task Dispatch_WhenBackgroundFocusIsRejected_DropsTextAndPasteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(16, 4)));
        var root = new ProbeContainer();
        var background = new TextInput();
        var plane = new ProbeContainer();
        root.Children.Add(background);
        root.Children.Add(plane);
        var routes = 0;
        Record(root);
        Record(background);
        Record(plane);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Focus.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
        var text = new TerminalText(new Rune('x'));

        application.Input(in text);
        application.Input(new Paste("null"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(background).ShouldBeFalse();
                application.Focus.Focused.ShouldBeNull();
            },
            TestContext.Current.CancellationToken);
        application.Input(in text);
        application.Input(new Paste("background"u8));
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe(0);
        background.Text.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void Record(ControlBase control)
        {
            _ = control.AddHandler(Events.Text, (_, _) => routes++);
            _ = control.AddHandler(Events.Paste, (_, _) => routes++);
        }
    }

    /// <summary>Verifies terminal focus loss clears pointer ownership before modal-safe focus routing.</summary>
    [Fact]
    public async Task Dispatch_WhenTerminalFocusIsLost_CleansPointerBeforeRoutingToModalPrimaryRootAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeContainer();
        var plane = new ProbeContainer();
        var captured = new ProbeControl();
        plane.Children.Add(captured);
        root.Children.Add(plane);
        var routes = 0;
        _ = plane.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble)
            {
                routes++;
                captured.ProbeHasPointerCapture.ShouldBeFalse();
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane);
            application.Capture.Capture(captured).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        var lostFocus = new TerminalFocus(gained: false);

        application.Input(in lostFocus);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        routes.ShouldBe(1);
        captured.PointerCaptureCancellationCalls.ShouldBe(1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies modal clipboard work routes one handled preview inside its captured plane.</summary>
    [Fact]
    public async Task Dispatch_WhenClipboardShortcutRunsInModalTextInput_RoutesHandledPreviewWithinPlaneAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "modal" };
        var defaults = new List<string>();
        var plane = new RecordingControl("plane", defaults);
        var root = new RecordingControl("root", defaults);
        var background = new ProbeControl();
        plane.Children.Add(input);
        root.Children.Add(background);
        root.Children.Add(plane);
        var ordinaryRoutes = 0;
        var outsideRoutes = 0;
        var handled = new List<(RoutingPhase Phase, KeyEventArgs EventArgs)>();
        _ = plane.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = input.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = plane.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                eventArgs.IsHandled.ShouldBeTrue();
                eventArgs.OriginalSource.ShouldBeSameAs(input);
                eventArgs.Source.ShouldBeSameAs(input);
                handled.Add((eventArgs.Phase, eventArgs));
            },
            handledEventsToo: true);
        _ = root.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        _ = background.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = application.Modality.Enter(plane, initialFocus: input);
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync('c');
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                input.Text = string.Empty;
            },
            TestContext.Current.CancellationToken);
        await ShortcutAsync('v');
        await application.Dispatcher.InvokeAsync(() =>
        {
            input.Text.ShouldBe("modal");
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync('x');
        await application.Dispatcher.InvokeAsync(
            () => input.Text.ShouldBeEmpty(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync('v');
        await application.Dispatcher.InvokeAsync(
            () => input.Text.ShouldBe("modal"),
            TestContext.Current.CancellationToken);

        // IsHandled ends ordinary handling, not the route, so the opted-in plane handler observes
        // each shortcut once in preview and once again in bubble.
        handled.Count.ShouldBe(8);
        handled.Select(entry => entry.Phase).ShouldBe([
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
        ]);
        handled.Select(entry => entry.EventArgs.Stroke.Character).ShouldBe([
            new Rune('c'),
            new Rune('c'),
            new Rune('v'),
            new Rune('v'),
            new Rune('x'),
            new Rune('x'),
            new Rune('v'),
            new Rune('v'),
        ]);
        handled
            .Select(entry => entry.EventArgs)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Count()
            .ShouldBe(4);
        ordinaryRoutes.ShouldBe(0);
        outsideRoutes.ShouldBe(0);
        defaults.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        async Task ShortcutAsync(char character)
        {
            var stroke = new Stroke(
                Code.Character,
                new Rune(character),
                nativeCode: 0,
                Modifiers.Control,
                KeyAction.Press);
            application.Input(in stroke);
            await application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies clipboard callbacks cannot rewrite the route captured before modal target and scope mutation.</summary>
    [Fact]
    public async Task Dispatch_WhenModalClipboardCallbackMutatesTargetAndScope_KeepsCapturedHandledRouteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var defaults = new List<string>();
        var root = new RecordingControl("root", defaults);
        var plane = new RecordingControl("plane", defaults);
        var input = new TextInput { Text = "cut" };
        var nested = new ProbeContainer();
        plane.Children.Add(input);
        plane.Children.Add(nested);
        root.Children.Add(plane);
        var ordinaryRoutes = 0;
        var outsideRoutes = 0;
        var observed = new List<(ControlBase Sender, RoutingPhase Phase, KeyEventArgs EventArgs)>();
        _ = plane.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        _ = input.AddHandler(Events.Key, (_, _) => ordinaryRoutes++);
        RecordHandled(plane);
        RecordHandled(input);
        _ = root.AddHandler(Events.Key, (_, _) => outsideRoutes++, handledEventsToo: true);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        ModalScope? outer = null;
        ModalScope? inner = null;
        input.TextChanged += (_, _) =>
        {
            plane.Children.Remove(input).ShouldBeTrue();
            inner = application.Modality.Enter(nested);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            outer = application.Modality.Enter(plane, initialFocus: input);
            input.Select(0, input.Text.Length);
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Character,
            new Rune('x'),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBeEmpty();
        input.Parent.ShouldBeNull();
        outer.ShouldNotBeNull().IsActive.ShouldBeTrue();
        inner.ShouldNotBeNull().IsActive.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(inner);
        // The route captured before the callback removed the input from the plane is reused for
        // both phases, so bubble walks the same ancestry in reverse even though the tree changed.
        observed.Select(item => item.Sender).ShouldBe([plane, input, input, plane]);
        observed.Select(item => item.Phase).ShouldBe([
            RoutingPhase.Preview,
            RoutingPhase.Preview,
            RoutingPhase.Bubble,
            RoutingPhase.Bubble
        ]);
        observed
            .Select(item => (object) item.EventArgs)
            .Distinct(ReferenceEqualityComparer.Instance)
            .ShouldHaveSingleItem()
            .ShouldBeSameAs(observed[0].EventArgs);
        observed[0].EventArgs.OriginalSource.ShouldBeSameAs(input);
        ordinaryRoutes.ShouldBe(0);
        outsideRoutes.ShouldBe(0);
        defaults.ShouldBeEmpty();
        await application.StopAsync(TestContext.Current.CancellationToken);
        input.Dispose();
        return;

        void RecordHandled(ControlBase control) =>
            _ = control.AddHandler(
                Events.Key,
                (sender, eventArgs) =>
                {
                    sender.ShouldBeSameAs(control);
                    eventArgs.IsHandled.ShouldBeTrue();
                    observed.Add((control, eventArgs.Phase, eventArgs));
                },
                handledEventsToo: true);
    }

    #region Raw application proof

    /// <summary>Verifies one modal plane consumes raw input until dismissal and preserves final terminal output.</summary>
    [Fact]
    public async Task Input_WhenModalPlaneIsActive_IsolatesRawRecordsAndNeverReplaysDismissalAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(32, 8)));
        var root = CreateSurface(
            out var background,
            out var backgroundEditor,
            out var backgroundButton,
            out var modal,
            out var modalEditor,
            out var modalButton,
            out var modalButtonLabel);
        var backgroundRoutes = 0;
        var modalKeyRoutes = 0;
        var modalTextRoutes = 0;
        var modalPasteRoutes = 0;
        var modalFocusRoutes = 0;
        var modalPointerRoutes = 0;
        var modalWheelRoutes = 0;
        var backgroundClicks = 0;
        var modalClicks = 0;
        var dismissRequests = 0;
        backgroundButton.Click += (_, _) => backgroundClicks++;
        modalButton.Click += (_, _) => modalClicks++;
        RecordBackground(Events.Key);
        RecordBackground(Events.Text);
        RecordBackground(Events.Paste);
        RecordBackground(Events.TerminalFocusChanged);
        RecordBackground(Events.Pointer);
        _ = modal.AddHandler(Events.Key, (_, _) => modalKeyRoutes++);
        _ = modal.AddHandler(Events.Text, (_, _) => modalTextRoutes++);
        _ = modal.AddHandler(Events.Paste, (_, _) => modalPasteRoutes++);
        _ = modal.AddHandler(Events.TerminalFocusChanged, (_, _) => modalFocusRoutes++);
        _ = modal.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            modalPointerRoutes++;

            if (eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                modalWheelRoutes++;
            }
        });
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        ModalScope? scope = null;

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(backgroundEditor).ShouldBeTrue();
            scope = application.Modality.Enter(
                modal,
                OutsideInteraction.Dismiss,
                initialFocus: modalEditor);
            scope.DismissRequested += (_, _) =>
            {
                dismissRequests++;
                scope.Dispose();
            };
        }, TestContext.Current.CancellationToken);

        await SendAndWaitAsync(
            terminal,
            application,
            "λ"u8.ToArray(),
            () => modalEditor.Text == "λ",
            "modal UTF-8 text");
        await SendAndWaitAsync(
            terminal,
            application,
            "\u001b[200~界\u001b[201~"u8.ToArray(),
            () => modalEditor.Text == "λ界",
            "modal bracketed paste");
        await SendAndWaitAsync(
            terminal,
            application,
            "\t"u8.ToArray(),
            () => ReferenceEquals(application.Focus.Focused, modalButton),
            "forward modal Tab");
        await SendAndWaitAsync(
            terminal,
            application,
            "\u001b[O\u001b[I"u8.ToArray(),
            () => application.HasFocus && modalFocusRoutes >= 2,
            "terminal focus loss and gain");

        var modalButtonPoint = await application.Dispatcher.InvokeAsync(
            () => Center(modalButton),
            TestContext.Current.CancellationToken);
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(35, modalButtonPoint, 'M'),
            () => application.Pointer.Position == modalButtonPoint,
            "modal pointer move");
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(0, modalButtonPoint, 'M'),
            () => ReferenceEquals(application.Capture.Captured, modalButton),
            "modal pointer press");
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(0, modalButtonPoint, 'm'),
            () => modalClicks == 1 && application.Capture.Captured is null,
            "modal pointer release");
        await SendAndWaitAsync(
            terminal,
            application,
            "\t"u8.ToArray(),
            () => ReferenceEquals(application.Focus.Focused, modalEditor),
            "wrapped modal Tab");

        backgroundEditor.Text.ShouldBeEmpty();
        backgroundRoutes.ShouldBe(0);
        backgroundClicks.ShouldBe(0);
        modalKeyRoutes.ShouldBeGreaterThan(0);
        modalTextRoutes.ShouldBeGreaterThan(0);
        modalPasteRoutes.ShouldBeGreaterThan(0);
        modalPointerRoutes.ShouldBeGreaterThan(0);
        scope.ShouldNotBeNull().IsActive.ShouldBeTrue();
        application.Modality.Active.ShouldBeSameAs(scope);

        await WaitForIdleAsync(application);
        var postResizeWriteIndex = terminal.Writes.Count;
        var resized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        terminal.QueueResize(new Dimensions(new Size(36, 10), new Size(288, 160)));
        await resized.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;
        await WaitForIdleAsync(application);

        application.Size.ShouldBe(new Size(36, 10));
        application.Modality.Active.ShouldBeSameAs(scope);
        scope.IsActive.ShouldBeTrue();
        application.Focus.Focused.ShouldBeSameAs(modalEditor);

        var backgroundButtonPoint = await application.Dispatcher.InvokeAsync(
            () => Center(backgroundButton),
            TestContext.Current.CancellationToken);
        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(35, backgroundButtonPoint, 'M'),
            () => application.Pointer.Position == backgroundButtonPoint,
            "outside physical pointer move");

        application.Pointer.Position.ShouldBe(backgroundButtonPoint);
        application.Pointer.Hovered.ShouldBeNull();
        backgroundButton.IsPointerOver.ShouldBeFalse();
        dismissRequests.ShouldBe(0);

        await SendAndWaitAsync(
            terminal,
            application,
            EncodePointer(65, modalButtonPoint, 'M'),
            () => modalWheelRoutes > 0 && !scope.IsActive,
            "unhandled in-plane wheel completes dismiss policy");

        dismissRequests.ShouldBe(1);
        scope.IsActive.ShouldBeFalse();
        backgroundClicks.ShouldBe(0);
        backgroundRoutes.ShouldBe(0);
        application.Modality.Active.ShouldBeNull();
        application.Focus.Focused.ShouldBeSameAs(backgroundEditor);
        application.Pointer.Position.ShouldBe(modalButtonPoint);
        application.Pointer.PressOrigin.ShouldBeNull();

        await SendAndWaitAsync(
            terminal,
            application,
            "R"u8.ToArray(),
            () => backgroundEditor.Text == "R",
            "fresh background text after dismissal");
        await WaitForIdleAsync(application);

        backgroundEditor.Text.ShouldBe("R");
        modalEditor.Text.ShouldBe("λ界");
        backgroundRoutes.ShouldBeGreaterThan(0);
        backgroundClicks.ShouldBe(0);
        modalClicks.ShouldBe(1);

        var postResizeWrites = terminal.Writes.Skip(postResizeWriteIndex).ToArray();
        postResizeWrites.ShouldNotBeEmpty();
        var emitted = postResizeWrites.SelectMany(static value => value).ToArray();
        emitted.AsSpan().IndexOf("λ界"u8).ShouldBeGreaterThanOrEqualTo(0);
        emitted.ShouldContain((byte) 'R');
        var screen = new ComponentScreen(application.Size);

        foreach (var write in postResizeWrites)
        {
            screen.Apply(write);
        }

        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame expected = new(application.Size);
            root.Render(expected.Canvas);
            var backgroundTextOrigin = new Point(
                backgroundEditor.ContentBounds.X,
                backgroundEditor.ContentBounds.Y);
            FrameOracle.Get(expected, backgroundTextOrigin).ShouldBe("R");
            var modalTextOrigin = new Point(
                modalEditor.ContentBounds.X,
                modalEditor.ContentBounds.Y);
            FrameOracle.Get(expected, modalTextOrigin).ShouldBe("λ");
            FrameOracle.Get(expected, new Point(modalTextOrigin.X + 1, modalTextOrigin.Y)).ShouldBe("界");
            expected.GetCell(new Point(modalTextOrigin.X + 2, modalTextOrigin.Y))
                .Continuation.ShouldBeTrue();
            FrameOracle.Get(
                expected,
                new Point(modalButtonLabel.Bounds.X, modalButtonLabel.Bounds.Y)).ShouldBe("O");
            AssertScreen(expected, screen);
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void RecordBackground<TEventArgs>(Event<TEventArgs> routedEvent)
            where TEventArgs : RoutedEventArgs =>
            _ = background.AddHandler(routedEvent, (_, _) => backgroundRoutes++);

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (application.Size == new Size(36, 10))
            {
                _ = resized.TrySetResult();
            }
        }
    }

    #endregion

    #region Surface fixture

    private static Overlay CreateSurface(
        out Stack background,
        out TextInput backgroundEditor,
        out Button backgroundButton,
        out Stack modal,
        out TextInput modalEditor,
        out Button modalButton,
        out ControlText modalButtonLabel)
    {
        backgroundEditor = new TextInput
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        backgroundButton = new Button
        {
            Text = "BG",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        background = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { backgroundEditor, backgroundButton },
        };
        modalEditor = new TextInput
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        modalButton = new Button
        {
            Text = "OK",
            Width = Length.Cells(12),
            Height = Length.Cells(3),
        };
        modalButtonLabel = modalButton.TextControl!;
        modal = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { modalEditor, modalButton },
        };
        Overlay.SetTop(background, Length.Cells(1));
        Overlay.SetLeft(modal, Length.Cells(18));
        Overlay.SetTop(modal, Length.Cells(1));
        return new Overlay { Children = { background, modal } };
    }

    #endregion

    #region Terminal synchronization

    private static async Task SendAndWaitAsync(
        FakeTerminal terminal,
        Application application,
        ReadOnlyMemory<byte> bytes,
        Func<bool> predicate,
        string operation)
    {
        terminal.QueueInput(bytes.Span);
        await WaitUntilAsync(application, predicate, operation);
        await WaitForIdleAsync(application);
    }

    private static async Task WaitUntilAsync(
        Application application,
        Func<bool> predicate,
        string operation)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(
                predicate,
                TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Yield();
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }

        throw new TimeoutException($"Timed out waiting for {operation}.");
    }

    private static async Task WaitForIdleAsync(Application application)
    {
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Idle += OnIdle;

        try
        {
            await application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            application.Idle -= OnIdle;
        }

        return;

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = idle.TrySetResult();
        }
    }

    #endregion

    #region Output oracle

    private static void AssertScreen(Frame expected, ComponentScreen actual)
    {
        actual.Size.ShouldBe(expected.Size);

        for (var y = 0; y < expected.Size.Height; y++)
        {
            for (var x = 0; x < expected.Size.Width; x++)
            {
                var point = new Point(x, y);
                var expectedCell = expected.GetCell(point);
                var actualCell = actual.Cell(point);
                var expectedText = FrameOracle.Get(expected, point);
                actualCell.Text.ShouldBe(
                    expectedText.Length == 0 ? " " : expectedText,
                    $"terminal cell text at {point}");
                var projectedStyle = new TerminalStyle(
                    TerminalPalette.Project(expectedCell.Style.Foreground, ColorDepth.Basic16),
                    TerminalPalette.Project(expectedCell.Style.Background, ColorDepth.Basic16),
                    expectedCell.Style.Attributes);
                actualCell.Style.ShouldBe(projectedStyle, $"terminal cell style at {point}");
                actualCell.Width.ShouldBe(expectedCell.Width, $"terminal cell width at {point}");
                actualCell.Continuation.ShouldBe(
                    expectedCell.Continuation,
                    $"terminal continuation at {point}");

                if (expectedCell.Continuation)
                {
                    actualCell.LeadX.ShouldBe(expectedCell.Lead.X, $"terminal lead at {point}");
                }
            }
        }
    }

    private static Point Center(ControlBase control) => new(
        control.Bounds.X + (control.Bounds.Width / 2),
        control.Bounds.Y + (control.Bounds.Height / 2));

    private static byte[] EncodePointer(int button, Point point, char final) =>
        Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<{button};{point.X + 1};{point.Y + 1}{final}"));

    #endregion

    #endregion

    #region Focus

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

    /// <summary>Verifies a queued initial-focus candidate that becomes ineligible before the deferred
    /// commit settles still activates the modal root's owner, even though the pre-commit target
    /// inspected at entry time was non-null.</summary>
    [Fact]
    public async Task Enter_WhenQueuedInitialFocusBecomesIneligible_ActivatesModalRootAsync()
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
            var activated = new List<ControlBase?>();
            using var modality = new ModalityManager(root, focus, pointer, target => activated.Add(target));
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
            activated.ShouldHaveSingleItem().ShouldBeSameAs(plane);
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

    #endregion

    #region Pointer

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
                IsFocusable = true,
            };
            var background = new ProbeControl
            {
                Bounds = new Rect(16, 0, 8, 6),
                IsFocusable = true,
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
                IsFocusable = true,
            };
            var second = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                IsFocusable = true,
            };
            var saved = new ProbeControl
            {
                Bounds = new Rect(22, 0, 6, 6),
                IsFocusable = true,
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
                IsFocusable = true,
            };
            var target = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                IsFocusable = true,
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
                IsFocusable = true,
            };
            var target = new ProbeControl
            {
                Bounds = new Rect(10, 1, 6, 4),
                IsFocusable = true,
            };
            var nestedRoot = new ProbeContainer { Bounds = new Rect(22, 0, 8, 6) };
            var nestedFocus = new ProbeControl
            {
                Bounds = new Rect(23, 1, 4, 4),
                IsFocusable = true,
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

    /// <summary>Verifies an unhandled in-plane wheel completes the configured post-route policy.</summary>
    [Theory]
    [InlineData(OutsideInteraction.Ignore)]
    [InlineData(OutsideInteraction.Dismiss)]
    public async Task Dispatch_WhenInPlaneWheelRemainsUnhandled_CompletesPolicyAsync(OutsideInteraction policy)
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

            dismissals.ShouldBe(policy == OutsideInteraction.Dismiss ? 1 : 0);
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
            _ = plane.AddHandler(Events.Pointer, (_, eventArgs) => eventArgs.IsHandled = true);

            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Wheel, wheelY: -1))
                .ShouldBeSameAs(plane);

            dismissals.ShouldBe(0);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an armed Container that cannot move leaves the wheel for modal policy.</summary>
    [Fact]
    public async Task Dispatch_WhenArmedContainerWheelMovesNoOffset_RequestsDismissalAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var plane = new ProbeContainer { Bounds = new Rect(0, 0, 8, 6), AutoScroll = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            var dismissals = 0;
            scope.DismissRequested += (_, _) => dismissals++;

            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Wheel, wheelY: -1))
                .ShouldBeSameAs(plane);

            dismissals.ShouldBe(1);
            scope.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a DismissRequested subscriber that ends the scope stops later subscribers on
    /// the same publication from running.</summary>
    [Fact]
    public async Task Dispatch_WhenDismissSubscriberDisposesScope_SkipsRemainingSubscribersAsync()
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
            var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            var secondSubscriberCalls = 0;

            scope.DismissRequested += (_, _) => scope.Dispose();
            scope.DismissRequested += (_, _) => secondSubscriberCalls++;

            _ = pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Press, Buttons.Primary));

            secondSubscriberCalls.ShouldBe(0);
            scope.IsActive.ShouldBeFalse();
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

    /// <summary>Verifies including a root re-hit-tests the retained physical pointer cell.</summary>
    [Fact]
    public async Task Include_WhenPointerIsStationaryOverNewRoot_ReconcilesHoverImmediatelyAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var primary = new ProbeControl { Bounds = new Rect(0, 0, 8, 6) };
            var included = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(primary);
            root.Children.Add(included);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary);
            _ = pointer.Dispatch(CreatePointer(new Point(14, 2), PointerAction.Move));
            pointer.Hovered.ShouldBeNull();

            scope.Include(included);

            pointer.Hovered.ShouldBeSameAs(included);
            included.IsPointerOver.ShouldBeTrue();
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

    /// <summary>Verifies a press consumed as an outside-modal-plane dismiss breaks the multi-click
    /// chain, so the next physical press at the same spot starts a fresh click sequence instead of
    /// continuing one the user never performed.</summary>
    [Fact]
    public async Task Dispatch_WhenPressIsConsumedAsOutsideDismiss_BreaksClickChainAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 24, 8) };
            var background = new ProbeControl { Bounds = new Rect(0, 0, 8, 6), IsFocusable = true };
            var plane = new ProbeControl { Bounds = new Rect(12, 0, 8, 6) };
            root.Children.Add(background);
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            var clock = new ManualTimeProvider();
            using var pointer = new PointerManager(root, clock);
            using var modality = new ModalityManager(root, focus, pointer);
            List<int> observed = [];
            _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    observed.Add(eventArgs.ClickCount);
                }
            });

            _ = pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            _ = pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            clock.Advance(TimeSpan.FromMilliseconds(100));

            var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
            scope.DismissRequested += (_, _) => scope.Dispose();
            pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press, Buttons.Primary))
                .ShouldBeNull();
            clock.Advance(TimeSpan.FromMilliseconds(100));

            _ = pointer.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));

            observed.ShouldBe([1, 0, 1]);
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
            var parentFocus = new ProbeControl { IsFocusable = true };
            var childRoot = new ProbeContainer { Bounds = new Rect(12, 0, 16, 6) };
            var childPointer = new ProbeControl { Bounds = new Rect(13, 1, 6, 4) };
            var childFocus = new ProbeControl
            {
                Bounds = new Rect(21, 1, 6, 4),
                IsFocusable = true,
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
            var parentFocus = new ProbeControl { IsFocusable = true };
            var childRoot = new ProbeContainer { Bounds = new Rect(12, 0, 8, 6) };
            var childFocus = new ProbeControl
            {
                Bounds = new Rect(13, 1, 6, 4),
                IsFocusable = true,
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
                IsFocusable = true,
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
                IsFocusable = true,
            };
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 20, 7) };
            var plane = new ProbeContainer { Bounds = new Rect(2, 1, 16, 5) };
            var leaf = new ProbeControl
            {
                Bounds = new Rect(4, 2, 8, 3),
                IsFocusable = true,
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
            Text = "Modal",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var background = new Button
        {
            Text = "Background",
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
            Text = "Modal",
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var background = new Button
        {
            Text = "Background",
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

    #endregion

    #region Randomized invariants

    private const int _caseCount = 16;
    private const int _maximumScopes = 6;
    private const int _stepCount = 48;

    #region Named regression

    /// <summary>Verifies exiting a disjoint child scope reconfines all retained pointer state to its parent.</summary>
    [Fact]
    public async Task Dispose_WhenChildPointerStateIsOutsideParentPlane_ClearsThatStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var outerRoot = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 6),
                IsFocusable = true,
            };
            var childRoot = new ProbeControl
            {
                Bounds = new Rect(16, 0, 10, 6),
                IsFocusable = true,
            };
            root.Children.Add(outerRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerRoot);
            var child = modality.Enter(childRoot, initialFocus: childRoot);
            pointer.Capture(childRoot).ShouldBeTrue();
            var cells = new Point(18, 2);
            _ = pointer.Dispatch(new Pointer(
                cells,
                pixels: null,
                Buttons.None,
                PointerAction.Move,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: true,
                isCellPositionInferred: false));
            _ = pointer.Dispatch(new Pointer(
                cells,
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));

            child.Dispose();

            modality.Active.ShouldBeSameAs(outer);
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            childRoot.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Randomized state machine

    /// <summary>Verifies every generated operation preserves the independently modeled active plane.</summary>
    [Theory]
    [InlineData(0x51A4_80D1)]
    [InlineData(0x27D1_5C0D)]
    [InlineData(0x0D15_C0DE)]
    public async Task State_WhenOperationsAreRandomized_RemainsInsideActivePlaneAsync(int seed)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            for (var sample = 0; sample < _caseCount; sample++)
            {
                RunCase(dispatcher, seed, sample);
            }
        }, TestContext.Current.CancellationToken);
    }

    private static void RunCase(Dispatcher dispatcher, int seed, int sample)
    {
        var random = new Random(unchecked(seed + (sample * 7_919)));
        var root = new ProbeContainer { Bounds = new Rect(0, 0, 60, 8) };
        var first = new ProbeContainer
        {
            Bounds = new Rect(0, 0, 12, 8),
            IsFocusable = true,
        };
        var firstNested = new ProbeControl
        {
            Bounds = new Rect(2, 2, 4, 3),
            IsFocusable = true,
        };
        var second = new ProbeContainer
        {
            Bounds = new Rect(14, 0, 12, 8),
            IsFocusable = true,
        };
        var secondNested = new ProbeControl
        {
            Bounds = new Rect(16, 2, 4, 3),
            IsFocusable = true,
        };
        var third = new ProbeControl
        {
            Bounds = new Rect(28, 0, 10, 8),
            IsFocusable = true,
        };
        var fourth = new ProbeControl
        {
            Bounds = new Rect(40, 0, 10, 8),
            IsFocusable = true,
        };
        first.Children.Add(firstNested);
        second.Children.Add(secondNested);
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);
        root.Children.Add(fourth);
        root.Attach(dispatcher);
        var controls = new List<ControlBase>
        {
            first,
            firstNested,
            second,
            secondNested,
            third,
            fourth,
        };
        var points = new Dictionary<ControlBase, Point>(ReferenceEqualityComparer.Instance)
        {
            [first] = new Point(1, 1),
            [firstNested] = new Point(3, 3),
            [second] = new Point(15, 1),
            [secondNested] = new Point(17, 3),
            [third] = new Point(32, 3),
            [fourth] = new Point(44, 3),
            [root] = new Point(55, 6),
        };
        using var focus = new FocusManager(root);
        using var pointer = new PointerManager(root);
        using var modality = new ModalityManager(root, focus, pointer);
        var handles = new List<ModalScope>();
        var expectedStack = new List<ModalScope>();
        var roots = new Dictionary<ModalScope, List<ControlBase>>(ReferenceEqualityComparer.Instance);
        var policies = new Dictionary<ModalScope, OutsideInteraction>(ReferenceEqualityComparer.Instance);
        var inactive = new HashSet<ModalScope>(ReferenceEqualityComparer.Instance);
        var dismissedScopes = new List<ModalScope>();
        var routedTargets = new List<ControlBase>();
        var recent = new Queue<string>();
        var dismissalCallbacks = 0;
        var qualifyingDismissInputs = 0;
        var qualifyingIgnoreInputs = 0;
        var dismissingPresses = 0;
        var dismissingWheels = 0;
        var entriesAfterInactiveHandles = 0;
        var entriesAfterHistoricalLimit = 0;

        foreach (var control in controls.Prepend(root))
        {
            var observed = control;
            _ = observed.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble &&
                    ReferenceEquals(eventArgs.OriginalSource, observed))
                {
                    routedTargets.Add(observed);
                }
            });
        }

        for (var step = 0; step < _stepCount; step++)
        {
            if (step == 0)
            {
                Enter(first, OutsideInteraction.Ignore, "Enter(first)");
            }
            else if (step == 1)
            {
                Include(third, "Include(third)");
            }
            else if (step == 2)
            {
                Focus(firstNested, "Focus(firstNested)");
            }
            else if (step == 3)
            {
                Capture(third, "Capture(third)");
            }
            else if (step == 4)
            {
                Dispatch(fourth, PointerAction.Move, "Move(fourth)");
            }
            else if (step == 5)
            {
                Dispatch(firstNested, PointerAction.Press, "Press(firstNested)");
            }
            else if (step == 6)
            {
                Dispatch(fourth, PointerAction.Wheel, "Wheel(fourth)");
            }
            else if (step == 7)
            {
                Enter(firstNested, OutsideInteraction.Dismiss, "NestedEnter(firstNested)");
            }
            else if (step == 8)
            {
                DispatchScriptedDismissingPress();
            }
            else if (step == 9)
            {
                Dispatch(fourth, PointerAction.Wheel, "IgnoreWheel(fourth)");
            }
            else if (step == 10)
            {
                Hide(firstNested, "Hide(firstNested)");
            }
            else if (step == 11)
            {
                Dispose(handles[0], "Dispose(first scope)");
            }
            else if (step == 12)
            {
                Enter(second, OutsideInteraction.Dismiss, "Enter(second dismiss)");
            }
            else if (step == 13)
            {
                Dispatch(fourth, PointerAction.Wheel, "DismissWheel(fourth)");
            }
            else if (step == 14)
            {
                Enter(third, OutsideInteraction.Ignore, "Enter(third churn)");
            }
            else if (step == 15)
            {
                Dispose(handles[^1], "Dispose(third churn)");
            }
            else if (step == 16)
            {
                Enter(fourth, OutsideInteraction.Ignore, "Enter(fourth churn)");
            }
            else if (step == 17)
            {
                Dispose(handles[^1], "Dispose(fourth churn)");
            }
            else if (step == 18)
            {
                Enter(second, OutsideInteraction.Ignore, "Enter(second churn)");
            }
            else if (step == 19)
            {
                Dispose(handles[^1], "Dispose(second churn)");
            }
            else if (step == 20)
            {
                ApplyRandom(operation: 0);
            }
            else if (step == 21)
            {
                Dispose(handles[^1], "Dispose(post-limit churn)");
            }
            else
            {
                ApplyRandom(random.Next(10));
            }

            AssertInvariants(step);
        }

        if (expectedStack.Count > 0)
        {
            var oldest = expectedStack[0];
            Record("FinalDispose(oldest)");
            oldest.Dispose();
            expectedStack.Clear();
        }

        foreach (var handle in handles)
        {
            handle.IsActive.ShouldBeFalse(Context(_stepCount));
        }

        dismissalCallbacks.ShouldBe(qualifyingDismissInputs, Context(_stepCount));
        dismissingPresses.ShouldBeGreaterThan(0, Context(_stepCount));
        dismissingWheels.ShouldBeGreaterThan(0, Context(_stepCount));
        qualifyingIgnoreInputs.ShouldBeGreaterThan(0, Context(_stepCount));
        entriesAfterInactiveHandles.ShouldBeGreaterThan(0, Context(_stepCount));
        entriesAfterHistoricalLimit.ShouldBeGreaterThan(0, Context(_stepCount));
        handles.Count.ShouldBeGreaterThan(_maximumScopes, Context(_stepCount));
        modality.Active.ShouldBeNull(Context(_stepCount));
        modality.Dispose();
        pointer.Dispose();
        focus.Dispose();
        root.Dispose();
        return;

        void ApplyRandom(int operation)
        {
            switch (operation)
            {
                case 0:
                    var enterCandidates = controls.Where(control =>
                        control.EffectiveIsVisible &&
                        control.EffectiveIsEnabled &&
                        expectedStack.Count < _maximumScopes &&
                        !expectedStack.Any(scope => roots[scope].Any(root => ReferenceEquals(root, control)))).ToArray();

                    if (enterCandidates.Length > 0)
                    {
                        var candidate = Choose(enterCandidates);
                        Enter(
                            candidate,
                            random.Next(2) == 0 ? OutsideInteraction.Ignore : OutsideInteraction.Dismiss,
                            $"Enter({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(enter fallback)");
                    break;
                case 1:
                    var includeCandidates = expectedStack.Count == 0
                        ? []
                        : controls.Where(control =>
                            control.EffectiveIsVisible &&
                            control.EffectiveIsEnabled &&
                            expectedStack.All(scope => roots[scope].All(root =>
                                !IsWithin(control, root) && !IsWithin(root, control)))).ToArray();

                    if (includeCandidates.Length > 0)
                    {
                        var candidate = Choose(includeCandidates);
                        Include(candidate, $"Include({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(include fallback)");
                    break;
                case 2:
                    var nestedCandidates = expectedStack.Count is 0 or >= _maximumScopes
                        ? []
                        : controls.Where(control =>
                            control.EffectiveIsVisible &&
                            control.EffectiveIsEnabled &&
                            roots[expectedStack[^1]].Any(root =>
                                !ReferenceEquals(control, root) && IsWithin(control, root)) &&
                            expectedStack.All(scope => roots[scope].All(root => !ReferenceEquals(root, control))))
                            .ToArray();

                    if (nestedCandidates.Length > 0)
                    {
                        var candidate = Choose(nestedCandidates);
                        Enter(candidate, OutsideInteraction.Ignore, $"NestedEnter({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(nested fallback)");
                    break;
                case 3:
                    var focusCandidates = AllowedControls();

                    if (focusCandidates.Length > 0)
                    {
                        var candidate = Choose(focusCandidates);
                        Focus(candidate, $"Focus({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(focus fallback)");
                    break;
                case 4:
                    var captureCandidates = AllowedControls();

                    if (captureCandidates.Length > 0)
                    {
                        var candidate = Choose(captureCandidates);
                        Capture(candidate, $"Capture({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(capture fallback)");
                    break;
                case 5:
                    DispatchRandom(PointerAction.Move, "Move(random)");
                    break;
                case 6:
                    DispatchRandom(PointerAction.Press, "Press(random)");
                    break;
                case 7:
                    DispatchRandom(PointerAction.Wheel, "Wheel(random)");
                    break;
                case 8:
                    var hideCandidates = controls.Where(static control => control.EffectiveIsVisible).ToArray();

                    if (hideCandidates.Length > 0)
                    {
                        var candidate = Choose(hideCandidates);
                        Hide(candidate, $"Hide({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(hide fallback)");
                    break;
                case 9:
                    if (handles.Count > 0)
                    {
                        var handle = handles[random.Next(handles.Count)];
                        Dispose(handle, $"Dispose(scope {handles.IndexOf(handle)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(dispose fallback)");
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        void Enter(ControlBase control, OutsideInteraction outsideInteraction, string operation)
        {
            Record(operation);
            var followsInactiveHandle = handles.Exists(static handle => !handle.IsActive);
            var exceedsHistoricalLimit = handles.Count >= _maximumScopes;
            var scope = modality.Enter(control, outsideInteraction, initialFocus: control);
            handles.Add(scope);
            expectedStack.Add(scope);
            roots.Add(scope, [control]);
            policies.Add(scope, outsideInteraction);

            if (followsInactiveHandle)
            {
                entriesAfterInactiveHandles++;
            }

            if (exceedsHistoricalLimit)
            {
                entriesAfterHistoricalLimit++;
            }

            scope.DismissRequested += (_, _) =>
            {
                dismissalCallbacks++;
                dismissedScopes.Add(scope);
                var index = expectedStack.IndexOf(scope);

                if (index >= 0)
                {
                    expectedStack.RemoveRange(index, expectedStack.Count - index);
                }

                scope.Dispose();
            };

        }

        void Include(ControlBase control, string operation)
        {
            Record(operation);
            var active = expectedStack[^1];
            active.Include(control);
            roots[active].Add(control);
        }

        void Focus(ControlBase control, string operation)
        {
            Record(operation);
            focus.Focus(control).ShouldBeTrue(Context(-1));
        }

        void Capture(ControlBase control, string operation)
        {
            Record(operation);
            pointer.Capture(control).ShouldBeTrue(Context(-1));
        }

        void Hide(ControlBase control, string operation)
        {
            Record(operation);
            var unwind = -1;

            for (var index = 0; index < expectedStack.Count; index++)
            {
                if (IsWithin(expectedStack[index].Root, control))
                {
                    unwind = index;
                    break;
                }
            }

            var surviving = unwind < 0 ? expectedStack.Count : unwind;

            for (var index = 0; index < surviving; index++)
            {
                var planeRoots = roots[expectedStack[index]];

                for (var rootIndex = planeRoots.Count - 1; rootIndex > 0; rootIndex--)
                {
                    if (IsWithin(planeRoots[rootIndex], control))
                    {
                        planeRoots.RemoveAt(rootIndex);
                    }
                }
            }

            if (unwind >= 0)
            {
                expectedStack.RemoveRange(unwind, expectedStack.Count - unwind);
            }

            control.Visibility = Visibility.Hidden;
        }

        void Dispose(ModalScope scope, string operation)
        {
            Record(operation);
            var index = expectedStack.IndexOf(scope);

            if (index >= 0)
            {
                expectedStack.RemoveRange(index, expectedStack.Count - index);
            }

            scope.Dispose();
        }

        void DispatchScriptedDismissingPress()
        {
            expectedStack.Count.ShouldBe(2, Context(-1));
            var parent = expectedStack[^2];
            var child = expectedStack[^1];
            IsAllowed(third, roots[parent]).ShouldBeTrue(Context(-1));
            IsAllowed(third, roots[child]).ShouldBeFalse(Context(-1));
            modality.Active.ShouldBeSameAs(child, Context(-1));
            policies[child].ShouldBe(OutsideInteraction.Dismiss, Context(-1));
            var callbacksBefore = dismissalCallbacks;
            var dismissedBefore = dismissedScopes.Count;

            Dispatch(third, PointerAction.Press, "DismissPress(third)");

            dismissalCallbacks.ShouldBe(callbacksBefore + 1, Context(-1));
            dismissedScopes.Count.ShouldBe(dismissedBefore + 1, Context(-1));
            dismissedScopes[^1].ShouldBeSameAs(child, Context(-1));
            routedTargets.ShouldBeEmpty(Context(-1));
            expectedStack.Count.ShouldBe(1, Context(-1));
            expectedStack[^1].ShouldBeSameAs(parent, Context(-1));
            modality.Active.ShouldBeSameAs(parent, Context(-1));
        }

        void DispatchRandom(PointerAction action, string operation)
        {
            var target = Choose(points.Keys.ToArray());
            Dispatch(target, action, $"{operation}:{Name(target)}");
        }

        void Dispatch(ControlBase pointOwner, PointerAction action, string operation)
        {
            Record(operation);
            var point = points[pointOwner];
            var activeBefore = expectedStack.Count == 0 ? null : expectedStack[^1];
            var stackBefore = expectedStack.ToArray();
            var planeRoots = expectedStack.Count == 0
                ? null
                : roots[expectedStack[^1]].ToArray();
            var physical = root.HitTest(point);
            var captured = pointer.Captured;
            var eligiblePhysical = planeRoots is null || IsAllowed(physical, planeRoots)
                ? physical
                : null;
            var expectedTarget = captured ?? eligiblePhysical;
            var qualifiesOutside = activeBefore is not null &&
                captured is null &&
                eligiblePhysical is null &&
                action is PointerAction.Press or PointerAction.Wheel;
            var qualifiesPolicyInput = qualifiesOutside ||
                (activeBefore is not null && captured is null && action == PointerAction.Wheel);
            var expectsDismissal = qualifiesPolicyInput &&
                policies[activeBefore!] == OutsideInteraction.Dismiss;
            var callbacksBefore = dismissalCallbacks;
            var dismissedBefore = dismissedScopes.Count;

            if (qualifiesPolicyInput)
            {
                if (expectsDismissal)
                {
                    qualifyingDismissInputs++;

                    if (action == PointerAction.Press)
                    {
                        dismissingPresses++;
                    }
                    else
                    {
                        dismissingWheels++;
                    }
                }
                else
                {
                    qualifyingIgnoreInputs++;
                }
            }

            routedTargets.Clear();
            var input = new Pointer(
                point,
                pixels: null,
                action == PointerAction.Press ? Buttons.Primary : Buttons.None,
                action,
                wheelX: 0,
                wheelY: action == PointerAction.Wheel ? -1 : 0,
                Modifiers.None,
                isMotion: action == PointerAction.Move,
                isCellPositionInferred: false);

            var actualTarget = pointer.Dispatch(input);

            actualTarget.ShouldBeSameAs(expectedTarget, Context(-1));
            (dismissalCallbacks - callbacksBefore).ShouldBe(
                expectsDismissal ? 1 : 0,
                Context(-1));

            if (expectsDismissal)
            {
                dismissedScopes.Count.ShouldBe(dismissedBefore + 1, Context(-1));
                dismissedScopes[^1].ShouldBeSameAs(activeBefore, Context(-1));
                activeBefore.ShouldNotBeNull().IsActive.ShouldBeFalse(Context(-1));
                AssertStack(stackBefore[..^1], Context(-1));
            }
            else
            {
                dismissedScopes.Count.ShouldBe(dismissedBefore, Context(-1));
                AssertStack(stackBefore, Context(-1));
            }

            if (expectedTarget is null)
            {
                routedTargets.ShouldBeEmpty(Context(-1));
            }
            else
            {
                routedTargets.ShouldBe([expectedTarget], Context(-1));
            }
        }

        ControlBase[] AllowedControls()
        {
            var planeRoots = expectedStack.Count == 0 ? null : roots[expectedStack[^1]];
            return [.. controls.Where(control =>
                control.EffectiveIsVisible &&
                control.EffectiveIsEnabled &&
                control.CanFocus &&
                (planeRoots is null || IsAllowed(control, planeRoots)))];
        }

        T Choose<T>(IReadOnlyList<T> values) => values[random.Next(values.Count)];

        void AssertStack(IReadOnlyList<ModalScope> expected, string context)
        {
            expectedStack.Count.ShouldBe(expected.Count, context);

            for (var index = 0; index < expected.Count; index++)
            {
                expectedStack[index].ShouldBeSameAs(expected[index], context);
            }
        }

        void AssertInvariants(int step)
        {
            var context = Context(step);
            modality.Active.ShouldBe(expectedStack.Count == 0 ? null : expectedStack[^1], context);

            foreach (var handle in handles)
            {
                var expectedActive = expectedStack.Contains(handle);
                handle.IsActive.ShouldBe(expectedActive, context);

                if (!expectedActive)
                {
                    _ = inactive.Add(handle);
                }
            }

            foreach (var handle in inactive)
            {
                handle.IsActive.ShouldBeFalse(context);
            }

            if (expectedStack.Count > 0)
            {
                var activeRoots = roots[expectedStack[^1]];
                expectedStack[^1].RootCount.ShouldBe(activeRoots.Count, context);

                for (var index = 0; index < activeRoots.Count; index++)
                {
                    expectedStack[^1].RootAt(index).ShouldBeSameAs(activeRoots[index], context);
                }

                AssertState(focus.Focused, "focus", activeRoots, context);
                AssertState(pointer.Captured, "capture", activeRoots, context);
                AssertState(pointer.Hovered, "hover", activeRoots, context);
                AssertState(pointer.PressOrigin, "press origin", activeRoots, context);
            }

            dismissalCallbacks.ShouldBe(qualifyingDismissInputs, context);
            handles.Count.ShouldBeLessThanOrEqualTo(_stepCount, context);
            expectedStack.Count.ShouldBeLessThanOrEqualTo(_maximumScopes, context);
            controls.Count.ShouldBe(6, context);

            if (step >= 8)
            {
                dismissingPresses.ShouldBeGreaterThan(0, context);
            }

            if (step >= 9)
            {
                qualifyingIgnoreInputs.ShouldBeGreaterThan(0, context);
            }

            if (step >= 13)
            {
                dismissingWheels.ShouldBeGreaterThan(0, context);
                entriesAfterInactiveHandles.ShouldBeGreaterThan(0, context);
            }

            if (step >= 20)
            {
                entriesAfterHistoricalLimit.ShouldBeGreaterThan(0, context);
                handles.Count.ShouldBeGreaterThan(_maximumScopes, context);
            }
        }

        void Record(string operation)
        {
            recent.Enqueue(operation);

            while (recent.Count > 8)
            {
                _ = recent.Dequeue();
            }
        }

        string Context(int step) =>
            $"seed=0x{seed:X8}, case={sample}, step={step}, liveScopes={expectedStack.Count}, " +
            $"historicalHandles={handles.Count}, dismissals={dismissalCallbacks}/{qualifyingDismissInputs}, " +
            $"reentries={entriesAfterInactiveHandles}/{entriesAfterHistoricalLimit}, " +
            $"recent=[{string.Join(" | ", recent)}]";

        string Name(ControlBase control) => ReferenceEquals(control, root)
            ? "root"
            : $"control-{controls.IndexOf(control)}";
    }

    #endregion

    #region Independent plane oracle

    private static void AssertState(
        ControlBase? control,
        string name,
        IReadOnlyList<ControlBase> planeRoots,
        string context)
    {
        if (control is not null)
        {
            IsAllowed(control, planeRoots).ShouldBeTrue($"{context}, state={name}");
        }
    }

    private static bool IsAllowed(ControlBase? control, IReadOnlyList<ControlBase> planeRoots)
    {
        if (control is null)
        {
            return false;
        }

        foreach (var root in planeRoots)
        {
            if (IsWithin(control, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(ControlBase? control, ControlBase root)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #endregion

    #region Routing

    /// <summary>Verifies preview, bubble, and defaults remain inside the matching primary plane root.</summary>
    [Fact]
    public async Task Route_WhenTargetIsInsideModalPlane_StopsAtMatchingRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            Record(appRoot, "app");
            Record(plane, "plane");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies blocked direct callers fail before arguments or handlers observe a route.</summary>
    [Fact]
    public async Task Route_WhenTargetIsOutsideActivePlane_ThrowsBeforeBeginningArgumentsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var background = new RecordingControl("background", order);
            var plane = new RecordingControl("plane", order);
            appRoot.Children.Add(background);
            appRoot.Children.Add(plane);
            _ = appRoot.AddHandler(Events.Key, (_, _) => order.Add("app-handler"));
            _ = background.AddHandler(Events.Key, (_, _) => order.Add("background-handler"));
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);
            var eventArgs = new KeyEventArgs(CreateStroke()) { IsHandled = true };

            _ = Should.Throw<InvalidOperationException>(() =>
                Router.Route(background, Events.Key, eventArgs));

            eventArgs.OriginalSource.ShouldBeNull();
            eventArgs.Source.ShouldBeNull();
            eventArgs.IsHandled.ShouldBeTrue();
            order.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a target in a secondary plane routes only through that included root.</summary>
    [Fact]
    public async Task Route_WhenTargetUsesIncludedRoot_StopsAtIncludedRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var primary = new RecordingControl("primary", order);
            var included = new RecordingControl("included", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(primary);
            appRoot.Children.Add(included);
            included.Children.Add(leaf);
            Record(appRoot, "app");
            Record(primary, "primary");
            Record(included, "included");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(primary);
            scope.Include(included);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "included-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "included-Bubble",
                "included-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entering a scope during preview changes only later routes.</summary>
    [Fact]
    public async Task Route_WhenHandlerEntersScope_KeepsCapturedAncestryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            ModalScope? scope = null;
            Record(appRoot, "app", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && scope is null)
                {
                    scope = modality.Enter(plane);
                }
            });
            Record(plane, "plane");
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            _ = scope.ShouldNotBeNull();
            scope.Dispose();
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies including a root during preview affects eligibility only after the current route.</summary>
    [Fact]
    public async Task Route_WhenHandlerIncludesRoot_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var primary = new RecordingControl("primary", order);
            var primaryLeaf = new RecordingControl("primary-leaf", order);
            var included = new RecordingControl("included", order);
            var includedLeaf = new RecordingControl("included-leaf", order);
            appRoot.Children.Add(primary);
            appRoot.Children.Add(included);
            primary.Children.Add(primaryLeaf);
            included.Children.Add(includedLeaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(primary);
            var includedDuringRoute = false;
            Record(primary, "primary", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !includedDuringRoute)
                {
                    includedDuringRoute = true;
                    scope.Include(included);
                }
            });
            Record(primaryLeaf, "primary-leaf");
            Record(included, "included");
            Record(includedLeaf, "included-leaf");

            _ = Router.Route(primaryLeaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "primary-Preview",
                "primary-leaf-Preview",
                "primary-leaf-Bubble",
                "primary-leaf-default",
                "primary-Bubble",
                "primary-default",
            ]);
            order.Clear();

            _ = Router.Route(includedLeaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "included-Preview",
                "included-leaf-Preview",
                "included-leaf-Bubble",
                "included-leaf-default",
                "included-Bubble",
                "included-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies exiting a scope during preview does not extend the current route.</summary>
    [Fact]
    public async Task Route_WhenHandlerExitsScope_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            var scope = modality.Enter(plane);
            var exitedDuringRoute = false;
            Record(appRoot, "app");
            Record(plane, "plane", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !exitedDuringRoute)
                {
                    exitedDuringRoute = true;
                    scope.Dispose();
                }
            });
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies manager disposal during preview does not extend the captured route.</summary>
    [Fact]
    public async Task Route_WhenHandlerDisposesManager_KeepsCapturedBoundaryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);
            var disposedDuringRoute = false;
            Record(appRoot, "app");
            Record(plane, "plane", eventArgs =>
            {
                if (eventArgs.Phase == RoutingPhase.Preview && !disposedDuringRoute)
                {
                    disposedDuringRoute = true;
                    modality.Dispose();
                }
            });
            Record(leaf, "leaf");

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
            ]);
            order.Clear();

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "plane-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "plane-Bubble",
                "plane-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(
                ControlBase control,
                string name,
                Action<KeyEventArgs>? callback = null) =>
                _ = control.AddHandler(Events.Key, (_, eventArgs) =>
                {
                    order.Add($"{name}-{eventArgs.Phase}");
                    callback?.Invoke(eventArgs);
                });
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct events remain target-only while enforcing modal eligibility.</summary>
    [Fact]
    public async Task Route_WhenStrategyIsDirect_UsesOnlyEligibleTargetAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var background = new RecordingControl("background", order);
            var plane = new RecordingControl("plane", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(background);
            appRoot.Children.Add(plane);
            plane.Children.Add(leaf);
            var direct = new Event<KeyEventArgs>("Direct", RoutingStrategy.Direct);
            Record(appRoot, "app");
            Record(background, "background");
            Record(plane, "plane");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);
            using var scope = modality.Enter(plane);

            _ = Router.Route(leaf, direct, new KeyEventArgs(CreateStroke()));

            order.ShouldBe(["leaf-Bubble", "leaf-default"]);
            var blockedArgs = new KeyEventArgs(CreateStroke());
            _ = Should.Throw<InvalidOperationException>(() =>
                Router.Route(background, direct, blockedArgs));
            blockedArgs.OriginalSource.ShouldBeNull();
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    direct,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an attached manager without an active scope preserves the established route order.</summary>
    [Fact]
    public async Task Route_WhenNoScopeIsActive_PreservesExistingOrderingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var order = new List<string>();
            var appRoot = new RecordingControl("app", order);
            var middle = new RecordingControl("middle", order);
            var leaf = new RecordingControl("leaf", order);
            appRoot.Children.Add(middle);
            middle.Children.Add(leaf);
            Record(appRoot, "app");
            Record(middle, "middle");
            Record(leaf, "leaf");
            appRoot.Attach(dispatcher);
            using var focus = new FocusManager(appRoot);
            using var pointer = new PointerManager(appRoot);
            using var modality = new ModalityManager(appRoot, focus, pointer);

            _ = Router.Route(leaf, Events.Key, new KeyEventArgs(CreateStroke()));

            order.ShouldBe([
                "app-Preview",
                "middle-Preview",
                "leaf-Preview",
                "leaf-Bubble",
                "leaf-default",
                "middle-Bubble",
                "middle-default",
                "app-Bubble",
                "app-default",
            ]);
            return;

            void Record(ControlBase control, string name) =>
                _ = control.AddHandler(
                    Events.Key,
                    (_, eventArgs) => order.Add($"{name}-{eventArgs.Phase}"));
        }, TestContext.Current.CancellationToken);
    }

    private static Stroke CreateStroke() => new(
        Code.Enter,
        character: null,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press);

    #endregion
}
