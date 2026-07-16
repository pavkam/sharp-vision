// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;



/// <summary>Verifies transactional focus, navigation, and invalid-state cleanup.</summary>
public sealed class FocusTests
{
    /// <summary>Verifies making the focused control ineligible commits uncancellable cleanup before notification returns.</summary>
    [Fact]
    public async Task CanFocus_WhenFocusedControlBecomesFalse_ReleasesFocusSynchronouslyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl() { CanFocus = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child).ShouldBeTrue();
            var changingCalls = 0;
            var lostCalls = 0;
            var order = new List<string>();
            manager.Changing += (_, eventArgs) =>
            {
                changingCalls++;
                eventArgs.Cancel = true;
            };
            manager.Lost += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeNull();
                child.CanFocus.ShouldBeFalse();
                child.IsFocused.ShouldBeFalse();
                eventArgs.Previous.ShouldBeSameAs(child);
                eventArgs.Current.ShouldBeNull();
                lostCalls++;
                order.Add("lost");
            };
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Control.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    child.IsFocused.ShouldBeFalse();
                    order.Add("can-focus");
                }
            };

            child.CanFocus = false;

            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            changingCalls.ShouldBe(0);
            lostCalls.ShouldBe(1);
            order.ShouldBe(["lost", "can-focus"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies ineligibility raised inside a focus notification is cleaned before the enclosing request returns.</summary>
    [Fact]
    public async Task Focus_WhenGainedMakesControlIneligible_CleansBeforeRequestReturnsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl() { CanFocus = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            var focusReturned = false;
            var gainedCalls = 0;
            var lostCalls = 0;
            var notificationCalls = 0;
            manager.Gained += (_, eventArgs) =>
            {
                eventArgs.Current.ShouldBeSameAs(child);
                gainedCalls++;
                child.CanFocus = false;
            };
            manager.Lost += (_, eventArgs) =>
            {
                focusReturned.ShouldBeFalse();
                manager.Focused.ShouldBeNull();
                child.CanFocus.ShouldBeFalse();
                child.IsFocused.ShouldBeFalse();
                eventArgs.Previous.ShouldBeSameAs(child);
                eventArgs.Current.ShouldBeNull();
                lostCalls++;
            };
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Control.CanFocus))
                {
                    focusReturned.ShouldBeFalse();
                    manager.Focused.ShouldBeNull();
                    child.IsFocused.ShouldBeFalse();
                    notificationCalls++;
                }
            };

            manager.Focus(child).ShouldBeTrue();
            focusReturned = true;

            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            gainedCalls.ShouldBe(1);
            lostCalls.ShouldBe(1);
            notificationCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one control's local focus eligibility does not evict a focused descendant.</summary>
    [Fact]
    public async Task CanFocus_WhenAncestorBecomesFalse_PreservesDescendantFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer() { CanFocus = true };
            var child = new ProbeControl() { CanFocus = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child).ShouldBeTrue();

            root.CanFocus = false;

            manager.Focused.ShouldBeSameAs(child);
            child.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one focus transaction drains eligibility notifications for both old and new targets.</summary>
    [Fact]
    public async Task Focus_WhenOldAndNewTargetsBecomeIneligible_PublishesEveryDeferredChangeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var previous = new ProbeControl() { CanFocus = true };
            var next = new ProbeControl() { CanFocus = true };
            root.Children.Add(previous);
            root.Children.Add(next);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(previous).ShouldBeTrue();
            var previousNotifications = 0;
            var nextNotifications = 0;
            previous.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Control.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    previousNotifications++;
                }
            };
            next.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Control.CanFocus))
                {
                    manager.Focused.ShouldBeNull();
                    nextNotifications++;
                }
            };
            manager.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, next))
                {
                    previous.CanFocus = false;
                }
            };
            manager.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, next))
                {
                    next.CanFocus = false;
                }
            };

            manager.Focus(next).ShouldBeTrue();

            manager.Focused.ShouldBeNull();
            previousNotifications.ShouldBe(1);
            nextNotifications.ShouldBe(1);
            previous.CanFocus = true;
            previous.CanFocus = false;
            previousNotifications.ShouldBe(3);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies commit happens before lost and gained callbacks.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsEligible_CommitsBeforeNotificationsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            List<string> order = [];
            var root = new RecordingControl("root", order);
            var first = new ProbeControl() { CanFocus = true };
            var second = new ProbeControl() { CanFocus = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
            manager.Focus(first).ShouldBeTrue();
            order.Clear();
            manager.Changing += (_, eventArgs) =>
            {
                eventArgs.Previous.ShouldBeSameAs(first);
                eventArgs.Next.ShouldBeSameAs(second);
                order.Add("preview");
            };
            manager.Lost += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                first.IsFocused.ShouldBeFalse();
                second.IsFocused.ShouldBeTrue();
                eventArgs.Previous.ShouldBeSameAs(first);
                order.Add("lost");
            };
            manager.Gained += (_, eventArgs) =>
            {
                manager.Focused.ShouldBeSameAs(second);
                eventArgs.Current.ShouldBeSameAs(second);
                order.Add("gained");
            };

            manager.Focus(second).ShouldBeTrue();

            order.ShouldBe(["preview", "lost", "gained"]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies preview cancellation leaves the complete old state intact.</summary>
    [Fact]
    public async Task Focus_WhenPreviewCancels_PreservesPreviousFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl() { CanFocus = true };
            var second = new ProbeControl() { CanFocus = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
            manager.Focus(first).ShouldBeTrue();
            manager.Changing += (_, eventArgs) => eventArgs.Cancel = true;

            manager.Focus(second).ShouldBeFalse();

            manager.Focused.ShouldBeSameAs(first);
            first.IsFocused.ShouldBeTrue();
            second.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies tab order uses index then tree order and wraps both directions.</summary>
    [Fact]
    public async Task MoveNext_WhenTreeHasFocusableControls_OrdersAndWrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var first = new ProbeControl() { CanFocus = true, TabIndex = 1 };
            var second = new ProbeControl() { CanFocus = true, TabIndex = 0 };
            var third = new ProbeControl() { CanFocus = true, TabIndex = 1 };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(second);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(first);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(third);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(second);
            manager.MoveNext(reverse: true).ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(third);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies membership and eligibility reject invalid explicit targets.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsForeignOrIneligible_RejectsWithoutMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var hidden = new ProbeControl()
            {
                CanFocus = true,
                Visibility = Visibility.Hidden,
            };
            var foreign = new ProbeControl() { CanFocus = true };
            root.Children.Add(hidden);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);

            manager.Focus(hidden).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => manager.Focus(foreign));
            manager.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext wraps within a Cycle scope instead of traversing globally.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsCycle_WrapsWithinScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outside = new ProbeControl() { CanFocus = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl() { CanFocus = true };
            var inner2 = new ProbeControl() { CanFocus = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            root.Children.Add(outside);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(inner1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
            manager.MoveNext(reverse: true).ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext traps focus within a Contained scope.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsContained_TrapsFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outside = new ProbeControl() { CanFocus = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Contained };
            var inner1 = new ProbeControl() { CanFocus = true };
            var inner2 = new ProbeControl() { CanFocus = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            root.Children.Add(outside);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(inner1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext returns false for an empty scope.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeIsEmpty_ReturnsFalseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer() { CanFocus = true, TabNavigation = TabNavigation.Cycle };
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(scope).ShouldBeTrue();

            manager.MoveNext().ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext wraps to the same control when the scope has one tab stop.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeHasSingleTabStop_WrapsToSelfAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var only = new ProbeControl() { CanFocus = true };
            scope.Children.Add(only);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(only).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(only);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies nested scopes use the innermost scope for Tab traversal.</summary>
    [Fact]
    public async Task MoveNext_WhenScopesAreNested_UsesInnermostAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outer = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var outerChild = new ProbeControl() { CanFocus = true };
            var inner = new ProbeContainer() { TabNavigation = TabNavigation.Contained };
            var innerA = new ProbeControl() { CanFocus = true };
            var innerB = new ProbeControl() { CanFocus = true };
            inner.Children.Add(innerA);
            inner.Children.Add(innerB);
            outer.Children.Add(outerChild);
            outer.Children.Add(inner);
            root.Children.Add(outer);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(innerA).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(innerB);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(innerA);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the scope root is excluded from its own scope's tab-stop candidates.</summary>
    [Fact]
    public async Task MoveNext_WhenScopeRootIsTabStop_ExcludesRootFromOwnScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var scope = new ProbeContainer()
            {
                CanFocus = true,
                IsTabStop = true,
                TabNavigation = TabNavigation.Cycle,
            };
            var child1 = new ProbeControl() { CanFocus = true };
            var child2 = new ProbeControl() { CanFocus = true };
            scope.Children.Add(child1);
            scope.Children.Add(child2);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(child1).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(child2);
            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(child1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext from outside a Cycle scope can enter the scope.</summary>
    [Fact]
    public async Task MoveNext_WhenOutsideCycleScope_EntersScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl() { CanFocus = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var inner1 = new ProbeControl() { CanFocus = true };
            var inner2 = new ProbeControl() { CanFocus = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            var after = new ProbeControl() { CanFocus = true };
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(before).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveNext from outside a Contained scope can enter it, and Tab then traps inside.</summary>
    [Fact]
    public async Task MoveNext_WhenOutsideContainedScope_EntersAndTrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl() { CanFocus = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Contained };
            var inner1 = new ProbeControl() { CanFocus = true };
            var inner2 = new ProbeControl() { CanFocus = true };
            scope.Children.Add(inner1);
            scope.Children.Add(inner2);
            var after = new ProbeControl() { CanFocus = true };
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Children.Add(after);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);
            manager.Focus(before).ShouldBeTrue();

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner2);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(inner1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab traversal visits controls, enters scopes, and exits correctly in a mixed tree.</summary>
    [Fact]
    public async Task MoveNext_WhenTreeHasMixedScopes_TraversesFullyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { CanFocus = true };
            var menu = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var m1 = new ProbeControl() { CanFocus = true };
            var m2 = new ProbeControl() { CanFocus = true };
            menu.Children.Add(m1);
            menu.Children.Add(m2);
            var b = new ProbeControl() { CanFocus = true };
            root.Children.Add(a);
            root.Children.Add(menu);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var manager = new FocusManager(root);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(a);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m1);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m2);

            manager.MoveNext().ShouldBeTrue();
            manager.Focused.ShouldBeSameAs(m1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disable, hide, detach, and preview mutation clear or reject safely.</summary>
    [Fact]
    public async Task Focus_WhenTreeMutates_ReleasesInvalidReferencesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl() { CanFocus = true };
            var replacement = new ProbeControl() { CanFocus = true };
            root.Children.Add(child);
            root.Children.Add(replacement);
            root.Attach(dispatcher);
            using FocusManager manager = new(root);
            manager.Focus(child).ShouldBeTrue();

            root.IsEnabled = false;
            manager.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            root.IsEnabled = true;
            manager.Focus(child).ShouldBeTrue();
            child.Visibility = Visibility.Hidden;
            manager.Focused.ShouldBeNull();
            child.Visibility = Visibility.Visible;
            manager.Focus(child).ShouldBeTrue();
            _ = root.Children.Remove(child);
            manager.Focused.ShouldBeNull();

            manager.Changing += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Next, replacement))
                {
                    _ = root.Children.Remove(replacement);
                }
            };
            manager.Focus(replacement).ShouldBeFalse();
            manager.Focused.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }
}
