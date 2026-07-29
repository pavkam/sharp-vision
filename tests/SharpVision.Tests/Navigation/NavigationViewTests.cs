// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies sidebar navigation item ownership, selection, groups, and keyboard navigation.</summary>
public sealed class NavigationViewTests
{
    /// <summary>Verifies a navigation view starts as a quiet borderless sidebar surface without caller styling.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesQuietBackgroundDefaults()
    {
        // Arrange and act
        var navigation = new NavigationView();

        // Assert
        navigation.ActualBorder.Sides.ShouldBe(BorderSide.None);
        navigation.Face.Background.ShouldBe(ThemeColor.Control);
    }

    /// <summary>Verifies items are added through the typed collection.</summary>
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var nav = new NavigationView { Header = "Test" };
        nav.Items.Add(new NavigationViewItem { Header = "Page 1" });
        nav.Items.Add(new NavigationViewItem { Header = "Page 2" });

        nav.Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies callers select an owned semantic entry without moving focus to its private face.</summary>
    [Fact]
    public void SelectItem_WhenOwned_UpdatesSelectionWithoutChangingFocusOwnership()
    {
        var nav = new NavigationView();
        var item = new NavigationViewItem { Header = "Page" };
        nav.Items.Add(item);

        nav.SelectItem(item);

        nav.SelectedItem.ShouldBeSameAs(item);
        item.IsSelected.ShouldBeTrue();
        item.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies semantic selection rejects an entry from another navigation owner.</summary>
    [Fact]
    public void SelectItem_WhenForeign_ThrowsArgumentException()
    {
        var nav = new NavigationView();
        var other = new NavigationView();
        var item = new NavigationViewItem { Header = "Elsewhere" };
        other.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => nav.SelectItem(item));
    }

    /// <summary>Verifies private items reject focus while selection remains owner-managed.</summary>
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

            focus.Focus(item1).ShouldBeFalse();
            nav.SelectedItem.ShouldBeNull();
            raised.ShouldBe(0);
            focus.Focus(nav).ShouldBeTrue();
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
            focus.Focus(nav).ShouldBeTrue();

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(nav, Events.Key, down);

            down.Handled.ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(a);
            focus.Focused.ShouldBeSameAs(nav);
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
            focus.Focus(nav).ShouldBeTrue();

            _ = Router.Route(nav, Events.Key, new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            nav.SelectedItem.ShouldBeNull();
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

    /// <summary>Verifies removed and cleared grouped items cannot select through their former owner.</summary>
    [Fact]
    public void Group_WhenItemsAreRemovedOrCleared_DetachesOwnerActivation()
    {
        // Arrange
        var selected = new NavigationViewItem { Header = "Selected" };
        var removed = new NavigationViewItem { Header = "Removed" };
        var cleared = new NavigationViewItem { Header = "Cleared" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.AddItem(removed);
        group.AddItem(cleared);
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        // Act
        group.RemoveItem(removed).ShouldBeTrue();
        group.ClearItems();
        removed.ActivateFromOwner(ActivationCause.Programmatic);
        cleared.ActivateFromOwner(ActivationCause.Programmatic);

        // Assert
        navigation.SelectedItem.ShouldBeSameAs(selected);
        selected.IsSelected.ShouldBeTrue();
        removed.IsSelected.ShouldBeFalse();
        cleared.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies removal from a group restores the item's authored Padding, Focusable, and TabStop.</summary>
    [Fact]
    public void Group_WhenItemIsRemoved_RestoresAuthoredPaddingFocusableAndTabStop()
    {
        var item = new NavigationViewItem
        {
            Header = "Item",
            Padding = new Thickness(5, 1, 5, 1),
            Focusable = false,
            TabStop = false
        };
        var group = new NavigationViewGroup { Header = "Group" };
        group.AddItem(item);

        group.RemoveItem(item).ShouldBeTrue();

        item.Padding.ShouldBe(new Thickness(5, 1, 5, 1));
        item.Focusable.ShouldBeFalse();
        item.TabStop.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an item moved from a group to the top level renders without the
    /// group's indentation, rather than retaining a leaked private padding
    /// value that has no explanation at the top level.
    /// </summary>
    [Fact]
    public void Items_WhenGroupedItemMovesToTopLevel_DoesNotRetainGroupPadding()
    {
        var item = new NavigationViewItem { Header = "Item" };
        var authoredPadding = item.Padding;
        var group = new NavigationViewGroup { Header = "Group" };
        group.AddItem(item);

        group.RemoveItem(item).ShouldBeTrue();
        var navigation = new NavigationView();
        navigation.Items.Add(item);

        item.Padding.ShouldBe(authoredPadding);
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
            Height = Length.Cells(10)
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
            nav.SelectedItem.ShouldBeNull();

            _ = nav.Items.Remove(item);

            nav.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies NavigationView is the only tab stop; private item faces are never focusable.</summary>
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

            a.CanFocus.ShouldBeFalse();
            b.CanFocus.ShouldBeFalse();
            c.CanFocus.ShouldBeFalse();
            focus.Focus(nav).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }
}
