// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a mounted scrolling Stack: wheel and keyboard scrolling with their typed
/// causes, keyboard focus revealing a descendant below the fold, every scrollbar policy
/// combination's rail reservation, scrollbar presses, re-clamping on resize, and fixed-width
/// children that exceed the host.</summary>
public sealed class StackScrollingInteractionTests
{
    private static Stack CreateRows(int count, ScrollBars bars = ScrollBars.Vertical, ShowScrollBars show = ShowScrollBars.Never)
    {
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = bars,
            ShowScrollBars = show,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        for (var index = 0; index < count; index++)
        {
            stack.Children.Add(new ControlText($"R{index}"));
        }

        return stack;
    }

    /// <summary>Verifies wheel ticks move the offset by LineSize in the wheel's direction, report
    /// the Wheel cause with previous and committed offsets, and do nothing at an endpoint.</summary>
    [Fact]
    public async Task Wheel_WhenScrolledDownThenUp_MovesByLineSizeAndReportsWheelCauseAsync()
    {
        // Arrange
        var stack = CreateRows(10);
        stack.LineSize = 2;
        var changes = new List<ScrollChangedEventArgs>();
        stack.ScrollChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act wheel up at the top does nothing
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: 1);
        stack.VerticalOffset.ShouldBe(0);
        changes.ShouldBeEmpty();

        // Act wheel down
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert
        stack.VerticalOffset.ShouldBe(2);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("R");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("2");
        var change = changes.ShouldHaveSingleItem();
        change.Cause.ShouldBe(ScrollCause.Wheel);
        change.PreviousOffset.ShouldBe(new Point(0, 0));
        change.Offset.ShouldBe(new Point(0, 2));
        change.Viewport.ShouldBe(new Size(4, 3));
        change.Extent.ShouldBe(new Size(2, 10));

        // Act wheel back up
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: 1);

        // Assert
        stack.VerticalOffset.ShouldBe(0);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("0");
    }

    /// <summary>Verifies a focusable armed stack drives Down, PageDown, End, Home, and Up through
    /// the keyboard cause, with Up at the top leaving the offset untouched.</summary>
    [Fact]
    public async Task Keyboard_WhenStackIsFocused_ArrowsPageAndEndpointKeysScrollWithKeyboardCauseAsync()
    {
        // Arrange
        var stack = CreateRows(10);
        stack.IsFocusable = true;
        var causes = new List<ScrollCause>();
        stack.ScrollChanged += (_, args) => causes.Add(args.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(stack);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        stack.VerticalOffset.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.PageDown);
        stack.VerticalOffset.ShouldBe(4);
        await surface.Keyboard.PressAsync(Code.End);
        stack.VerticalOffset.ShouldBe(7);
        surface.Cell(new Point(1, 2)).Text.ShouldBe("9");
        await surface.Keyboard.PressAsync(Code.PageUp);
        stack.VerticalOffset.ShouldBe(4);
        await surface.Keyboard.PressAsync(Code.Home);
        stack.VerticalOffset.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.Up);
        stack.VerticalOffset.ShouldBe(0);
        causes.ShouldAllBe(cause => cause == ScrollCause.Keyboard);
        causes.Count.ShouldBe(5);
    }

    /// <summary>Verifies tabbing to a descendant below the fold reveals it with the BringIntoView
    /// cause and the focused control ends up inside the viewport.</summary>
    [Fact]
    public async Task Focus_WhenTabReachesADescendantBelowTheFold_RevealsItWithBringIntoViewCauseAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var boxes = Enumerable.Range(0, 6).Select(index => new CheckBox { Text = $"C{index}" }).ToArray();

        foreach (var box in boxes)
        {
            stack.Children.Add(box);
        }

        var causes = new List<ScrollCause>();
        stack.ScrollChanged += (_, args) => causes.Add(args.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        for (var press = 0; press < 5; press++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
        }

        // Assert
        surface.ShouldHaveFocus(boxes[4]);
        stack.VerticalOffset.ShouldBe(2);
        boxes[4].Bounds.Y.ShouldBeInRange(0, 2);
        causes.ShouldNotBeEmpty();
        causes.ShouldAllBe(cause => cause == ScrollCause.BringIntoView);
        surface.Cell(new Point(0, 0)).Text.ShouldNotBe(" ");

        // Act Shift+Tab back to the top
        for (var press = 0; press < 4; press++)
        {
            await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        }

        // Assert
        surface.ShouldHaveFocus(boxes[0]);
        stack.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies the vertical rail reservation across every ShowScrollBars policy with
    /// and without overflow: the rail takes the last column exactly when policy and range agree.</summary>
    [Theory]
    [InlineData(ShowScrollBars.Never, 10, false)]
    [InlineData(ShowScrollBars.WhenNeeded, 2, false)]
    [InlineData(ShowScrollBars.WhenNeeded, 10, true)]
    [InlineData(ShowScrollBars.Always, 2, true)]
    [InlineData(ShowScrollBars.Always, 10, true)]
    public async Task ScrollBars_WhenPolicyAndRangeCombine_ReserveTheRailExactlyWhenRequiredAsync(
        ShowScrollBars show,
        int rows,
        bool expectRail)
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = show,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        for (var index = 0; index < rows; index++)
        {
            stack.Children.Add(new ControlText("0123456789"));
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        if (expectRail)
        {
            surface.Cell(new Point(9, 0)).Text.ShouldNotBe("9");
            stack.Viewport.Width.ShouldBe(9);
        }
        else
        {
            surface.Cell(new Point(9, 0)).Text.ShouldBe("9");
            stack.Viewport.Width.ShouldBe(10);
        }

        // Act wheel down regardless of the rail
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert the range, not the rail, decides whether scrolling happens
        stack.VerticalOffset.ShouldBe(rows > 3 ? 1 : 0);
    }

    /// <summary>Verifies the per-axis visibility override: Hidden suppresses an overflowing rail
    /// while keeping the range scrollable, and Always reserves one with no overflow.</summary>
    [Theory]
    [InlineData(ScrollBarVisibility.Hidden, 10, false, 1)]
    [InlineData(ScrollBarVisibility.Always, 2, true, 0)]
    public async Task VerticalBarVisibility_WhenOverridden_ControlsTheRailIndependentlyOfRangeAsync(
        ScrollBarVisibility visibility,
        int rows,
        bool expectRail,
        int expectedOffsetAfterWheel)
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            VerticalBarVisibility = visibility,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        for (var index = 0; index < rows; index++)
        {
            stack.Children.Add(new ControlText("0123456789"));
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        (surface.Cell(new Point(9, 0)).Text != "9").ShouldBe(expectRail);
        stack.Viewport.Width.ShouldBe(expectRail ? 9 : 10);

        // Act
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert
        stack.VerticalOffset.ShouldBe(expectedOffsetAfterWheel);
    }

    /// <summary>Verifies ScrollBars.None disarms both axes entirely: no rail, no wheel movement,
    /// and content clipped at the viewport.</summary>
    [Fact]
    public async Task ScrollBars_WhenNone_ClipsWithoutAnyScrollingAsync()
    {
        // Arrange
        var stack = CreateRows(10, ScrollBars.None, ShowScrollBars.Always);
        var changes = 0;
        stack.ScrollChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert
        stack.VerticalOffset.ShouldBe(0);
        stack.Extent.ShouldBe(new Size(2, 10));
        stack.Viewport.ShouldBe(new Size(4, 3));
        changes.ShouldBe(0);
        surface.ShouldRender("R0  \nR1  \nR2  ");
    }

    /// <summary>Verifies a primary press on the vertical rail moves the offset through the
    /// scrollbar with the Pointer cause and repaints the content.</summary>
    [Fact]
    public async Task Pointer_WhenVerticalRailIsPressed_ScrollsWithPointerCauseAsync()
    {
        // Arrange
        var stack = CreateRows(20, ScrollBars.Vertical, ShowScrollBars.WhenNeeded);
        var changes = new List<ScrollChangedEventArgs>();
        stack.ScrollChanged += (_, args) => changes.Add(args);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 4),
            TestContext.Current.CancellationToken);
        stack.Viewport.Width.ShouldBe(3);

        // Act press the bottom cell of the rail
        await surface.Pointer.ClickAsync(stack, new Point(3, 3));

        // Assert
        stack.VerticalOffset.ShouldBeGreaterThan(0);
        var change = changes.ShouldHaveSingleItem();
        change.Cause.ShouldBe(ScrollCause.Pointer);
        change.PreviousOffset.ShouldBe(new Point(0, 0));
        surface.Cell(new Point(1, 0)).Text.ShouldBe(stack.VerticalOffset.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Verifies growing the host while scrolled to the end re-clamps the offset with the
    /// Resize cause and reveals the first rows again.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostGrowsWhileScrolledToTheEnd_ReclampsWithResizeCauseAsync()
    {
        // Arrange
        var stack = CreateRows(10);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => stack.ScrollBy(0, 100), "scroll to the end");
        stack.VerticalOffset.ShouldBe(7);
        var causes = new List<ScrollCause>();
        stack.ScrollChanged += (_, args) => causes.Add(args.Cause);

        // Act
        await surface.ResizeAsync(new Size(4, 8));

        // Assert
        stack.VerticalOffset.ShouldBe(2);
        causes.ShouldBe([ScrollCause.Resize]);
        surface.Cell(new Point(1, 0)).Text.ShouldBe("2");
        surface.Cell(new Point(1, 7)).Text.ShouldBe("9");

        // Act grow past the extent
        await surface.ResizeAsync(new Size(4, 12));

        // Assert
        stack.VerticalOffset.ShouldBe(0);
        causes.ShouldBe([ScrollCause.Resize, ScrollCause.Resize]);
    }

    /// <summary>Verifies a child whose fixed width exceeds the stack is clamped to the host
    /// without any horizontal affordance, and scrolls with a horizontal rail once armed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Layout_WhenAFixedChildWidthExceedsTheStack_ClampsOrScrollsAsync(bool autoScroll)
    {
        // Arrange
        var wide = new ControlText("ABCDEFGHIJKLMNOPQRST") { Width = Length.Cells(20) };
        var stack = new Stack
        {
            AutoScroll = autoScroll,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { wide, new ControlText("x") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("x");

        if (autoScroll)
        {
            wide.Bounds.Width.ShouldBe(20);
            stack.Extent.Width.ShouldBe(20);
            surface.Cell(new Point(0, 2)).Text.ShouldNotBe(" ");
            await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelX: 1);
            stack.HorizontalOffset.ShouldBe(1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("B");
        }
        else
        {
            wide.Bounds.Width.ShouldBe(8);
            surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");
            await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelX: 1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        }
    }
}
