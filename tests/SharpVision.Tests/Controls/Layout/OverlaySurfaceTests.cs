// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Overlay sizing, z-order, removal damage, resize, and hit targets through mounted surfaces.</summary>
public sealed class OverlaySurfaceTests
{
    /// <summary>Verifies z-order changes visual and hit priority and removal clears the winning layer.</summary>
    [ComponentBehaviorEvidence(
        typeof(Overlay),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Pointer_WhenZOrderChangesAndWinnerIsRemoved_UsesCurrentTopLayerAsync()
    {
        // Arrange
        var clicked = string.Empty;
        var low = new Button
        {
            Text = "LLLLL\nLLLLL",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        low.Click += (_, _) => clicked = "low";
        var high = new Button
        {
            Text = "HH",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        high.Click += (_, _) => clicked = "high";
        Overlay.SetZIndex(low, -1);
        Overlay.SetZIndex(high, 10);
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { low, high }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(5, 2),
            TestContext.Current.CancellationToken);

        // Act and assert initial winner
        await surface.Pointer.ClickAsync(high);
        clicked.ShouldBe("high");
        surface.ShouldRender("""
                             HHLLL
                             LLLLL
                             """);

        // Act and assert reordered winner
        await surface.UpdateAsync(() => Overlay.SetZIndex(low, 20), "raise lower Overlay child");
        await surface.Pointer.ClickAsync(high);
        clicked.ShouldBe("low");
        surface.ShouldRender("""
                             LLLLL
                             LLLLL
                             """);

        // Act and assert removal damage
        await surface.UpdateAsync(
            () => overlay.Children.Remove(low).ShouldBeTrue(),
            "remove top Overlay child");
        await surface.Pointer.ClickAsync(high);
        clicked.ShouldBe("high");
        overlay.IsPointerOver.ShouldBeTrue();
        overlay.IsPointerDirectlyOver.ShouldBeFalse();
        overlay.IsFocused.ShouldBeFalse();
        overlay.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(high, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("HH");
    }

    /// <summary>Verifies percent sizing and trailing alignment recompute against the resized shared slot.</summary>
    [Fact]
    public async Task ResizeAsync_WhenChildUsesPercentAndAlignment_RepositionsExactCellsAsync()
    {
        // Arrange
        var child = new ControlText("XXXXX")
        {
            Width = Length.Percent(50),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Overflow = Overflow.Clip
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        child.Bounds.ShouldBe(new Rect(5, 2, 5, 1));
        surface.ShouldRender("""


                                  XXXXX
                             """);

        // Act
        await surface.ResizeAsync(new Size(6, 2));

        // Assert
        child.Bounds.ShouldBe(new Rect(3, 1, 3, 1));
        surface.ShouldRender("""

                                XXX
                             """);
    }

    /// <summary>Verifies percentage offsets reposition a clickable child against each final surface size.</summary>
    [Fact]
    public async Task ResizeAsync_WhenOffsetsArePercent_RepositionsCellsAndHitTargetAsync()
    {
        // Arrange
        var clicked = 0;
        var child = new Button
        {
            Text = "界",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        child.Click += (_, _) => clicked++;
        Overlay.SetLeft(child, Length.Percent(25));
        Overlay.SetTop(child, Length.Percent(50));
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        // Act and assert initial geometry
        await surface.Pointer.ClickAsync(child);
        clicked.ShouldBe(1);
        child.Bounds.ShouldBe(new Rect(2, 2, 2, 1));
        surface.Cell(new Point(2, 2)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 2)).IsContinuation.ShouldBeTrue();

        // Act resize and hit
        await surface.ResizeAsync(new Size(12, 6));
        await surface.Pointer.ClickAsync(child);

        // Assert resized geometry
        clicked.ShouldBe(2);
        child.Bounds.ShouldBe(new Rect(3, 3, 2, 1));
        overlay.IsPointerOver.ShouldBeTrue();
        overlay.IsPointerDirectlyOver.ShouldBeFalse();
        overlay.IsFocused.ShouldBeFalse();
        overlay.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(child, VisualState.PointerOver | VisualState.Focused);
        surface.Cell(new Point(3, 3)).Text.ShouldBe("界");
        surface.Cell(new Point(4, 3)).IsContinuation.ShouldBeTrue();
        surface.Cell(new Point(2, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(3, 2)).IsContinuation.ShouldBeFalse();
    }

    /// <summary>Verifies oversized trailing placement keeps a negative origin and clips exact visible cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenTrailingChildIsOversized_ClipsThenRevealsItsContentAsync()
    {
        // Arrange
        var child = new ControlText("ABCDEFGH")
        {
            Width = Length.Cells(8),
            Height = Length.Cells(1),
            Overflow = Overflow.Clip
        };
        Overlay.SetRight(child, Length.Cells(1));
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert clipped negative placement
        child.Bounds.ShouldBe(new Rect(-4, 0, 8, 1));
        surface.ShouldRender("EFGH");

        // Act
        await surface.ResizeAsync(new Size(10, 1));

        // Assert revealed placement
        child.Bounds.ShouldBe(new Rect(1, 0, 8, 1));
        surface.ShouldRender(" ABCDEFGH");
    }
}
