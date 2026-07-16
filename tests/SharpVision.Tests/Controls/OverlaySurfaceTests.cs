// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Overlay sizing, z-order, removal damage, resize, and hit targets through mounted surfaces.</summary>
public sealed class OverlaySurfaceTests
{
    /// <summary>Verifies z-order changes visual and hit priority and removal clears the winning layer.</summary>
    [Fact]
    public async Task Pointer_WhenZOrderChangesAndWinnerIsRemoved_UsesCurrentTopLayerAsync()
    {
        // Arrange
        var clicked = string.Empty;
        var low = new Button
        {
            Content = new ControlText("LLLLL\nLLLLL"),
            BorderThickness = default,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        low.Click += (_, _) => clicked = "low";
        var high = new Button
        {
            Content = new ControlText("HH"),
            BorderThickness = default,
            Padding = default,
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        high.Click += (_, _) => clicked = "high";
        Overlay.SetZIndex(low, -1);
        Overlay.SetZIndex(high, 10);
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { low, high },
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
        surface.ShouldHaveState(high, State.Hovered | State.Focused);
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
            Overflow = Overflow.Clip,
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child },
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
}
