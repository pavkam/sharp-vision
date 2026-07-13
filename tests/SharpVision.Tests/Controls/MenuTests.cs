namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using KeyAction = Terminal.Input.Action;

/// <summary>Verifies typed menu ownership, selection navigation, check states, and cells.</summary>
public sealed class MenuTests
{
    /// <summary>Verifies typed collection ownership selects the first available item and renders vertical markers.</summary>
    [Fact]
    public void Items_WhenAdded_UseTypedOwnershipSelectionAndVerticalCells()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Header = "Open" });
        menu.Items.Add(new MenuItem { Header = "Pinned", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });
        var size = new Size(12, 5);
        new Engine().Layout(menu, size);
        using var frame = new Frame(size);

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
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Header = "First" };
            var separator = new MenuItem { Kind = MenuItemKind.Separator };
            var second = new MenuItem { Header = "Second" };
            menu.Items.Add(first);
            menu.Items.Add(separator);
            menu.Items.Add(second);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
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
        var check = new MenuItem { Header = "Auto save", Kind = MenuItemKind.Check };
        var first = new MenuItem { Header = "Small", Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        var second = new MenuItem { Header = "Large", Kind = MenuItemKind.Radio, GroupName = "size" };
        var observed = new List<string>();
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
