// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies every ScrollBar keyboard, wheel, button, track, thumb-drag, geometry, and lifecycle
/// interaction through a mounted terminal surface, complementing the appearance-oriented ScrollBarSurfaceTests.</summary>
public sealed class ScrollBarInteractionTests
{
    /// <summary>Verifies the two end cells and the two cells beside the thumb apply button and paging
    /// changes per chrome: full chrome ends are SmallChange buttons, thin chrome ends are track pages.</summary>
    /// <param name="orientation">The rail orientation.</param>
    /// <param name="chrome">The chrome preset.</param>
    /// <param name="afterStartEnd">The value after clicking the first cell from 50.</param>
    [Theory]
    [InlineData(Orientation.Horizontal, ScrollBarChrome.Full, 48)]
    [InlineData(Orientation.Vertical, ScrollBarChrome.Full, 48)]
    [InlineData(Orientation.Horizontal, ScrollBarChrome.Thin, 30)]
    [InlineData(Orientation.Vertical, ScrollBarChrome.Thin, 30)]
    public async Task Pointer_WhenEndsAndTrackAreClicked_AppliesButtonOrPagingChangePerChromeAsync(
        Orientation orientation,
        ScrollBarChrome chrome,
        int afterStartEnd)
    {
        // Arrange
        var bar = NewBar(orientation, chrome);
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            orientation == Orientation.Horizontal ? new Size(14, 3) : new Size(3, 14),
            TestContext.Current.CancellationToken);

        // Act and assert the first end cell
        await surface.Pointer.ClickAsync(bar, Cell(orientation, 0));
        bar.Value.ShouldBe(afterStartEnd);

        // Act and assert the last end cell restores 50
        await surface.Pointer.ClickAsync(bar, Cell(orientation, 11));
        bar.Value.ShouldBe(50);

        // Act and assert the cells just inside the ends page in both directions
        await surface.Pointer.ClickAsync(bar, Cell(orientation, 1));
        bar.Value.ShouldBe(30);
        await surface.Pointer.ClickAsync(bar, Cell(orientation, 10));
        bar.Value.ShouldBe(50);
        changes.Select(change => (change.Previous, change.Value))
            .ShouldBe([(50, afterStartEnd), (afterStartEnd, 50), (50, 30), (30, 50)]);
        changes.ForEach(change => change.Cause.ShouldBe(ScrollCause.Pointer));
        surface.ShouldHaveFocus(bar);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies every vertical keyboard command commits its exact value with the keyboard cause,
    /// the horizontal-only arrows leave a vertical bar untouched, repeat keeps stepping, and Shift or Ctrl
    /// chords are ignored.</summary>
    /// <param name="code">The key to press.</param>
    /// <param name="expected">The value after the press.</param>
    [Theory]
    [InlineData(Code.Up, 48)]
    [InlineData(Code.Down, 52)]
    [InlineData(Code.PageUp, 30)]
    [InlineData(Code.PageDown, 70)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.End, 100)]
    [InlineData(Code.Left, 50)]
    [InlineData(Code.Right, 50)]
    public async Task Keyboard_WhenVerticalCommandIsPressed_CommitsExactValueAsync(Code code, int expected)
    {
        // Arrange
        var bar = NewBar(Orientation.Vertical, ScrollBarChrome.Full);
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(3, 14),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        bar.Value.ShouldBe(expected);
        changes.Select(change => (change.Previous, change.Value)).ShouldBe(expected == 50 ? [] : [(50, expected)]);
        changes.ForEach(change => change.Cause.ShouldBe(ScrollCause.Keyboard));
    }

    /// <summary>Verifies a horizontal bar ignores the vertical arrows, steps on key repeat, and ignores
    /// Shift and Ctrl chords.</summary>
    [Fact]
    public async Task Keyboard_WhenHorizontalBarReceivesRepeatsAndChords_StepsOnlyForBareKeysAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(14, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.PressAsync(Code.Down);
        bar.Value.ShouldBe(50);
        await surface.Keyboard.PressAsync(Code.Right);
        bar.Value.ShouldBe(52);
        await surface.Keyboard.RepeatAsync(Code.Right);
        bar.Value.ShouldBe(54);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Control);
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);
        bar.Value.ShouldBe(54);
    }

    /// <summary>Verifies a thumb drag beyond either end of the track clamps to the endpoints, reports
    /// pointer causes, and commits nothing for motion within the same cell.</summary>
    [Fact]
    public async Task Pointer_WhenThumbDragLeavesTrack_ClampsToEndpointsAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("<....##....>");

        // Act
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(bar);
        surface.ShouldHaveState(bar, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        changes.ShouldBeEmpty();
        await surface.Pointer.MovePressedToAsync(new Point(15, 0));
        bar.Value.ShouldBe(100);
        await surface.Pointer.MovePressedToAsync(new Point(15, 2));
        await surface.Pointer.MovePressedToAsync(new Point(0, 2));
        bar.Value.ShouldBe(0);
        await surface.Pointer.ReleaseAsync();

        // Assert
        changes.ShouldBe([(50, 100, ScrollCause.Pointer), (100, 0, ScrollCause.Pointer)]);
        surface.ShouldHaveCapture(null);
        bar.IsPressed.ShouldBeFalse();
        surface.ShouldRender("<##........>");
    }

    /// <summary>Verifies a terminal pointer-leave during a thumb drag cancels the drag, releases capture,
    /// and preserves the last committed value.</summary>
    [Fact]
    public async Task Pointer_WhenTerminalLeavesDuringThumbDrag_CancelsAndPreservesValueAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(bar, new Point(7, 0));
        bar.Value.ShouldBe(75);

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        bar.Value.ShouldBe(75);
        changes.Count.ShouldBe(1);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(bar, VisualState.Focused);
    }

    /// <summary>Verifies a Tab during a thumb drag moves focus, cancels the drag, and releases capture
    /// so later held motion commits nothing.</summary>
    [Fact]
    public async Task Pointer_WhenFocusMovesDuringThumbDrag_CancelsDragAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { bar, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(bar);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveCapture(null);
        bar.IsPressed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(bar, new Point(9, 0));
        bar.Value.ShouldBe(50);
        await surface.Pointer.ReleaseAsync();
        bar.Value.ShouldBe(50);
    }

    /// <summary>Verifies wheel ticks follow each rail's own axis - a vertical bar scrolls with vertical
    /// ticks only and a horizontal bar with horizontal ticks only - and unhandled ticks change nothing.</summary>
    [Fact]
    public async Task Pointer_WhenWheelMoves_AppliesSmallChangeOnlyAlongOwnAxisAsync()
    {
        // Arrange
        var vertical = NewBar(Orientation.Vertical, ScrollBarChrome.Full);
        var horizontal = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        var verticalChanges = Record(vertical);
        var horizontalChanges = Record(horizontal);
        var stack = new Stack { Orientation = Orientation.Horizontal, Children = { vertical, horizontal } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 14),
            TestContext.Current.CancellationToken);

        // Act and assert vertical
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelY: -1);
        vertical.Value.ShouldBe(52);
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelY: 1);
        vertical.Value.ShouldBe(50);
        await surface.Pointer.WheelAsync(vertical, new Point(0, 5), wheelX: 1);
        vertical.Value.ShouldBe(50);
        verticalChanges.ShouldBe([(50, 52, ScrollCause.Wheel), (52, 50, ScrollCause.Wheel)]);

        // Act and assert horizontal
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelX: 1);
        horizontal.Value.ShouldBe(52);
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelX: -1);
        horizontal.Value.ShouldBe(50);
        await surface.Pointer.WheelAsync(horizontal, new Point(5, 0), wheelY: -1);
        horizontal.Value.ShouldBe(50);
        horizontalChanges.Count.ShouldBe(2);
        vertical.IsFocused.ShouldBeFalse();
        horizontal.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies a rail with no span (Maximum equals Minimum) fills its track with the thumb and
    /// treats every click, drag, key, and wheel as a silent no-op.</summary>
    [Fact]
    public async Task Input_WhenRangeHasNoSpan_ThumbFillsTrackAndNothingMovesAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        bar.Value = 0;
        bar.Maximum = 0;
        bar.ViewportSize = 200;
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("<##########>");

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(bar);
        await surface.Pointer.MovePressedToAsync(bar, new Point(9, 0));
        await surface.Pointer.ReleaseAsync();
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Pointer.WheelAsync(bar, new Point(5, 0), wheelX: 1);

        // Assert
        bar.Value.ShouldBe(0);
        changes.ShouldBeEmpty();
        surface.ShouldHaveCapture(null);
        surface.ShouldRender("<##########>");
    }

    /// <summary>Verifies a two-cell full rail keeps working buttons with no track, and a one-cell rail
    /// degrades to a single thumb cell that neither buttons nor drags can move.</summary>
    [Fact]
    public async Task Pointer_WhenRailIsTiny_ButtonsStillWorkAndSingleCellIsInertAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = BlockGlyphs(),
            Maximum = 100,
            Value = 50,
            SmallChange = 2,
            LargeChange = 20,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(2, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("<>");

        // Act and assert buttons without a track
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        bar.Value.ShouldBe(48);
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        bar.Value.ShouldBe(50);

        // Act and assert a single cell
        await surface.ResizeAsync(new Size(1, 1));
        surface.ShouldRender("#");
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        await surface.Pointer.MoveToAsync(bar, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));
        await surface.Pointer.ReleaseAsync();
        bar.Value.ShouldBe(50);
        changes.Count.ShouldBe(2);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies the thin line presets draw the vertical and horizontal line track and thumb
    /// glyphs and that the thin track still pages on click.</summary>
    [Fact]
    public async Task Render_WhenThinLineStyleIsUsed_DrawsLineGlyphsPerOrientationAsync()
    {
        // Arrange
        var vertical = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Style = ScrollBarStyle.ThinLine,
            Maximum = 100,
            ViewportSize = 20,
            Width = Length.Cells(1),
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.ThinLine,
            Maximum = 100,
            ViewportSize = 20,
            Width = Length.Cells(6),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var stack = new Stack { Orientation = Orientation.Horizontal, Children = { vertical, horizontal } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(7, 6),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┃━─────
                             │
                             │
                             │
                             │
                             │
                             """);

        // Act
        await surface.Pointer.ClickAsync(vertical, new Point(0, 5));
        await surface.Pointer.ClickAsync(horizontal, new Point(5, 0));

        // Assert
        vertical.Value.ShouldBe(10);
        horizontal.Value.ShouldBe(10);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("│");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("┃");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("─");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("━");
    }

    /// <summary>Verifies a disabled bar takes no focus and ignores clicks, wheel, and keys, then resumes
    /// after re-enabling.</summary>
    [Fact]
    public async Task Input_WhenDisabled_IgnoresEveryGestureUntilReenabledAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        bar.IsEnabled = false;
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        await surface.Pointer.WheelAsync(bar, new Point(5, 0), wheelX: 1);

        // Assert
        bar.IsFocused.ShouldBeFalse();
        bar.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Act
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable ScrollBar");
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));

        // Assert
        bar.Value.ShouldBe(52);
        surface.ShouldHaveFocus(bar);
    }

    /// <summary>Verifies zero SmallChange and LargeChange make buttons, arrows, pages, track clicks, and
    /// wheel silent no-ops while Home and End still jump.</summary>
    [Fact]
    public async Task Input_WhenStepsAreZero_ButtonsTrackAndWheelCommitNothingAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        bar.SmallChange = 0;
        bar.LargeChange = 0;
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        await surface.Pointer.ClickAsync(bar, new Point(10, 0));
        await surface.Pointer.WheelAsync(bar, new Point(5, 0), wheelX: 1);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert
        bar.Value.ShouldBe(50);
        changes.ShouldBeEmpty();

        // Act
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert
        changes.ShouldBe([(50, 100, ScrollCause.Keyboard), (100, 0, ScrollCause.Keyboard)]);
    }

    /// <summary>Verifies a non-focusable bar still scrolls from its buttons and track without taking
    /// focus, and Tab skips it.</summary>
    [Fact]
    public async Task Pointer_WhenBarIsNotFocusable_ScrollsWithoutTakingFocusAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        bar.IsFocusable = false;
        var button = new Button("Next") { Width = Length.Cells(8), Height = Length.Cells(3) };
        var stack = new Stack { Orientation = Orientation.Vertical, Children = { bar, button } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        await surface.Pointer.DragAsync(bar, new Point(4, 0), new Point(8, 0));
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert: 52 after the button, 32 after the page, then the thumb (cells 3-4 of the
        // track at 32) dragged four cells to start 7 of an 8-cell travel maps to 88
        bar.Value.ShouldBe(88);
        bar.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a press on the bar's own padding is ignored while the rail cells beside it still
    /// act, and a secondary press changes nothing.</summary>
    [Fact]
    public async Task Pointer_WhenPressIsOnPaddingOrSecondary_DoesNotScrollAsync()
    {
        // Arrange
        var bar = NewBar(Orientation.Horizontal, ScrollBarChrome.Full);
        bar.Padding = new Thickness(2, 0, 0, 0);
        bar.Width = Length.Cells(14);
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("  <....##....>");

        // Act and assert a secondary press
        await surface.Pointer.RightClickAsync(bar, new Point(7, 0));
        bar.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        bar.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);

        // Act and assert padding presses: the generic press-to-focus rule still focuses the bar,
        // but the rail itself ignores cells outside its content box
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        bar.Value.ShouldBe(50);
        changes.ShouldBeEmpty();
        surface.ShouldHaveFocus(bar);
        surface.ShouldHaveCapture(null);

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(2, 0));

        // Assert
        bar.Value.ShouldBe(48);
        surface.ShouldHaveFocus(bar);
    }

    /// <summary>Verifies a rail whose Minimum is above zero maps Home, End, and a thumb drag inside its
    /// own range, and rejects endpoint changes that would exclude the current value.</summary>
    [Fact]
    public async Task Range_WhenMinimumIsPositive_MapsKeysAndDragsInsideRangeAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = BlockGlyphs(),
            // Each endpoint setter validates against the live Value, so the value moves first.
            Value = 10,
            Minimum = 10,
            Maximum = 110,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 20,
            Width = Length.Cells(12),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("<##........>");

        // Act and assert keys
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.End);
        bar.Value.ShouldBe(110);
        await surface.Keyboard.PressAsync(Code.Home);
        bar.Value.ShouldBe(10);
        await surface.Keyboard.PressAsync(Code.Left);
        bar.Value.ShouldBe(10);

        // Act and assert drag
        await surface.Pointer.DragAsync(bar, new Point(1, 0), new Point(9, 0));
        bar.Value.ShouldBe(110);
        surface.ShouldRender("<........##>");
        changes.Select(change => change.Value).ShouldBe([110, 10, 110]);
        changes.Select(change => change.Cause).ShouldBe([ScrollCause.Keyboard, ScrollCause.Keyboard, ScrollCause.Pointer]);
    }

    /// <summary>Verifies a track that shrinks to nothing while a thumb drag is in flight neither
    /// snaps the value to Minimum nor loses the drag, and the value survives the release.</summary>
    [Fact]
    public async Task Pointer_WhenTrackShrinksToNothingDuringDrag_KeepsValueAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = BlockGlyphs(),
            Maximum = 100,
            Value = 50,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        var changes = Record(bar);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(bar, new Point(7, 0));
        bar.Value.ShouldBe(75);

        // Act
        await surface.ResizeAsync(new Size(2, 1));
        surface.ShouldRender("<>");
        await surface.Pointer.MovePressedToAsync(new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(0, 0));

        // Assert
        bar.Value.ShouldBe(75);
        surface.ShouldHaveCapture(bar);
        await surface.Pointer.ReleaseAsync();
        bar.Value.ShouldBe(75);
        changes.Select(change => change.Value).ShouldBe([75]);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a Container's generated vertical rail forwards its user-driven button clicks
    /// into the host's vertical offset with the pointer cause.</summary>
    [Fact]
    public async Task Pointer_WhenGeneratedVerticalRailButtonIsClicked_ScrollsTheHostAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = BlockGlyphs(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var value in new[] { "A", "B", "C", "D", "E", "F", "G", "H" })
        {
            stack.Children.Add(new ControlText(value)
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }

        List<ScrollCause> causes = [];
        stack.ScrollChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 4),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             A  ^
                             B  #
                             C  .
                             D  v
                             """);

        // Act
        await surface.Pointer.MoveToAsync(new Point(3, 3));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        stack.VerticalOffset.ShouldBe(stack.LineSize);
        stack.LineSize.ShouldBeGreaterThan(0);
        causes.ShouldBe([ScrollCause.Pointer]);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("B");

        // Act
        await surface.Pointer.MoveToAsync(new Point(3, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        stack.VerticalOffset.ShouldBe(0);
        causes.ShouldBe([ScrollCause.Pointer, ScrollCause.Pointer]);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
    }

    private static ScrollBar NewBar(Orientation orientation, ScrollBarChrome chrome) => new()
    {
        Orientation = orientation,
        Style = BlockGlyphs() with { Chrome = chrome },
        Maximum = 100,
        Value = 50,
        ViewportSize = 20,
        SmallChange = 2,
        LargeChange = 20,
        Width = Length.Cells(orientation == Orientation.Horizontal ? 12 : 1),
        Height = Length.Cells(orientation == Orientation.Horizontal ? 1 : 12),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };

    private static Point Cell(Orientation orientation, int position) =>
        orientation == Orientation.Horizontal ? new Point(position, 0) : new Point(0, position);

    private static ScrollBarStyle BlockGlyphs()
    {
        var glyphs = ScrollBarStyle.FullBlock.Glyphs;

        return ScrollBarStyle.FullBlock with
        {
            Glyphs = new ScrollBarGlyphs(
                new Rune('^'),
                new Rune('v'),
                new Rune('<'),
                new Rune('>'),
                new Rune('.'),
                new Rune('#'),
                glyphs.HorizontalLineTrack,
                glyphs.HorizontalLineThumb,
                glyphs.VerticalLineTrack,
                glyphs.VerticalLineThumb)
        };
    }

    private static List<(int Previous, int Value, ScrollCause Cause)> Record(ScrollBar bar)
    {
        List<(int Previous, int Value, ScrollCause Cause)> changes = [];
        bar.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value, eventArgs.Cause));
        return changes;
    }
}
