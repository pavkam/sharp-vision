// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Stack allocation, ordering, scrolling, resize, and hit targets through mounted surfaces.</summary>
public sealed class StackSurfaceTests
{
    /// <summary>Verifies pointer ancestry does not apply hover chrome to the layout container.</summary>
    [Fact]
    public async Task Pointer_WhenChildIsHovered_KeepsStackFrameInactiveAsync()
    {
        // Arrange
        var child = new Button
        {
            Text = "Child",
            Style = TestButtonStyles.Flat,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var stack = new Stack
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        var borderColor = surface.Cell(default).Style.Foreground;
        borderColor.IsRgb.ShouldBeTrue();

        // Act
        await surface.Pointer.MoveToAsync(child);

        // Assert
        stack.IsPointerOver.ShouldBeTrue();
        child.IsPointerOver.ShouldBeTrue();
        // The stack border now tracks PointerOver through the theme.
        surface.Cell(default).Style.Foreground.IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies fixed, percentage, and star tracks reflow to exact final cells after resize.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHorizontalTracksAreMixed_ReallocatesBoundsAndCellsExactlyAsync()
    {
        // Arrange
        var fixedChild = new ControlText("AAA")
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var percentChild = new ControlText("BBBBB")
        {
            Width = Length.Percent(25),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var starChild = new ControlText("CCCCCCCC")
        {
            Width = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { fixedChild, percentChild, starChild }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Assert initial layout
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 5, 1));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 1));
        surface.ShouldRender("AAA  BBBBB  CCCCCCCC");

        // Act
        await surface.ResizeAsync(new Size(12, 1));

        // Assert resized layout
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 3, 1));
        starChild.Bounds.ShouldBe(new Rect(10, 0, 2, 1));
        surface.ShouldRender("AAA  BBB  CC");
    }

    /// <summary>Verifies reverse order excludes collapsed children and routes clicks to final arranged rows.</summary>
    [ComponentBehaviorEvidence(
        typeof(Stack),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Pointer_WhenStackIsReversedAndChildCollapsed_UsesVisibleVisualOrderAsync()
    {
        // Arrange
        var activated = string.Empty;
        var first = new Button
        {
            Text = "A",
            Style = TestButtonStyles.Flat,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        first.Click += (_, _) => activated = "A";
        var collapsed = new Button { Text = "X", Visibility = Visibility.Collapsed };
        var second = new Button
        {
            Text = "B",
            Style = TestButtonStyles.Flat,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        second.Click += (_, _) => activated = "B";
        var stack = new Stack
        {
            Reverse = true,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { first, collapsed, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(second);

        // Assert
        second.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        collapsed.Bounds.ShouldBe(default);
        first.Bounds.ShouldBe(new Rect(0, 2, 4, 1));
        activated.ShouldBe("B");
        stack.IsPointerOver.ShouldBeTrue();
        stack.IsPointerDirectlyOver.ShouldBeFalse();
        stack.IsFocused.ShouldBeFalse();
        stack.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
                              B

                              A
                             """);
    }

    /// <summary>Verifies intrinsic vertical scrolling clips rows and resize clamps the retained offset.</summary>
    [Fact]
    public async Task ResizeAsync_WhenVerticalStackAutoScrolls_ClipsAndRepairsVisibleRowsAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var value in new[] { "A", "B", "界", "D", "E" })
        {
            stack.Children.Add(new ControlText(value)
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             A
                             B
                             """);

        // Act scroll
        await surface.Pointer.WheelAsync(stack, default, wheelY: -1);

        // Assert scroll
        stack.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             B
                             界
                             """);
        surface.Cell(new Point(1, 1)).IsContinuation.ShouldBeTrue();

        // Act resize beyond content
        await surface.ResizeAsync(new Size(4, 6));

        // Assert resize repair
        stack.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             A
                             B
                             界
                             D
                             E

                             """);
    }

    /// <summary>Verifies vertical mixed tracks (fixed, percent, star) allocate exact heights.</summary>
    [Fact]
    public async Task Render_WhenVerticalTracksMixed_AllocatesExactHeightsAsync()
    {
        // Arrange
        var fixedChild = new ControlText("F")
        {
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        var percentChild = new ControlText("P")
        {
            Height = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
        };
        var starChild = new ControlText("S")
        {
            Height = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(3)),
        };
        var stack = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { fixedChild, percentChild, starChild }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 10),
            TestContext.Current.CancellationToken);

        // Assert — fixed=2, percent=50% of the complete 10-cell axis=5, star=remainder=3
        // (see #274: percentages resolve against the complete final axis, not the smaller
        // remainder left after reserving the fixed track).
        fixedChild.Bounds.Height.ShouldBe(2);
        percentChild.Bounds.Y.ShouldBe(2);
        percentChild.Bounds.Height.ShouldBe(5);
        starChild.Bounds.Y.ShouldBeGreaterThanOrEqualTo(percentChild.Bounds.Bottom);
        starChild.Bounds.Height.ShouldBe(3);
        (fixedChild.Bounds.Height + percentChild.Bounds.Height + starChild.Bounds.Height).ShouldBe(10);
    }

    /// <summary>Verifies min/max constraints clamp child sizes in a horizontal Stack.</summary>
    [Fact]
    public async Task Render_WhenChildrenHaveMinMaxConstraints_ClampsAllocatedSizesAsync()
    {
        // Arrange
        var small = new ControlText("S")
        {
            Width = Length.Star(1),
            MinWidth = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        var large = new ControlText("L")
        {
            Width = Length.Star(3),
            MaxWidth = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { small, large }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 1),
            TestContext.Current.CancellationToken);

        // Assert — Star(1) would get 4 cells, Star(3) would get 12, but max=8 clamps it
        small.Bounds.Width.ShouldBeGreaterThanOrEqualTo(4);
        large.Bounds.Width.ShouldBeLessThanOrEqualTo(8);
    }

    /// <summary>Verifies nested Stacks compose correctly with independent orientation and spacing.</summary>
    [Fact]
    public async Task Render_WhenStacksAreNested_ComposesIndependentlyAsync()
    {
        // Arrange
        var innerA = new ControlText("A")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Width = Length.Cells(3)
        };
        var innerB = new ControlText("B")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Width = Length.Cells(3)
        };
        var innerStack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { innerA, innerB }
        };
        var topLabel = new ControlText("TOP")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var outerStack = new Stack
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { topLabel, innerStack }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            outerStack,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Assert — vertical outer: row 0 = "TOP", row 1 = spacing, row 2 = inner horizontal
        surface.Cell(default).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("B");
    }
}
