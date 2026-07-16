// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies sidebar navigation item ownership, selection, groups, and keyboard navigation.</summary>
public sealed class NavigationViewTests
{
    /// <summary>Verifies items are added through the typed collection.</summary>
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var nav = new NavigationView { Header = "Test" };
        nav.Items.Add(new NavigationViewItem { Header = "Page 1" });
        nav.Items.Add(new NavigationViewItem { Header = "Page 2" });

        nav.Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies focusing an item selects it and raises SelectionChanged.</summary>
    [Fact]
    public async Task Focus_WhenItemReceivesFocus_SelectsItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var item1 = new NavigationViewItem { Header = "A" };
            var item2 = new NavigationViewItem { Header = "B" };
            nav.Items.Add(item1);
            nav.Items.Add(item2);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            var raised = 0;
            nav.SelectionChanged += (_, _) => raised++;

            focus.Focus(item1).ShouldBeTrue();

            nav.SelectedItem.ShouldBeSameAs(item1);
            raised.ShouldBe(1);
            item1.IsSelected.ShouldBeTrue();
            item2.IsSelected.ShouldBeFalse();

            focus.Focus(item2).ShouldBeTrue();

            nav.SelectedItem.ShouldBeSameAs(item2);
            raised.ShouldBe(2);
            item1.IsSelected.ShouldBeFalse();
            item2.IsSelected.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Down arrow navigates between items via the bubble handler.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_NavigatesBetweenItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Header = "A" };
            var b = new NavigationViewItem { Header = "B" };
            var c = new NavigationViewItem { Header = "C" };
            nav.Items.Add(a);
            nav.Items.Add(b);
            nav.Items.Add(c);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(a).ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(a);

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            Router.Route(a, Events.Key, down);

            down.Handled.ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(b);
            focus.Focused.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies groups contain sub-items that participate in selection.</summary>
    [Fact]
    public async Task Group_WhenSubItemActivated_SelectsSubItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Header = "General" };
            group.AddItem(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(sub).ShouldBeTrue();

            Router.Route(sub, Events.Key, new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            nav.SelectedItem.ShouldBeSameAs(sub);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies collapsing a group hides its sub-items.</summary>
    [Fact]
    public async Task Group_WhenCollapsed_HidesSubItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Header = "General" };
            group.AddItem(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);

            group.IsExpanded = false;

            sub.EffectiveIsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies separator is non-focusable and non-hit-testable.</summary>
    [Fact]
    public void Separator_WhenCreated_IsNonInteractive()
    {
        var separator = new NavigationViewSeparator();

        separator.CanFocus.ShouldBeFalse();
        separator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies footer items are accessible through the FooterItems collection.</summary>
    [Fact]
    public void FooterItems_WhenAdded_IncreasesFooterCount()
    {
        var nav = new NavigationView();
        nav.FooterItems.Add(new NavigationViewItem { Header = "Quit" });

        nav.FooterItems.Count.ShouldBe(1);
        nav.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies header renders when set.</summary>
    [Fact]
    public void Header_WhenSet_IsRendered()
    {
        var nav = new NavigationView
        {
            Header = "My App",
            Width = Length.Cells(24),
            Height = Length.Cells(10),
        };
        var size = new Size(24, 10);
        new Engine().Layout(nav, size);
        using Frame frame = new(size);

        nav.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("M");
    }

    /// <summary>Verifies removing an item clears selection if it was selected.</summary>
    [Fact]
    public async Task Items_WhenSelectedItemRemoved_ClearsSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var item = new NavigationViewItem { Header = "A" };
            nav.Items.Add(item);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(item).ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(item);

            _ = nav.Items.Remove(item);

            nav.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies only the selected item is a tab stop; others are focusable but not tab stops.</summary>
    [Fact]
    public async Task Selection_WhenChanged_OnlySelectedItemIsTabStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Header = "A" };
            var b = new NavigationViewItem { Header = "B" };
            var c = new NavigationViewItem { Header = "C" };
            nav.Items.Add(a);
            nav.Items.Add(b);
            nav.Items.Add(c);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);

            a.IsTabStop.ShouldBeFalse();
            b.IsTabStop.ShouldBeFalse();
            c.IsTabStop.ShouldBeFalse();

            focus.Focus(a).ShouldBeTrue();

            a.IsTabStop.ShouldBeTrue();
            b.IsTabStop.ShouldBeFalse();
            c.IsTabStop.ShouldBeFalse();

            focus.Focus(b).ShouldBeTrue();

            a.IsTabStop.ShouldBeFalse();
            b.IsTabStop.ShouldBeTrue();
            c.IsTabStop.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }
}
