// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Dock edge consumption, final fill, resize, cells, and hit targets through mounted surfaces.</summary>
public sealed class DockSurfaceTests
{
    /// <summary>Verifies all four edges consume in order and leave one exact clickable fill rectangle.</summary>
    [Fact]
    public async Task ResizeAsync_WhenEverySideAndFillArePresent_ReflowsExactRegionsAsync()
    {
        // Arrange
        var clicked = false;
        var left = new ControlText("LL\nLL\nLL\nLL\nLL\nLL")
        {
            Width = Length.Cells(2),
            Overflow = Overflow.Clip,
        };
        var top = new ControlText("TTTTTTTT")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip,
        };
        var right = new ControlText("RR\nRR\nRR\nRR\nRR")
        {
            Width = Length.Cells(2),
            Overflow = Overflow.Clip,
        };
        var bottom = new ControlText("BBBBBB")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip,
        };
        var fill = new Button
        {
            Content = new ControlText("FFFFFF\nFFFFFF\nFFFFFF\nFFFFFF")
            {
                Overflow = Overflow.Clip,
            },
            BorderThickness = default,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        fill.Click += (_, _) => clicked = true;
        Dock.SetSide(left, Side.Left);
        Dock.SetSide(top, Side.Top);
        Dock.SetSide(right, Side.Right);
        Dock.SetSide(bottom, Side.Bottom);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { left, top, right, bottom, fill },
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
}
