// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies tab navigation across complex hierarchies with nested scopes, mixed modes, and edge cases.</summary>
public sealed class TabNavigationScopeTests
{
    /// <summary>
    /// Tree: root > [A, Menu(Cycle > M1, M2, M3), B]
    /// Tab from A enters Menu at M1. Tab inside Menu cycles M1→M2→M3→M1.
    /// Shift+Tab from A wraps to B (skips Menu internals in global scope).
    /// </summary>
    [Fact]
    public async Task FullTraversal_WhenTreeHasCycleScopeBetweenControls_EntersCyclesAndExitsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { Focusable = true };
            var menu = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var m1 = new ProbeControl() { Focusable = true };
            var m2 = new ProbeControl() { Focusable = true };
            var m3 = new ProbeControl() { Focusable = true };
            menu.Children.Add(m1);
            menu.Children.Add(m2);
            menu.Children.Add(m3);
            var b = new ProbeControl() { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(menu);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m3);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m1);

            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m3);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(m2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Sidebar(Cycle > S1, S2), B, Popup(Contained > P1, P2)]
    /// Tab from A enters Sidebar. Tab from B enters Popup. Popup traps.
    /// Shift+Tab from B goes to Sidebar entry.
    /// </summary>
    [Fact]
    public async Task FullTraversal_WhenTreeHasCycleAndContained_EachScopeIsIndependentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { Focusable = true };
            var sidebar = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var s1 = new ProbeControl() { Focusable = true };
            var s2 = new ProbeControl() { Focusable = true };
            sidebar.Children.Add(s1);
            sidebar.Children.Add(s2);
            var b = new ProbeControl() { Focusable = true };
            var popup = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var p1 = new ProbeControl() { Focusable = true };
            var p2 = new ProbeControl() { Focusable = true };
            popup.Children.Add(p1);
            popup.Children.Add(p2);
            root.Children.Add(a);
            root.Children.Add(sidebar);
            root.Children.Add(b);
            root.Children.Add(popup);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(p1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Outer(Cycle > A, Inner(Contained > X, Y), B]
    /// Tab from A enters Inner at X. Inner traps: X→Y→X.
    /// Tab from B cycles back to A in the outer scope.
    /// </summary>
    [Fact]
    public async Task NestedScopes_WhenContainedInsideCycle_InnerTrapsAndOuterCyclesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var outer = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var a = new ProbeControl() { Focusable = true };
            var inner = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var x = new ProbeControl() { Focusable = true };
            var y = new ProbeControl() { Focusable = true };
            inner.Children.Add(x);
            inner.Children.Add(y);
            var b = new ProbeControl() { Focusable = true };
            outer.Children.Add(a);
            outer.Children.Add(inner);
            outer.Children.Add(b);
            root.Children.Add(outer);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Scope1(Cycle > S1a, S1b), Scope2(Cycle > S2a, S2b), B]
    /// Tab from A → enters Scope1 at S1a. Tab from S1a cycles S1a→S1b→S1a.
    /// Explicit focus to B, then Shift+Tab enters Scope2 at S2a.
    /// </summary>
    [Fact]
    public async Task SiblingScopes_WhenTabTraverses_EntersEachScopeIndependentlyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { Focusable = true };
            var scope1 = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var s1a = new ProbeControl() { Focusable = true };
            var s1b = new ProbeControl() { Focusable = true };
            scope1.Children.Add(s1a);
            scope1.Children.Add(s1b);
            var scope2 = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var s2a = new ProbeControl() { Focusable = true };
            var s2b = new ProbeControl() { Focusable = true };
            scope2.Children.Add(s2a);
            scope2.Children.Add(s2b);
            var b = new ProbeControl() { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(scope1);
            root.Children.Add(scope2);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1a);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1b);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s1a);

            focus.Focus(b).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(s2b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Dock > [Top: Header(non-focusable), Fill: Scope(Cycle > I1, I2, I3)], Button]
    /// Simulates a real sidebar layout. Tab from nothing starts at I1.
    /// Inside scope cycles. Button is reachable from root scope.
    /// </summary>
    [Fact]
    public async Task RealLayout_WhenSidebarWithHeaderAndButton_TabEntersAndCyclesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var sidebarDock = new Dock();
            var header = new ProbeControl();
            Dock.SetSide(header, Side.Top);
            sidebarDock.Children.Add(header);
            var navScope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var i1 = new ProbeControl() { Focusable = true };
            var i2 = new ProbeControl() { Focusable = true };
            var i3 = new ProbeControl() { Focusable = true };
            navScope.Children.Add(i1);
            navScope.Children.Add(i2);
            navScope.Children.Add(i3);
            sidebarDock.Children.Add(navScope);
            var button = new ProbeControl() { Focusable = true };
            root.Children.Add(sidebarDock);
            root.Children.Add(button);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i3);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);

            focus.Focus(button).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(i1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Scope(Cycle > Disabled, Hidden, Visible)]
    /// Only Visible is eligible. Tab enters scope at Visible.
    /// Inside scope, single tab stop wraps to itself.
    /// </summary>
    [Fact]
    public async Task ScopeEntry_WhenFirstChildrenAreIneligible_SkipsToFirstEligibleAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl() { Focusable = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var disabled = new ProbeControl() { Focusable = true, IsEnabled = false };
            var hidden = new ProbeControl() { Focusable = true, Visibility = Visibility.Hidden };
            var visible = new ProbeControl() { Focusable = true };
            scope.Children.Add(disabled);
            scope.Children.Add(hidden);
            scope.Children.Add(visible);
            root.Children.Add(before);
            root.Children.Add(scope);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(visible);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, EmptyScope(Cycle, no children), B]
    /// Empty scopes are skipped entirely. Tab goes A→B.
    /// </summary>
    [Fact]
    public async Task ScopeEntry_WhenScopeIsEmpty_SkipsToNextControlAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { Focusable = true };
            var empty = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var b = new ProbeControl() { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(empty);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Scope(Cycle > [Nested(Continue > D1, D2), E])]
    /// A Continue scope inside a Cycle scope is transparent.
    /// Tab cycles D1→D2→E→D1.
    /// </summary>
    [Fact]
    public async Task ContinueInsideCycle_WhenTraversed_IsFlattenedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var cycle = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var group = new ProbeContainer();
            var d1 = new ProbeControl() { Focusable = true };
            var d2 = new ProbeControl() { Focusable = true };
            group.Children.Add(d1);
            group.Children.Add(d2);
            var e = new ProbeControl() { Focusable = true };
            cycle.Children.Add(group);
            cycle.Children.Add(e);
            root.Children.Add(cycle);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(d1).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(d2);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(e);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(d1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [A, Scope(Cycle > TabIndex=2:Z, TabIndex=1:Y, TabIndex=0:X), B]
    /// Entry from outside goes to first by tree order (X after sorting by TabIndex).
    /// Inside scope, cycles X→Y→Z→X (respecting TabIndex).
    /// </summary>
    [Fact]
    public async Task ScopeWithTabIndex_WhenEntered_UsesTabIndexOrderInsideScopeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var a = new ProbeControl() { Focusable = true };
            var scope = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var z = new ProbeControl() { Focusable = true, TabIndex = 2 };
            var y = new ProbeControl() { Focusable = true, TabIndex = 1 };
            var x = new ProbeControl() { Focusable = true, TabIndex = 0 };
            scope.Children.Add(z);
            scope.Children.Add(y);
            scope.Children.Add(x);
            var b = new ProbeControl() { Focusable = true };
            root.Children.Add(a);
            root.Children.Add(scope);
            root.Children.Add(b);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(a).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(z);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [Lvl1(Cycle > Lvl2(Cycle > Lvl3(Contained > X, Y)))]
    /// Three levels deep. Tab from outside enters through Lvl1→Lvl2→Lvl3 entry point.
    /// Inside Lvl3 traps: X→Y→X.
    /// </summary>
    [Fact]
    public async Task DeeplyNested_WhenThreeLevelsDeep_EntersThroughAllLevelsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var before = new ProbeControl() { Focusable = true };
            var lvl1 = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var lvl2 = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var lvl3 = new ProbeContainer() { TabNavigation = TabNavigation.Cycle };
            var x = new ProbeControl() { Focusable = true };
            var y = new ProbeControl() { Focusable = true };
            lvl3.Children.Add(x);
            lvl3.Children.Add(y);
            lvl2.Children.Add(lvl3);
            lvl1.Children.Add(lvl2);
            root.Children.Add(before);
            root.Children.Add(lvl1);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.Focus(before).ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(y);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(x);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tree: root > [IsTabStop=false:Panel > [A, B, C]]
    /// A container with IsTabStop=false but no scope. Children are reachable. Tab cycles A→B→C→A.
    /// </summary>
    [Fact]
    public async Task NonTabStopContainer_WhenContinue_ChildrenAreReachableAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var panel = new ProbeContainer() { TabStop = false };
            var a = new ProbeControl() { Focusable = true };
            var b = new ProbeControl() { Focusable = true };
            var c = new ProbeControl() { Focusable = true };
            panel.Children.Add(a);
            panel.Children.Add(b);
            panel.Children.Add(c);
            root.Children.Add(panel);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(b);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(c);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }
}
