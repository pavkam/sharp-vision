// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Proves the modality manager's and modal session's guard conditions: root validation,
/// single ownership, inactive-scope inclusion, and session entry reuse and failure cleanup.</summary>
public sealed class ModalityConditionTests
{
    /// <summary>Verifies a second manager over an already-owned root is rejected without
    /// disturbing the first, and that disposing the first releases the root for a successor.</summary>
    [Fact]
    public async Task Constructor_WhenRootIsAlreadyOwned_ThrowsAndLeavesFirstManagerIntactAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            var first = new ModalityManager(root, focus, pointer);

            // Act
            var failure = Should.Throw<ArgumentException>(() => new ModalityManager(root, focus, pointer));

            // Assert
            failure.ParamName.ShouldBe("root");
            root.ModalityOwner.ShouldBeSameAs(first);
            using (var scope = first.Enter(plane))
            {
                scope.IsActive.ShouldBeTrue();
                first.Active.ShouldBeSameAs(scope);
            }

            // Act
            first.Dispose();
            using var second = new ModalityManager(root, focus, pointer);

            // Assert
            root.ModalityOwner.ShouldBeSameAs(second);
            using var successor = second.Enter(plane);
            successor.IsActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Include on an exited scope is rejected and the plane is unchanged.</summary>
    [Fact]
    public async Task Include_WhenScopeHasExited_ThrowsWithoutChangingPlaneAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            var extra = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Children.Add(extra);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var scope = modality.Enter(plane);
            scope.Dispose();
            scope.IsActive.ShouldBeFalse();

            // Act
            var failure = Should.Throw<InvalidOperationException>(() => scope.Include(extra));

            // Assert
            failure.Message.ShouldContain("inactive");
            modality.Active.ShouldBeNull();
            modality.ActiveRootCount.ShouldBe(0);
            scope.RootCount.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a session that already owns an active scope rejects a second,
    /// non-reentrant entry before invoking the entry delegate.</summary>
    [Fact]
    public async Task Enter_WhenSessionAlreadyOwnsActiveScope_ThrowsBeforeEnteringAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();
            var entries = 0;
            var first = session.Enter(
                () =>
                {
                    entries++;
                    return modality.Enter(plane, OutsideInteraction.Dismiss);
                },
                static () => true);
            first.IsActive.ShouldBeTrue();

            // Act
            var failure = Should.Throw<InvalidOperationException>(() => session.Enter(
                () =>
                {
                    entries++;
                    return modality.Enter(plane, OutsideInteraction.Dismiss);
                },
                static () => true));

            // Assert
            failure.Message.ShouldContain("already owns");
            entries.ShouldBe(1);
            session.Current.ShouldBeSameAs(first);
            session.IsActive.ShouldBeTrue();
            modality.Active.ShouldBeSameAs(first);

            session.Exit();
            first.IsActive.ShouldBeFalse();
            session.Current.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a currency check that throws after a live scope was entered disposes that
    /// scope, runs the rollback once, clears the session, and rethrows the check's failure.</summary>
    [Fact]
    public async Task Enter_WhenCurrencyCheckThrowsAfterLiveScope_DisposesScopeAndRethrowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();
            var rollbacks = 0;
            ModalScope? entered = null;
            var exited = 0;

            // Act
            var failure = Should.Throw<InvalidOperationException>(() => session.Enter(
                () =>
                {
                    entered = modality.Enter(plane, OutsideInteraction.Dismiss);
                    entered.Exited += (_, _) => exited++;
                    return entered;
                },
                static () => throw new InvalidOperationException("stale"),
                () => rollbacks++));

            // Assert
            failure.Message.ShouldBe("stale");
            entered.ShouldNotBeNull().IsActive.ShouldBeFalse();
            exited.ShouldBe(1);
            rollbacks.ShouldBe(1);
            session.Current.ShouldBeNull();
            session.IsActive.ShouldBeFalse();
            session.IsEntering.ShouldBeFalse();
            modality.Active.ShouldBeNull();

            // Act - the session is reusable afterwards
            using var replacement = session.Enter(
                () => modality.Enter(plane, OutsideInteraction.Dismiss),
                static () => true);

            // Assert
            replacement.IsActive.ShouldBeTrue();
            session.Current.ShouldBeSameAs(replacement);
            session.Exit();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the entry failure stays authoritative when the scope's own Exited
    /// subscriber also fails during cleanup.</summary>
    [Fact]
    public async Task Enter_WhenCurrencyCheckAndCleanupBothFail_RethrowsTheEntryFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();

            // Act
            var failure = Should.Throw<InvalidOperationException>(() => session.Enter(
                () =>
                {
                    var scope = modality.Enter(plane, OutsideInteraction.Dismiss);
                    scope.Exited += (_, _) => throw new NotSupportedException("cleanup");
                    return scope;
                },
                static () => throw new InvalidOperationException("stale")));

            // Assert
            failure.Message.ShouldBe("stale");
            session.Current.ShouldBeNull();
            modality.Active.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a scope the session has exited publishes no dismissal at all - the scope's
    /// own inactive guard returns before any subscriber runs and the session unsubscribed on
    /// exit - while the replacement scope the session now owns still reaches the policy. There is
    /// no public route that delivers a dismissal from an active scope the session stopped
    /// tracking, so this is the whole observable guarantee.</summary>
    [Fact]
    public async Task PublishDismissRequested_WhenScopeWasExitedBySession_ReachesNoPolicyWhileReplacementDoesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer();
            var plane = new ProbeContainer { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var dismissals = new List<ModalScope>();
            var session = new ModalSession(dismissRequested: dismissals.Add);
            var first = session.Enter(() => modality.Enter(plane, OutsideInteraction.Dismiss), static () => true);
            var firstObserved = 0;
            first.DismissRequested += (_, _) => firstObserved++;
            session.Exit();
            first.IsActive.ShouldBeFalse();
            var second = session.Enter(() => modality.Enter(plane, OutsideInteraction.Dismiss), static () => true);

            // Act
            first.PublishDismissRequested();
            second.PublishDismissRequested();

            // Assert
            firstObserved.ShouldBe(0);
            dismissals.ShouldBe([second]);
            session.Exit();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the middle scope of a three-deep stack unwinds youngest-first
    /// (C exits, then B), leaves A active as the plane, restores focus into A, and confines Tab to
    /// A's descendants afterwards.</summary>
    [Fact]
    public async Task Dispose_WhenMiddleOfThreeScopesIsDisposed_UnwindsYoungestFirstAndResumesOldestPlaneAsync()
    {
        // Arrange - three disjoint planes, each with two tab stops
        var a1 = new Button { Text = "A1" };
        var a2 = new Button { Text = "A2" };
        var b1 = new Button { Text = "B1" };
        var c1 = new Button { Text = "C1" };
        var planeA = new Stack { Children = { a1, a2 } };
        var planeB = new Stack { Children = { b1 } };
        var planeC = new Stack { Children = { c1 } };
        Overlay.SetLeft(planeB, Length.Cells(10));
        Overlay.SetLeft(planeC, Length.Cells(20));
        var root = new Overlay { Children = { planeA, planeB, planeC } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 6),
            TestContext.Current.CancellationToken);
        var order = new List<string>();
        ModalScope? scopeA = null;
        ModalScope? scopeB = null;
        ModalScope? scopeC = null;
        await surface.UpdateAsync(
            () =>
            {
                scopeA = surface.Application.Modality.Enter(planeA, initialFocus: a2);
                scopeA.Exited += (_, _) => order.Add("A");
                scopeB = surface.Application.Modality.Enter(planeB);
                scopeB.Exited += (_, _) => order.Add("B");
                scopeC = surface.Application.Modality.Enter(planeC);
                scopeC.Exited += (_, _) => order.Add("C");
            },
            "enter three stacked scopes");
        surface.ShouldHaveFocus(c1);
        surface.Application.Modality.Active.ShouldBeSameAs(scopeC);

        // Act
        await surface.UpdateAsync(() => scopeB.ShouldNotBeNull().Dispose(), "dispose the middle scope");

        // Assert
        order.ShouldBe(["C", "B"]);
        scopeC.ShouldNotBeNull().IsActive.ShouldBeFalse();
        scopeB.ShouldNotBeNull().IsActive.ShouldBeFalse();
        scopeA.ShouldNotBeNull().IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scopeA);
        surface.ShouldHaveFocus(a2);

        // Act - Tab stays inside A's plane
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(a1);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(a2);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert
        surface.ShouldHaveFocus(a1);
        b1.IsFocused.ShouldBeFalse();
        c1.IsFocused.ShouldBeFalse();

        // Act
        await surface.UpdateAsync(() => scopeA.Dispose(), "dispose the oldest scope");

        // Assert
        order.ShouldBe(["C", "B", "A"]);
        surface.Application.Modality.Active.ShouldBeNull();
    }
}
