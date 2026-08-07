// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies modal-plane validation, ownership, stacking, and cleanup.</summary>
public sealed class ModalityManagerTests
{
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
            var first = new ProbeContainer { Focusable = true };
            var second = new ProbeContainer { Focusable = true };
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
            outer.Active.ShouldBeFalse();
            inner.Active.ShouldBeFalse();
            order.ShouldBe(["inner", "outer"]);
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
            var outerFocus = new ProbeControl { Focusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { Focusable = true };
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
            outer.Active.ShouldBeTrue();
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
            var disabled = new ProbeContainer { Enabled = false };
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
            scope.Active.ShouldBeTrue();
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
            var eligible = new ProbeControl { Focusable = true };
            var ineligible = new ProbeControl();
            var foreign = new ProbeControl { Focusable = true };
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
            scope.Active.ShouldBeTrue();
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
            outer.Active.ShouldBeFalse();
            inner.Active.ShouldBeFalse();
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

            includedAncestor.Enabled = false;

            scope.Active.ShouldBeTrue();
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
            var dying = new OwnershipObserverControl { Focusable = true };
            var unrelated = new ProbeControl { Focusable = true };
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
                scope.Active.ShouldBeFalse();
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
            scope.Active.ShouldBeFalse();
            dying.Focused.ShouldBeFalse();
            dying.UnavailableReasons.ShouldBe([UnavailableReasonFor(mutation)]);
            var surviving = replacement.ShouldNotBeNull();
            modality.Active.ShouldBeSameAs(surviving);
            focus.Focused.ShouldBeSameAs(unrelated);
            unrelated.Focused.ShouldBeTrue();

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
            var plane = new ProbeControl { Focusable = true };
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
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            plane.Focused.ShouldBeFalse();
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
            var parent = new ProbeContainer { Focusable = true };
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
            scope.Active.ShouldBeFalse();
            modality.Active.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            parent.Focused.ShouldBeFalse();
            parent.Disposed.ShouldBeTrue();
            child.Disposed.ShouldBeTrue();
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
            var primary = new ProbeControl { Focusable = true };
            var dying = new OwnershipObserverControl { Focusable = true };
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
            scope.Active.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(scope);
            modality.ActiveRootCount.ShouldBe(1);
            modality.ActiveRootAt(0).ShouldBeSameAs(primary);
            focus.Focused.ShouldBeSameAs(primary);
            primary.Focused.ShouldBeTrue();
            dying.Focused.ShouldBeFalse();
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
            var guarded = new OwnershipObserverControl { Focusable = true };
            var safe = new ProbeControl { Focusable = true };
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
            safe.Focused.ShouldBeTrue();
            guarded.Focused.ShouldBeFalse();
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
            var background = new ProbeControl { Focusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { Focusable = true };
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

            scope.Active.ShouldBeFalse();
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
            var savedInside = new ProbeControl { Focusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { Focusable = true };
            var outsideFallback = new ProbeControl { Focusable = true };
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
            outer.Active.ShouldBeTrue();
            inner.Active.ShouldBeFalse();
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
            scope.Active.ShouldBeFalse();
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
            var background = new ProbeControl { Focusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { Focusable = true };
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
            inner.Active.ShouldBeFalse();
            outer.Active.ShouldBeFalse();
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
            var first = new ProbeContainer { Focusable = true };
            var second = new ProbeContainer { Focusable = true };
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
                outerWasInactive = !outer.Active;

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
            inner.Active.ShouldBeFalse();
            outer.Active.ShouldBeFalse();
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
            outer.Active.ShouldBeFalse();
            inner.Active.ShouldBeFalse();
            _ = reentrant.ShouldNotBeNull();
            reentrant.Active.ShouldBeFalse();
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

            original.Active.ShouldBeFalse();
            _ = replacement.ShouldNotBeNull();
            replacement.Active.ShouldBeTrue();
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
            var background = new ProbeControl { Focusable = true };
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { Focusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { Focusable = true };
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
            inner.Active.ShouldBeFalse();
            outer.Active.ShouldBeFalse();
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
            var background = new ProbeControl { Focusable = true };
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { Focusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { Focusable = true };
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
            inner.Active.ShouldBeFalse();
            outer.Active.ShouldBeFalse();
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
            var savedInside = new ProbeControl { Focusable = true };
            var outerRoot = new ProbeContainer();
            var outerFocus = new ProbeControl { Focusable = true };
            var innerRoot = new ProbeContainer();
            var innerFocus = new ProbeControl { Focusable = true };
            var outsideFallback = new ProbeControl { Focusable = true };
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
            parent.Active.ShouldBeTrue();
            inner.Active.ShouldBeFalse();
            outer.Active.ShouldBeFalse();
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
            var background = new ProbeControl { Focusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { Focusable = true };
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

            scope.Active.ShouldBeFalse();
            restored.ShouldBe(0);
            background.Focused.ShouldBeFalse();
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
            var background = new ProbeControl { Focusable = true };
            var plane = new ProbeContainer();
            var initial = new ProbeControl { Focusable = true };
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

            scope.Active.ShouldBeFalse();
            restored.ShouldBe(0);
            background.Focused.ShouldBeFalse();
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
                control.Enabled = false;
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
            control.Enabled = true;
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
}
