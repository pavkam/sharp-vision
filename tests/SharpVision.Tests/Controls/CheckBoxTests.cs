// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


using SharpVision.Terminal.Input;


using ControlText = SharpVision.Controls.Text;
using KeyAction = Terminal.Input.Action;
using TerminalStyle = CellStyle;

/// <summary>Verifies CheckBox transitions, events, ownership, styling, and cells.</summary>
public sealed class CheckBoxTests
{
    /// <summary>Verifies defaults and the two-state user cycle.</summary>
    [Fact]
    public void Activate_WhenTwoState_CyclesFalseAndTrue()
    {
        CheckBox checkBox = new();

        checkBox.IsChecked.ShouldBe(false);
        checkBox.IsThreeState.ShouldBeFalse();
        checkBox.Content.ShouldBeNull();
        checkBox.Marks.ShouldBe(Marks.Default);
        checkBox.PerformToggle();
        checkBox.IsChecked.ShouldBe(true);
        checkBox.PerformToggle();
        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies three-state activation follows false, true, null, false.</summary>
    [Fact]
    public void Activate_WhenThreeState_CyclesInDocumentedOrder()
    {
        CheckBox checkBox = new() { IsThreeState = true };

        checkBox.PerformToggle();
        checkBox.PerformToggle();
        checkBox.IsChecked.ShouldBeNull();
        checkBox.PerformToggle();

        checkBox.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies invalid null assignment throws before changing two-state value.</summary>
    [Fact]
    public void IsChecked_WhenNullInTwoState_ThrowsBeforeMutation()
    {
        CheckBox checkBox = new() { IsChecked = true };

        _ = Should.Throw<ArgumentException>(() => checkBox.IsChecked = null);

        checkBox.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies disabling three-state normalizes null with exact notification order.</summary>
    [Fact]
    public void IsThreeState_WhenDisabledFromNull_CommitsFalseBeforeEvents()
    {
        CheckBox checkBox = new() { IsThreeState = true, IsChecked = null };
        List<string> order = [];
        checkBox.Unchecked += (_, eventArgs) =>
        {
            checkBox.IsThreeState.ShouldBeFalse();
            checkBox.IsChecked.ShouldBe(false);
            eventArgs.Previous.ShouldBeNull();
            order.Add("unchecked");
        };
        checkBox.StateChanged += (_, _) => order.Add("changed");

        checkBox.IsThreeState = false;

        order.ShouldBe(["unchecked", "changed"]);
    }

    /// <summary>Verifies specific events precede StateChanged from committed programmatic state.</summary>
    [Fact]
    public void IsChecked_WhenChangedProgrammatically_RaisesSpecificThenGeneral()
    {
        CheckBox checkBox = new();
        List<string> order = [];
        checkBox.Checked += (_, eventArgs) =>
        {
            checkBox.IsChecked.ShouldBe(true);
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("checked");
        };
        checkBox.StateChanged += (_, _) => order.Add("changed");

        checkBox.IsChecked = true;

        order.ShouldBe(["checked", "changed"]);
    }

    /// <summary>Verifies Space activation uses the shared keyboard path.</summary>
    [Fact]
    public void Route_WhenSpaceCompletes_TogglesWithKeyboardCause()
    {
        CheckBox checkBox = new();
        ActivationCause? cause = null;
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;

        Key(checkBox, KeyAction.Press);
        Key(checkBox, KeyAction.Release);

        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Keyboard);
    }

    /// <summary>Verifies checked state participates in resolved style composition.</summary>
    [Fact]
    public void Foreground_WhenChecked_IncludesCheckedOverlay()
    {
        ControlStyle<Control> style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(1))),
            (State.Checked, new ThemeOverlay(foreground: Color.Indexed(5))));
        CheckBox checkBox = new() { Style = style, IsChecked = true };

        checkBox.Foreground.ShouldBe(Color.Indexed(5));
    }

    /// <summary>Verifies mark, separator, Unicode content, layout, and cells.</summary>
    [Fact]
    public void Render_WhenCheckedWithContent_WritesExactMarkAndUnicodeCells()
    {
        ControlText content = new("界");
        CheckBox checkBox = new() { Content = content, IsChecked = true };
        new Engine().Layout(checkBox, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        checkBox.Render(frame.Canvas);

        checkBox.DesiredSize.ShouldBe(new Size(4, 1));
        FrameOracle.Get(frame, default).ShouldBe("☑");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("界");
        frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies a foreground-only CheckBox style preserves the parent surface background.</summary>
    [Fact]
    public void Render_WhenStyleHasForegroundOnly_PreservesSurfaceBackground()
    {
        ControlStyle<Control> style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        CheckBox checkBox = new() { Style = style };
        new Engine().Layout(checkBox, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        checkBox.Render(frame.Canvas);

        frame.GetCell(default).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies bracket and tick mark styles reserve stable documented cell widths.</summary>
    [Theory]
    [InlineData(CheckBoxMarks.Brackets, "[x]", 5)]
    [InlineData(CheckBoxMarks.Tick, "✓", 3)]
    public void Render_WhenMarkStyleChanges_UsesExpectedMarkAndStableContentOffset(
        CheckBoxMarks style,
        string mark,
        int width)
    {
        ControlText content = new("Go");
        CheckBox checkBox = new()
        {
            Content = content,
            IsChecked = true,
            MarkStyle = style,
        };
        Size size = new(width, 1);
        new Engine().Layout(checkBox, size);
        using Frame frame = new(size);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(mark[..1]);
        FrameOracle.Get(frame, new Point(style == CheckBoxMarks.Brackets ? 4 : 2, 0)).ShouldBe("G");
    }

    /// <summary>Verifies wide and control marks are rejected during construction.</summary>
    [Theory]
    [InlineData('\n')]
    [InlineData('界')]
    public void Constructor_WhenMarkIsInvalid_Throws(char value)
    {
        _ = Should.Throw<ArgumentException>(() => new Marks(
            new Rune(value),
            new Rune('x'),
            new Rune('-')));
    }

    private static void Key(CheckBox checkBox, KeyAction action) => Router.Route(
        checkBox,
        Events.Key,
        new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            action)));
}
