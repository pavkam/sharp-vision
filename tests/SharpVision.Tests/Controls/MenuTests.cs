// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Terminal.Input;


using KeyAction = Terminal.Input.Action;

/// <summary>Verifies typed menu ownership, selection navigation, check states, and cells.</summary>
public sealed class MenuTests
{
    /// <summary>Verifies typed collection ownership selects the first available item and renders vertical markers.</summary>
    [Fact]
    public void Items_WhenAdded_UseTypedOwnershipSelectionAndVerticalCells()
    {
        Menu menu = new() { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Header = "Open" });
        menu.Items.Add(new MenuItem { Header = "Pinned", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });
        Size size = new(12, 5);
        new Engine().Layout(menu, size);
        using Frame frame = new(size);

        menu.Render(frame.Canvas);

        menu.Items.Count.ShouldBe(3);
        menu.SelectedIndex.ShouldBe(0);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("[");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("x");
        FrameOracle.Get(frame, new Point(0, 4)).ShouldBe("─");
    }

    /// <summary>Verifies directional keys skip separators, commit selection state, and move focus to the active item.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsSeparatorAndFocusesNextItemAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            Menu menu = new() { Orientation = Orientation.Vertical };
            MenuItem first = new() { Header = "First" };
            MenuItem separator = new() { Kind = MenuItemKind.Separator };
            MenuItem second = new() { Header = "Second" };
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
        Menu menu = new();
        MenuItem check = new() { Header = "Auto save", Kind = MenuItemKind.Check };
        MenuItem first = new() { Header = "Small", Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        MenuItem second = new() { Header = "Large", Kind = MenuItemKind.Radio, GroupName = "size" };
        List<string> observed = [];
        menu.Items.Add(check);
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.ItemInvoked += (_, eventArgs) => observed.Add($"{eventArgs.Item.Header}:{eventArgs.Item.IsChecked}");

        check.PerformInvoke();
        second.PerformInvoke();

        check.IsChecked.ShouldBeTrue();
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        observed.ShouldBe(["Auto save:True", "Large:True"]);
    }
}
