// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


using SharpVision.Terminal.Input;


using ControlText = SharpVision.Controls.Text;
using KeyAction = Terminal.Input.Action;
using TerminalStyle = CellStyle;

/// <summary>Verifies RadioButton grouping, transactions, navigation, ownership, and cells.</summary>
public sealed class RadioButtonTests
{
    /// <summary>Verifies groups start empty and user activation selects without toggling off.</summary>
    [Fact]
    public void PerformSelect_WhenGroupStartsEmpty_SelectsOnlyOnce()
    {
        RadioButton radio = new();
        int checkedEvents = 0;
        radio.Checked += (_, _) => checkedEvents++;

        radio.PerformSelect();
        radio.PerformSelect();

        radio.IsChecked.ShouldBeTrue();
        checkedEvents.ShouldBe(1);
    }

    /// <summary>Verifies null-name siblings remain mutually exclusive.</summary>
    [Fact]
    public void IsChecked_WhenSiblingSelectionChanges_CommitsExclusiveStateBeforeEvents()
    {
        Stack parent = new();
        RadioButton first = new() { IsChecked = true };
        RadioButton second = new();
        parent.Children.Add(first);
        parent.Children.Add(second);
        List<string> order = [];
        first.Unchecked += (_, eventArgs) =>
        {
            first.IsChecked.ShouldBeFalse();
            second.IsChecked.ShouldBeTrue();
            eventArgs.Previous.ShouldBeSameAs(first);
            eventArgs.Current.ShouldBeSameAs(second);
            order.Add("old");
        };
        second.Checked += (_, _) => order.Add("new");
        second.SelectionChanged += (_, _) => order.Add("changed");

        second.IsChecked = true;

        order.ShouldBe(["old", "new", "changed"]);
    }

    /// <summary>Verifies named groups span containers but remain scoped to one root.</summary>
    [Fact]
    public void IsChecked_WhenNamedMembersAreInDifferentContainers_UsesRootScope()
    {
        Stack root = new();
        Stack left = new();
        Stack right = new();
        root.Children.Add(left);
        root.Children.Add(right);
        RadioButton first = new() { GroupName = "density", IsChecked = true };
        RadioButton second = new() { GroupName = "density" };
        RadioButton unrelated = new() { GroupName = "theme", IsChecked = true };
        left.Children.Add(first);
        right.Children.Add(second);
        right.Children.Add(unrelated);

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        unrelated.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies attaching a prechecked member resolves duplicate selection atomically.</summary>
    [Fact]
    public void Add_WhenCheckedMemberJoinsGroup_UnchecksExistingMember()
    {
        Stack parent = new();
        RadioButton first = new() { IsChecked = true };
        RadioButton second = new() { IsChecked = true };
        parent.Children.Add(first);

        parent.Children.Add(second);

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies regrouping a selected member resolves its new group.</summary>
    [Fact]
    public void GroupName_WhenCheckedMemberMoves_ResolvesNewGroupAfterCommit()
    {
        Stack parent = new();
        RadioButton first = new() { GroupName = "a", IsChecked = true };
        RadioButton second = new() { GroupName = "b", IsChecked = true };
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.GroupName = "b";

        first.IsChecked.ShouldBeTrue();
        second.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies reentrant old-member handlers suppress stale outer notifications.</summary>
    [Fact]
    public void IsChecked_WhenUncheckedHandlerReselects_DoesNotReportStaleSelection()
    {
        Stack parent = new();
        RadioButton first = new() { IsChecked = true };
        RadioButton second = new();
        RadioButton third = new();
        parent.Children.Add(first);
        parent.Children.Add(second);
        parent.Children.Add(third);
        int secondChecked = 0;
        first.Unchecked += (_, _) => third.IsChecked = true;
        second.Checked += (_, _) =>
        {
            second.IsChecked.ShouldBeTrue();
            secondChecked++;
        };

        second.IsChecked = true;

        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeTrue();
        secondChecked.ShouldBe(0);
    }

    /// <summary>Verifies arrows skip unavailable members, wrap, focus, and select.</summary>
    [Fact]
    public async Task Route_WhenArrowMoves_UsesEligibleGroupOrderAndWrapsAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            Stack root = new();
            RadioButton first = new();
            RadioButton skipped = new() { IsEnabled = false };
            RadioButton third = new();
            root.Children.Add(first);
            root.Children.Add(skipped);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(first).ShouldBeTrue();

            Key(first, Code.Right);
            focus.Focused.ShouldBeSameAs(third);
            third.IsChecked.ShouldBeTrue();
            Key(third, Code.Right);

            focus.Focused.ShouldBeSameAs(first);
            first.IsChecked.ShouldBeTrue();
            third.IsChecked.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies content layout and selection marks render exact semantic cells.</summary>
    [Fact]
    public void Render_WhenSelectedWithUnicodeContent_WritesExactCells()
    {
        RadioButton radio = new()
        {
            IsChecked = true,
            Content = new ControlText("界"),
        };
        new Engine().Layout(radio, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        radio.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("◉");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("界");
        frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a foreground-only RadioButton style preserves the parent surface background.</summary>
    [Fact]
    public void Render_WhenStyleHasForegroundOnly_PreservesSurfaceBackground()
    {
        ControlStyle<Control> style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        RadioButton radio = new() { Style = style };
        new Engine().Layout(radio, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        radio.Render(frame.Canvas);

        frame.GetCell(default).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies programmatic false leaves a valid empty group.</summary>
    [Fact]
    public void IsChecked_WhenSetFalse_AllowsNoSelection()
    {
        RadioButton radio = new() { IsChecked = true };

        radio.IsChecked = false;

        radio.IsChecked.ShouldBeFalse();
    }

    private static void Key(RadioButton radio, Code code) => Router.Route(
        radio,
        Events.Key,
        new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
}
