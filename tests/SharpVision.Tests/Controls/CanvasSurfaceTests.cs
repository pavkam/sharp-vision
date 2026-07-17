// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using ControlCanvas = SharpVision.Controls.Canvas;

/// <summary>Verifies Canvas offsets, clipping, resize, Unicode cells, and hit targets through mounted surfaces.</summary>
public sealed class CanvasSurfaceTests
{
    /// <summary>Verifies percentage offsets reposition a clickable child against each final surface size.</summary>
    [ComponentBehaviorEvidence(
        typeof(ControlCanvas),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task ResizeAsync_WhenOffsetsArePercent_RepositionsCellsAndHitTargetAsync()
    {
        // Arrange
        var clicked = 0;
        var child = new Button
        {
            Content = new ControlText("界"),
            BorderThickness = default,
            Padding = default,
            Width = Length.Cells(2),
            Height = Length.Cells(1),
        };
        child.Click += (_, _) => clicked++;
        ControlCanvas.SetLeft(child, Length.Percent(25));
        ControlCanvas.SetTop(child, Length.Percent(50));
        var canvas = new ControlCanvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child },
        };
        await using var surface = await ComponentSurface.MountAsync(
            canvas,
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
        canvas.IsPointerOver.ShouldBeTrue();
        canvas.IsPointerDirectlyOver.ShouldBeFalse();
        canvas.IsFocused.ShouldBeFalse();
        canvas.IsPressed.ShouldBeFalse();
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
            Overflow = Overflow.Clip,
        };
        ControlCanvas.SetRight(child, Length.Cells(1));
        var canvas = new ControlCanvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child },
        };
        await using var surface = await ComponentSurface.MountAsync(
            canvas,
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
