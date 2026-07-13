namespace SharpVision.Tests.Controls;

using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using KeyAction = Terminal.Input.Action;
using TerminalStyle = Terminal.Rendering.Style;

/// <summary>Verifies CheckBox transitions, events, ownership, styling, and cells.</summary>
public sealed class CheckBoxTests
{
    /// <summary>Verifies defaults and the two-state user cycle.</summary>
    [Fact]
    public void Activate_WhenTwoState_CyclesFalseAndTrue()
    {
        var checkBox = new CheckBox();

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
        var checkBox = new CheckBox { IsThreeState = true };

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
        var checkBox = new CheckBox { IsChecked = true };

        _ = Should.Throw<ArgumentException>(() => checkBox.IsChecked = null);

        checkBox.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies disabling three-state normalizes null with exact notification order.</summary>
    [Fact]
    public void IsThreeState_WhenDisabledFromNull_CommitsFalseBeforeEvents()
    {
        var checkBox = new CheckBox { IsThreeState = true, IsChecked = null };
        var order = new List<string>();
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
        var checkBox = new CheckBox();
        var order = new List<string>();
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
        var checkBox = new CheckBox();
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
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(1))),
            (State.Checked, new ThemeOverlay(foreground: Color.Indexed(5))));
        var checkBox = new CheckBox { Style = style, IsChecked = true };

        checkBox.Foreground.ShouldBe(Color.Indexed(5));
    }

    /// <summary>Verifies mark, separator, Unicode content, layout, and cells.</summary>
    [Fact]
    public void Render_WhenCheckedWithContent_WritesExactMarkAndUnicodeCells()
    {
        var content = new ControlText("界");
        var checkBox = new CheckBox { Content = content, IsChecked = true };
        new Engine().Layout(checkBox, new Size(4, 1));
        using var frame = new Frame(new Size(4, 1));

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
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        var checkBox = new CheckBox { Style = style };
        new Engine().Layout(checkBox, new Size(1, 1));
        using var frame = new Frame(new Size(1, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        checkBox.Render(frame.Canvas);

        frame.GetCell(default).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies bracket and tick mark styles reserve stable documented cell widths.</summary>
    [Theory]
    [InlineData(CheckBoxStyle.Brackets, "[x]", 5)]
    [InlineData(CheckBoxStyle.Tick, "✓", 3)]
    public void Render_WhenMarkStyleChanges_UsesExpectedMarkAndStableContentOffset(
        CheckBoxStyle style,
        string mark,
        int width)
    {
        var content = new ControlText("Go");
        var checkBox = new CheckBox
        {
            Content = content,
            IsChecked = true,
            MarkStyle = style,
        };
        var size = new Size(width, 1);
        new Engine().Layout(checkBox, size);
        using var frame = new Frame(size);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(mark[..1]);
        FrameOracle.Get(frame, new Point(style == CheckBoxStyle.Brackets ? 4 : 2, 0)).ShouldBe("G");
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
