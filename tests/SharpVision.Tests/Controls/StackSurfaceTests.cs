// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Stack allocation, ordering, scrolling, resize, and hit targets through mounted surfaces.</summary>
public sealed class StackSurfaceTests
{
    /// <summary>Verifies fixed, percentage, and star tracks reflow to exact final cells after resize.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHorizontalTracksAreMixed_ReallocatesBoundsAndCellsExactlyAsync()
    {
        // Arrange
        var fixedChild = new ControlText("AAA")
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip,
        };
        var percentChild = new ControlText("BBBBB")
        {
            Width = Length.Percent(25),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip,
        };
        var starChild = new ControlText("CCCCCCCC")
        {
            Width = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip,
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { fixedChild, percentChild, starChild },
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
            Content = new ControlText("A"),
            BorderThickness = default,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        first.Click += (_, _) => activated = "A";
        var collapsed = new Button
        {
            Content = new ControlText("X"),
            Visibility = Visibility.Collapsed,
        };
        var second = new Button
        {
            Content = new ControlText("B"),
            BorderThickness = default,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        second.Click += (_, _) => activated = "B";
        var stack = new Stack
        {
            Reverse = true,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { first, collapsed, second },
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
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        foreach (var value in new[] { "A", "B", "界", "D", "E" })
        {
            stack.Children.Add(new ControlText(value)
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
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
}
