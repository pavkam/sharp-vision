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

    /// <summary>Verifies public labels reject terminal controls before observable state changes.</summary>
    [Fact]
    public void Labels_WhenAssignedInvalidText_PreserveCommittedValues()
    {
        var view = new NavigationView { Header = "View" };
        var item = new NavigationViewItem { Header = "Item", Glyph = "◆" };
        var group = new NavigationViewGroup { Header = "Group" };

        _ = Should.Throw<ArgumentException>(() => view.Header = "bad\nview");
        _ = Should.Throw<ArgumentNullException>(() => item.Header = null!);
        _ = Should.Throw<ArgumentException>(() => item.Header = "bad\nitem");
        _ = Should.Throw<ArgumentException>(() => item.Glyph = "bad\tglyph");
        _ = Should.Throw<ArgumentNullException>(() => group.Header = null!);
        _ = Should.Throw<ArgumentException>(() => group.Header = "bad\rgroup");

        view.Header.ShouldBe("View");
        item.Header.ShouldBe("Item");
        item.Glyph.ShouldBe("◆");
        group.Header.ShouldBe("Group");
    }

    /// <summary>Verifies invalid typed ownership candidates preserve collections and caller properties.</summary>
    [Fact]
    public void Items_WhenCandidateIsOwnedOrDuplicated_PreserveStateBeforeThrowing()
    {
        var view = new NavigationView();
        var item = new NavigationViewItem { Header = "Item", Padding = new Thickness(1) };
        view.Items.Add(item);
        var group = new NavigationViewGroup();
        var attached = new NavigationViewItem { Header = "Attached", Padding = new Thickness(3) };
        view.FooterItems.Add(attached);

        _ = Should.Throw<ArgumentException>(() => view.Items.Add(item));
        _ = Should.Throw<ArgumentException>(() => group.AddItem(attached));

        view.Items.ShouldBe([item]);
        view.FooterItems.ShouldBe([attached]);
        item.Padding.ShouldBe(new Thickness(1));
        attached.Padding.ShouldBe(new Thickness(3));
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

    /// <summary>Verifies Down navigates between items through the bubble handler.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_NavigatesBetweenItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var first = new NavigationViewItem { Header = "A" };
            var second = new NavigationViewItem { Header = "B" };
            var third = new NavigationViewItem { Header = "C" };
            nav.Items.Add(first);
            nav.Items.Add(second);
            nav.Items.Add(third);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(first).ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(first);

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            Router.Route(first, Events.Key, down);

            down.Handled.ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(second);
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies group sub-items participate in selection.</summary>
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

    /// <summary>Verifies footer items are accessible through the footer collection.</summary>
    [Fact]
    public void FooterItems_WhenAdded_IncreasesFooterCount()
    {
        var nav = new NavigationView();
        nav.FooterItems.Add(new NavigationViewItem { Header = "Quit" });

        nav.FooterItems.Count.ShouldBe(1);
        nav.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies a non-empty header renders.</summary>
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

    /// <summary>Verifies removing the selected item clears selection.</summary>
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

    /// <summary>Verifies unavailable, collapsed, and removed selected items choose the next then previous eligible identity.</summary>
    [Fact]
    public async Task Selection_WhenSelectedEntryBecomesUnavailable_RepairsDeterministicallyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var view = new NavigationView();
            var first = new NavigationViewItem { Header = "First" };
            var child = new NavigationViewItem { Header = "Child" };
            var group = new NavigationViewGroup { Header = "Group" };
            group.AddItem(child);
            var last = new NavigationViewItem { Header = "Last" };
            view.Items.Add(first);
            view.Items.Add(group);
            view.Items.Add(last);
            view.Attach(dispatcher);
            using FocusManager focus = new(view);
            focus.Focus(first).ShouldBeTrue();

            first.IsEnabled = false;
            view.SelectedItem.ShouldBeSameAs(child);

            group.IsExpanded = false;
            view.SelectedItem.ShouldBeSameAs(last);

            _ = view.Items.Remove(last);
            view.SelectedItem.ShouldBeNull();
            first.IsTabStop.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies only the selected item is a tab stop.</summary>
    [Fact]
    public async Task Selection_WhenChanged_OnlySelectedItemIsTabStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var first = new NavigationViewItem { Header = "A" };
            var second = new NavigationViewItem { Header = "B" };
            var third = new NavigationViewItem { Header = "C" };
            nav.Items.Add(first);
            nav.Items.Add(second);
            nav.Items.Add(third);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);

            first.IsTabStop.ShouldBeTrue();
            second.IsTabStop.ShouldBeFalse();
            third.IsTabStop.ShouldBeFalse();
            focus.Focus(first).ShouldBeTrue();
            first.IsTabStop.ShouldBeTrue();
            second.IsTabStop.ShouldBeFalse();
            third.IsTabStop.ShouldBeFalse();
            focus.Focus(second).ShouldBeTrue();
            first.IsTabStop.ShouldBeFalse();
            second.IsTabStop.ShouldBeTrue();
            third.IsTabStop.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }
}
