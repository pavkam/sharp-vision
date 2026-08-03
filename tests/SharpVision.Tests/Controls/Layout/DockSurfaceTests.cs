// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Dock edge consumption, final fill, resize, cells, and hit targets through mounted surfaces.</summary>
public sealed class DockSurfaceTests
{
    /// <summary>Verifies a simple dock with children on all four sides renders the expected layout.</summary>
    [Fact]
    public async Task Render_WhenChildrenDockAllFourSides_DrawsExpectedLayoutAsync()
    {
        // Arrange
        var top = new ControlText("TTTTTT")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var bottom = new ControlText("BBBBBB")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var left = new ControlText("L\nL\nL") { Width = Length.Cells(1), Overflow = Overflow.Clip };
        var right = new ControlText("R\nR\nR") { Width = Length.Cells(1), Overflow = Overflow.Clip };
        var fill = new ControlText("XXXX\nXXXX\nXXXX")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(bottom, DockSide.Bottom);
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(right, DockSide.Right);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, bottom, left, right, fill }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(6, 5),
            TestContext.Current.CancellationToken);

        // Assert layout with top and bottom consuming full width first
        surface.ShouldRender("""
                             TTTTTT
                             LXXXXR
                             LXXXXR
                             LXXXXR
                             BBBBBB
                             """);
    }

    /// <summary>Verifies disabling LastChildFills renders the last child at its docked side instead of filling.</summary>
    [Fact]
    public async Task Render_WhenLastChildFillsIsDisabled_DocksLastChildToItsSideAsync()
    {
        // Arrange
        var left = new ControlText("LL\nLL\nLL") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var right = new ControlText("RR\nRR\nRR") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(right, DockSide.Right);
        var dock = new Dock
        {
            LastChildFills = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { left, right }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert last child docks to its side with gap in the middle
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 3));
        right.Bounds.ShouldBe(new Rect(6, 0, 2, 3));
        surface.ShouldRender("""
                             LL    RR
                             LL    RR
                             LL    RR
                             """);
    }

    /// <summary>Verifies all four edges consume in order and leave one exact clickable fill rectangle.</summary>
    [ComponentBehaviorEvidence(
        typeof(Dock),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task ResizeAsync_WhenEverySideAndFillArePresent_ReflowsExactRegionsAsync()
    {
        // Arrange
        var clicked = false;
        var left = new ControlText("LL\nLL\nLL\nLL\nLL\nLL") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var top = new ControlText("TTTTTTTT")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var right = new ControlText("RR\nRR\nRR\nRR\nRR") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var bottom = new ControlText("BBBBBB")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var fill = new Button
        {
            Text = "FFFFFF\nFFFFFF\nFFFFFF\nFFFFFF",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        fill.TextControl!.Overflow = Overflow.Clip;
        fill.Click += (_, _) => clicked = true;
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(right, DockSide.Right);
        Dock.SetSide(bottom, DockSide.Bottom);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children =
            {
                left,
                top,
                right,
                bottom,
                fill
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(10, 6),
            TestContext.Current.CancellationToken);

        // Act initial hit
        await surface.Pointer.ClickAsync(fill);

        // Assert initial geometry
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 6));
        top.Bounds.ShouldBe(new Rect(2, 0, 8, 1));
        right.Bounds.ShouldBe(new Rect(8, 1, 2, 5));
        bottom.Bounds.ShouldBe(new Rect(2, 5, 6, 1));
        fill.Bounds.ShouldBe(new Rect(2, 1, 6, 4));
        clicked.ShouldBeTrue();
        dock.IsPointerOver.ShouldBeTrue();
        dock.IsPointerDirectlyOver.ShouldBeFalse();
        dock.IsFocused.ShouldBeFalse();
        dock.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(fill, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             LLTTTTTTTT
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLBBBBBBRR
                             """);

        // Act resize
        await surface.ResizeAsync(new Size(8, 4));

        // Assert resized geometry
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 4));
        top.Bounds.ShouldBe(new Rect(2, 0, 6, 1));
        right.Bounds.ShouldBe(new Rect(6, 1, 2, 3));
        bottom.Bounds.ShouldBe(new Rect(2, 3, 4, 1));
        fill.Bounds.ShouldBe(new Rect(2, 1, 4, 2));
        surface.ShouldRender("""
                             LLTTTTTT
                             LLFFFFRR
                             LLFFFFRR
                             LLBBBBRR
                             """);
    }

    /// <summary>Verifies two children docked to the same side consume space in insertion order.</summary>
    [Fact]
    public async Task Render_WhenTwoChildrenDockToTop_ConsumesInInsertionOrderAsync()
    {
        // Arrange
        var first = new ControlText("111")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(first, DockSide.Top);
        var second = new ControlText("222")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(second, DockSide.Top);
        var fill = new ControlText("FFF")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { first, second, fill }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 4),
            TestContext.Current.CancellationToken);

        // Assert — first top gets row 0, second top gets row 1, fill gets rows 2-3
        first.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, 4, 1));
        fill.Bounds.Y.ShouldBe(2);
        surface.Cell(default).Text.ShouldBe("1");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("2");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("F");
    }

    /// <summary>Verifies collapsed children do not consume dock edge space.</summary>
    [Fact]
    public async Task Render_WhenDockChildIsCollapsed_DoesNotConsumeSpaceAsync()
    {
        // Arrange
        var top = new ControlText("TOP")
        {
            Height = Length.Cells(1),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Dock.SetSide(top, DockSide.Top);
        var fill = new ControlText("FILL")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, fill }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert — collapsed top doesn't consume space, fill starts at row 0
        top.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 4, 3));
        surface.Cell(default).Text.ShouldBe("F");
    }
}
