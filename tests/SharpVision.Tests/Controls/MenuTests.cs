// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies typed menu ownership, selection navigation, check states, and cells.</summary>
public sealed class MenuTests
{
    /// <summary>Verifies typed collection ownership selects the first available item and renders vertical markers.</summary>
    [Fact]
    public void Items_WhenAdded_UseTypedOwnershipSelectionAndVerticalCells()
    {
        var menu = new Menu() { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        menu.Items.Add(new MenuItem { Content = new ControlText("Pinned"), Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuSeparator());
        var size = new Size(12, 5);
        new Engine().Layout(menu, size);
        using Frame frame = new(size);

        menu.Render(frame.Canvas);

        menu.Items.Count.ShouldBe(3);
        menu.SelectedIndex.ShouldBe(0);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("[");
        FrameOracle.Get(frame, new Point(0, 4)).ShouldBe("─");
    }

    /// <summary>Verifies directional keys skip separators, commit selection state, and move focus to the active item.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsSeparatorAndFocusesNextItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu() { Orientation = Orientation.Vertical };
            var first = new MenuItem() { Content = new ControlText("First") };
            var separator = new MenuSeparator();
            var second = new MenuItem() { Content = new ControlText("Second") };
            menu.Items.Add(first);
            menu.Items.Add(separator);
            menu.Items.Add(second);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(first).ShouldBeTrue();

            Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down,
                default,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(2);
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies check and named radio items commit state before menu-level invocation reporting.</summary>
    [Fact]
    public void PerformInvoke_WhenCheckAndRadioItemsActivate_CommitsStateBeforeEvent()
    {
        var menu = new Menu();
        var check = new MenuItem() { Content = new ControlText("Auto save"), Kind = MenuItemKind.Check };
        var first = new MenuItem() { Content = new ControlText("Small"), Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        var second = new MenuItem() { Content = new ControlText("Large"), Kind = MenuItemKind.Radio, GroupName = "size" };
        List<string> observed = [];
        menu.Items.Add(check);
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.ItemInvoked += (_, eventArgs) =>
            observed.Add($"{eventArgs.Item.Content.ShouldBeOfType<ControlText>().Content}:{eventArgs.Item.IsChecked}");

        check.PerformInvoke();
        second.PerformInvoke();

        check.IsChecked.ShouldBeTrue();
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        observed.ShouldBe(["Auto save:True", "Large:True"]);
    }

    /// <summary>Verifies radio property observers see a complete group commit.</summary>
    [Fact]
    public void IsChecked_WhenRadioSelectionChanges_StagesEveryFieldBeforePropertyNotifications()
    {
        var menu = new Menu();
        var first = new MenuItem() { Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        var second = new MenuItem() { Kind = MenuItemKind.Radio, GroupName = "size" };
        menu.Items.Add(first);
        menu.Items.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.IsChecked))
            {
                first.IsChecked.ShouldBeFalse();
                second.IsChecked.ShouldBeTrue();
                observed = true;
            }
        };

        second.IsChecked = true;

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies an item reports its invocation before the owning menu forwards it.</summary>
    [Fact]
    public void PerformInvoke_WhenItemActivates_RaisesItemBeforeMenuNotification()
    {
        var menu = new Menu();
        var item = new MenuItem();
        List<string> order = [];
        menu.Items.Add(item);
        item.Invoked += (_, _) => order.Add("item");
        menu.ItemInvoked += (_, _) => order.Add("menu");

        item.PerformInvoke();

        order.ShouldBe(["item", "menu"]);
    }

    /// <summary>Verifies a separator is never focusable, hit-testable, selectable, or invokable.</summary>
    [Fact]
    public async Task MenuSeparator_WhenUsed_RemainsNonInteractiveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu();
            var item = new MenuItem { Content = new ControlText("Open") };
            var separator = new MenuSeparator();
            menu.Items.Add(item);
            menu.Items.Add(separator);
            new Engine().Layout(menu, new Size(12, 1));
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);

            separator.CanFocus.ShouldBeFalse();
            separator.HitTest(new Point(separator.Bounds.X, separator.Bounds.Y)).ShouldBeNull();
            focus.Focus(separator).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => menu.SelectedIndex = 1);
            menu.SelectedIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab cycles within Menu items instead of escaping to sibling controls.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressed_CyclesWithinMenuItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var menu = new Menu() { Orientation = Orientation.Vertical };
            var a = new MenuItem() { Content = new ControlText("A") };
            var b = new MenuItem() { Content = new ControlText("B") };
            var c = new MenuItem() { Content = new ControlText("C") };
            var outside = new ProbeControl() { CanFocus = true };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            root.Children.Add(menu);
            root.Children.Add(outside);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(a).ShouldBeTrue();

            Router.Route(a, Events.Key, Tab());
            focus.Focused.ShouldBeSameAs(b);
            Router.Route(b, Events.Key, Tab());
            focus.Focused.ShouldBeSameAs(c);
            Router.Route(c, Events.Key, Tab());
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Menu syncs SelectedIndex when a MenuItem receives focus externally.</summary>
    [Fact]
    public async Task Focus_WhenMenuItemReceivesExternalFocus_SyncsSelectedIndexAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu() { Orientation = Orientation.Vertical };
            var a = new MenuItem() { Content = new ControlText("A") };
            var b = new MenuItem() { Content = new ControlText("B") };
            var c = new MenuItem() { Content = new ControlText("C") };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            menu.SelectedIndex.ShouldBe(0);

            focus.Focus(c).ShouldBeTrue();

            menu.SelectedIndex.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies arrow navigation starts from the correct position after external Tab focus.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowAfterExternalFocus_NavigatesFromFocusedItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu() { Orientation = Orientation.Vertical };
            var a = new MenuItem() { Content = new ControlText("A") };
            var b = new MenuItem() { Content = new ControlText("B") };
            var c = new MenuItem() { Content = new ControlText("C") };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(c).ShouldBeTrue();
            menu.SelectedIndex.ShouldBe(2);

            Router.Route(c, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down,
                default,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(0);
            focus.Focused.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    private static KeyEventArgs Tab() => new(new Stroke(
        Code.Tab,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    /// <summary>Verifies changing a checked item to command clears checked state before observers.</summary>
    [Fact]
    public void Kind_WhenCheckedItemBecomesCommand_StagesUncheckedStateBeforeNotification()
    {
        var item = new MenuItem { Kind = MenuItemKind.Check, IsChecked = true };
        var observed = false;
        item.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.Kind))
            {
                item.Kind.ShouldBe(MenuItemKind.Command);
                item.IsChecked.ShouldBeFalse();
                observed = true;
            }
        };

        item.Kind = MenuItemKind.Command;

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies moving a checked radio item resolves its destination before GroupName publication.</summary>
    [Fact]
    public void GroupName_WhenCheckedRadioMoves_ResolvesDestinationBeforePropertyNotification()
    {
        var menu = new Menu();
        var first = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "a", IsChecked = true };
        var second = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "b", IsChecked = true };
        menu.Items.Add(first);
        menu.Items.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.GroupName))
            {
                first.IsChecked.ShouldBeTrue();
                second.IsChecked.ShouldBeFalse();
                observed = true;
            }
        };

        first.GroupName = "b";

        observed.ShouldBeTrue();
    }
}
