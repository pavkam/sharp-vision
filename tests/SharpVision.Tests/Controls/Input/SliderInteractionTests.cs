// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies every Slider keyboard, wheel, pointer, geometry, and lifecycle interaction through a
/// mounted terminal surface, complementing the appearance-oriented SliderSurfaceTests.</summary>
public sealed class SliderInteractionTests
{
    /// <summary>Verifies each horizontal keyboard command commits its exact value with ordered event
    /// arguments, and the vertical-only arrows leave a horizontal slider untouched.</summary>
    /// <param name="code">The key to press.</param>
    /// <param name="expected">The value after the press.</param>
    [Theory]
    [InlineData(Code.Left, 45)]
    [InlineData(Code.Right, 55)]
    [InlineData(Code.PageUp, 70)]
    [InlineData(Code.PageDown, 30)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.End, 100)]
    [InlineData(Code.Up, 50)]
    [InlineData(Code.Down, 50)]
    public async Task Keyboard_WhenHorizontalCommandIsPressed_CommitsExactValueAsync(Code code, int expected)
    {
        // Arrange
        var slider = Horizontal();
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        slider.Value.ShouldBe(expected);
        changes.ShouldBe(expected == 50 ? [] : [(50, expected)]);
        surface.ShouldHaveFocus(slider);
    }

    /// <summary>Verifies each vertical keyboard command commits its exact value, and the horizontal-only
    /// arrows leave a vertical slider untouched.</summary>
    /// <param name="code">The key to press.</param>
    /// <param name="expected">The value after the press.</param>
    [Theory]
    [InlineData(Code.Up, 55)]
    [InlineData(Code.Down, 45)]
    [InlineData(Code.PageUp, 70)]
    [InlineData(Code.PageDown, 30)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.End, 100)]
    [InlineData(Code.Left, 50)]
    [InlineData(Code.Right, 50)]
    public async Task Keyboard_WhenVerticalCommandIsPressed_CommitsExactValueAsync(Code code, int expected)
    {
        // Arrange
        var slider = Vertical();
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(3, 13),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        slider.Value.ShouldBe(expected);
        changes.ShouldBe(expected == 50 ? [] : [(50, expected)]);
    }

    /// <summary>Verifies key repeat keeps stepping, while Shift and Ctrl chords leave the value alone.</summary>
    [Fact]
    public async Task Keyboard_WhenArrowRepeatsOrCarriesModifiers_StepsOnlyForBareRepeatAsync()
    {
        // Arrange
        var slider = Horizontal();
        slider.SmallChange = 1;
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Right);
        slider.Value.ShouldBe(51);
        await surface.Keyboard.RepeatAsync(Code.Right);
        slider.Value.ShouldBe(52);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        slider.Value.ShouldBe(52);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control);
        slider.Value.ShouldBe(52);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        slider.Value.ShouldBe(52);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Alt);
        slider.Value.ShouldBe(52);
    }

    /// <summary>Verifies wheel gestures apply SmallChange along the slider's axis - a horizontal slider
    /// also accepts vertical wheel ticks - while a vertical slider ignores horizontal ticks.</summary>
    [Fact]
    public async Task Pointer_WhenWheelMoves_AppliesSmallChangePerAxisAsync()
    {
        // Arrange
        var horizontal = Horizontal();
        var vertical = Vertical();
        var stack = new Stack { Orientation = Orientation.Horizontal, Children = { horizontal, vertical } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 13),
            TestContext.Current.CancellationToken);

        // Act and assert horizontal
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelX: 1);
        horizontal.Value.ShouldBe(55);
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelX: -1);
        horizontal.Value.ShouldBe(50);
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelY: 1);
        horizontal.Value.ShouldBe(55);
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelY: -1);
        horizontal.Value.ShouldBe(50);

        // Act and assert vertical
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelY: 1);
        vertical.Value.ShouldBe(55);
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelY: -1);
        vertical.Value.ShouldBe(50);
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelX: 1);
        vertical.Value.ShouldBe(50);
        horizontal.IsFocused.ShouldBeFalse();
        vertical.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies a captured horizontal drag beyond either rail end clamps to the endpoint, and a
    /// move within the same cell commits nothing.</summary>
    [Fact]
    public async Task Pointer_WhenHorizontalDragLeavesRail_ClampsToEndpointsAsync()
    {
        // Arrange
        var slider = Horizontal();
        slider.Value = 0;
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(slider);
        await surface.Pointer.MovePressedToAsync(new Point(15, 0));
        slider.Value.ShouldBe(100);
        await surface.Pointer.MovePressedToAsync(new Point(15, 2));
        await surface.Pointer.MovePressedToAsync(new Point(0, 2));
        slider.Value.ShouldBe(0);
        await surface.Pointer.ReleaseAsync();

        // Assert
        changes.ShouldBe([(0, 50), (50, 100), (100, 0)]);
        surface.ShouldHaveCapture(null);
        slider.IsPressed.ShouldBeFalse();
        surface.Cell(new Point(0, 0)).Text.ShouldBe("◆");
    }

    /// <summary>Verifies a captured vertical drag beyond either rail end clamps: below the rail selects
    /// Minimum and above it selects Maximum.</summary>
    [Fact]
    public async Task Pointer_WhenVerticalDragLeavesRail_ClampsToEndpointsAsync()
    {
        // Arrange
        var slider = Vertical();
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(3, 13),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.MoveToAsync(slider, new Point(0, 5));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        await surface.Pointer.MovePressedToAsync(new Point(0, 12));
        slider.Value.ShouldBe(0);
        surface.Cell(new Point(0, 10)).Text.ShouldBe("◆");
        await surface.Pointer.MovePressedToAsync(new Point(2, 0));
        slider.Value.ShouldBe(100);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("◆");
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a resize while a drag is in flight maps later pointer motion through the live
    /// rail, so the thumb stays under the pointer instead of jumping through the stale press-time length.</summary>
    [Fact]
    public async Task Pointer_WhenRailResizesDuringDrag_MapsMotionAgainstLiveGeometryAsync()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, HorizontalAlignment = HorizontalAlignment.Stretch };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);

        // Act
        await surface.ResizeAsync(new Size(21, 1));
        slider.Bounds.Width.ShouldBe(21);
        await surface.Pointer.MovePressedToAsync(new Point(10, 0));

        // Assert the thumb is under the pointer on the grown rail
        slider.Value.ShouldBe(50);
        surface.Cell(new Point(10, 0)).Text.ShouldBe("◆");
        surface.ShouldHaveCapture(slider);

        // Act
        await surface.Pointer.MovePressedToAsync(new Point(20, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        slider.Value.ShouldBe(100);
        surface.Cell(new Point(20, 0)).Text.ShouldBe("◆");
    }

    /// <summary>Verifies zero SmallChange and LargeChange make arrows, pages, and wheel silent no-ops
    /// while Home and End still jump to the endpoints.</summary>
    [Fact]
    public async Task Input_WhenStepsAreZero_ArrowsPagesAndWheelCommitNothingAsync()
    {
        // Arrange
        var slider = Horizontal();
        slider.SmallChange = 0;
        slider.LargeChange = 0;
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Pointer.WheelAsync(slider, new Point(5, 0), wheelX: 1);

        // Assert
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();

        // Act
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert
        changes.ShouldBe([(50, 100), (100, 0)]);
    }

    /// <summary>Verifies a collapsed range (Minimum equals Maximum) renders its thumb at the rail start
    /// and ignores every key, wheel, and click without raising events.</summary>
    [Fact]
    public async Task Input_WhenRangeIsCollapsed_KeepsTheSingleValueAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Minimum = 7,
            Maximum = 7,
            Value = 7,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(1)
        };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("◆────");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.PageUp);
        await surface.Pointer.WheelAsync(slider, new Point(2, 0), wheelX: 1);
        await surface.Pointer.ClickAsync(slider, new Point(3, 0));

        // Assert
        slider.Value.ShouldBe(7);
        changes.ShouldBeEmpty();
        surface.ShouldRender("◆────");
    }

    /// <summary>Verifies a one-cell rail renders only the thumb, and a click on it keeps the current
    /// value: a single cell has no travel, so it can only ever map Minimum, and collapsing the value
    /// there would be a change the pointer never asked for.</summary>
    [Fact]
    public async Task Pointer_WhenRailIsOneCell_ClickKeepsValueAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("◆");

        // Act
        await surface.Pointer.ClickAsync(slider);

        // Assert
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveFocus(slider);
        surface.ShouldRender("◆");
    }

    /// <summary>Verifies a local glyph family paints the track, fill, and thumb runes for both
    /// orientations, including the vertical fill that runs below the thumb.</summary>
    [Fact]
    public async Task Render_WhenGlyphFamilyIsLocal_PaintsTrackFillAndThumbPerOrientationAsync()
    {
        // Arrange
        var style = SliderStyle.Default with
        {
            Glyphs = new SliderGlyphs(new Rune('.'), new Rune('='), new Rune(':'), new Rune('#'), new Rune('T'))
        };
        var horizontal = new Slider
        {
            Maximum = 100,
            Value = 50,
            Style = style,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(1)
        };
        var vertical = new Slider
        {
            Maximum = 100,
            Value = 50,
            Style = style,
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(1),
            Height = Length.Cells(5)
        };
        var stack = new Stack { Orientation = Orientation.Horizontal, Children = { horizontal, vertical } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(6, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ==T..:
                                  :
                                  T
                                  #
                                  #
                             """);

        // Act
        await surface.Pointer.ClickAsync(vertical, new Point(0, 0));
        await surface.Pointer.ClickAsync(horizontal, new Point(0, 0));

        // Assert
        vertical.Value.ShouldBe(100);
        horizontal.Value.ShouldBe(0);
        surface.ShouldRender("""
                             T....T
                                  #
                                  #
                                  #
                                  #
                             """);
    }

    /// <summary>Verifies a Tab during a drag moves focus, cancels the drag, and releases capture so later
    /// held motion commits nothing, and a terminal pointer-leave mid drag cancels the same way.</summary>
    [Fact]
    public async Task Pointer_WhenFocusMovesOrTerminalLeavesDuringDrag_CancelsDragAsync()
    {
        // Arrange
        var slider = Horizontal();
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { slider, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 5),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(slider);

        // Act Tab mid drag
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveCapture(null);
        slider.IsPressed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(slider, new Point(9, 0));
        slider.Value.ShouldBe(50);
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);

        // Act terminal leave mid drag
        await surface.Pointer.MoveToAsync(slider, new Point(2, 0));
        await surface.Pointer.PressAsync();
        slider.Value.ShouldBe(20);
        await surface.Pointer.LeaveAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        slider.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(slider);
        slider.Value.ShouldBe(20);
    }

    /// <summary>Verifies a secondary press never selects or captures, a press on the padding outside the
    /// rail is ignored, and a press on the rail maps relative to the rail's own origin.</summary>
    [Fact]
    public async Task Pointer_WhenPressIsSecondaryOrOnPadding_DoesNotSelectAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            Padding = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(13),
            Height = Length.Cells(1)
        };
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(7, 0)).Text.ShouldBe("◆");

        // Act and assert secondary press
        await surface.Pointer.RightClickAsync(slider, new Point(3, 0));
        slider.Value.ShouldBe(50);
        surface.ShouldHaveCapture(null);
        slider.IsFocused.ShouldBeFalse();

        // Act and assert padding press
        await surface.Pointer.ClickAsync(slider, new Point(0, 0));
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();

        // Act and assert rail press mapped from the rail origin
        await surface.Pointer.ClickAsync(slider, new Point(12, 0));
        slider.Value.ShouldBe(100);
        changes.ShouldBe([(50, 100)]);
        surface.ShouldHaveFocus(slider);
    }

    /// <summary>Verifies a disabled slider takes no focus and ignores clicks and wheel, then resumes
    /// after re-enabling.</summary>
    [Fact]
    public async Task Input_WhenDisabled_IgnoresEveryGestureUntilReenabledAsync()
    {
        // Arrange
        var slider = Horizontal();
        slider.IsEnabled = false;
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Pointer.ClickAsync(slider, new Point(10, 0));
        await surface.Pointer.WheelAsync(slider, new Point(5, 0), wheelX: 1);

        // Assert
        slider.IsFocused.ShouldBeFalse();
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveState(slider, VisualState.Disabled);

        // Act
        await surface.UpdateAsync(() => slider.IsEnabled = true, "re-enable Slider");
        await surface.Pointer.ClickAsync(slider, new Point(10, 0));

        // Assert
        slider.Value.ShouldBe(100);
        surface.ShouldHaveFocus(slider);
    }

    /// <summary>Verifies lowering Maximum below the mounted value clamps it, raises the exact event, and
    /// moves the thumb to the rail end; raising Minimum above the value behaves symmetrically.</summary>
    [Fact]
    public async Task Range_WhenEndpointExcludesValueWhileMounted_ClampsAndRedrawsThumbAsync()
    {
        // Arrange
        var slider = Horizontal();
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("◆");

        // Act
        await surface.UpdateAsync(() => slider.Maximum = 40, "lower Maximum below Value");

        // Assert
        slider.Value.ShouldBe(40);
        changes.ShouldBe([(50, 40)]);
        surface.Cell(new Point(10, 0)).Text.ShouldBe("◆");

        // Act
        await surface.UpdateAsync(() => slider.Minimum = 40, "raise Minimum to Value");
        await surface.UpdateAsync(() => slider.Maximum = 60, "widen Maximum");
        await surface.UpdateAsync(() => slider.Minimum = 55, "raise Minimum above Value");

        // Assert
        slider.Value.ShouldBe(55);
        changes.ShouldBe([(50, 40), (40, 55)]);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("◆");
    }

    /// <summary>Verifies a GotFocus handler that disables the slider during a press stops the value
    /// commit and the drag from starting.</summary>
    [Fact]
    public async Task Pointer_WhenFocusCallbackDisablesSlider_CommitsNothingAsync()
    {
        // Arrange
        var slider = Horizontal();
        slider.GotFocus += (_, _) => slider.IsEnabled = false;
        var changes = Record(slider);
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(slider, new Point(10, 0));
        await surface.Pointer.PressAsync();

        // Assert
        slider.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(slider, VisualState.Disabled);
        await surface.Pointer.ReleaseAsync();
        slider.Value.ShouldBe(50);
    }

    private static Slider Horizontal() => new()
    {
        Maximum = 100,
        Value = 50,
        SmallChange = 5,
        LargeChange = 20,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(11),
        Height = Length.Cells(1)
    };

    private static Slider Vertical() => new()
    {
        Maximum = 100,
        Value = 50,
        SmallChange = 5,
        LargeChange = 20,
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(1),
        Height = Length.Cells(11)
    };

    private static List<(int Previous, int Value)> Record(Slider slider)
    {
        List<(int Previous, int Value)> changes = [];
        slider.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        return changes;
    }
}
