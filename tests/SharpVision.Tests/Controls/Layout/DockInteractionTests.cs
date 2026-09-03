// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted Dock behavior: edge consumption in insertion order with the last
/// child filling or docking, fixed-width children exceeding the host with and without scrolling,
/// hosts shrunk to one cell, and an empty dock gaining content after layout.</summary>
public sealed class DockInteractionTests
{
    private static ControlText Cell(string text) => new(text);

    /// <summary>Verifies children consume edges in insertion order, the last child fills the
    /// remaining rectangle, and switching LastChildFills off docks it to its own side instead.</summary>
    [Fact]
    public async Task Layout_WhenChildrenDockInOrder_ConsumesEdgesAndFillsOrDocksTheLastChildAsync()
    {
        // Arrange
        var top = Cell("TTTTTTTT");
        var left = Cell("L");
        var right = Cell("R");
        var fill = Cell("FF");
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(right, DockSide.Right);
        Dock.SetSide(fill, DockSide.Left);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, left, right, fill }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert the fill occupies everything between the docked edges
        surface.ShouldRender("TTTTTTTT\nLFF    R\n        ");
        fill.Bounds.ShouldBe(new Rect(1, 1, 6, 2));
        await surface.Pointer.MoveToAsync(dock, new Point(5, 2));
        surface.ShouldHaveState(fill, VisualState.IsPointerOver);

        // Act
        await surface.UpdateAsync(() => dock.LastChildFills = false, "dock the last child to its side");

        // Assert the last child now docks left at its intrinsic width across the remaining height
        surface.ShouldRender("TTTTTTTT\nLFF    R\n        ");
        fill.Bounds.ShouldBe(new Rect(1, 1, 2, 2));
        await surface.Pointer.MoveToAsync(dock, new Point(5, 2));
        surface.ShouldHaveState(fill, VisualState.Normal);
        surface.ShouldHaveState(dock, VisualState.IsPointerOver);
    }

    /// <summary>Verifies a docked child with a fixed width beyond the host is clamped to the host
    /// without any scroll affordance, and scrolls with a horizontal rail once armed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Layout_WhenAFixedChildWidthExceedsTheDock_ClampsOrScrollsAsync(bool autoScroll)
    {
        // Arrange
        var wide = Cell("ABCDEFGHIJKLMNOPQRST");
        wide.Width = Length.Cells(20);
        Dock.SetSide(wide, DockSide.Top);
        var dock = new Dock
        {
            AutoScroll = autoScroll,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { wide, Cell("x") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("x");

        if (autoScroll)
        {
            wide.Bounds.Width.ShouldBe(20);
            dock.Extent.Width.ShouldBe(20);
            surface.Cell(new Point(0, 2)).Text.ShouldNotBe(" ");
            await surface.Pointer.WheelAsync(dock, new Point(0, 0), wheelX: 1);
            dock.HorizontalOffset.ShouldBe(1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("B");
        }
        else
        {
            wide.Bounds.Width.ShouldBe(8);
            dock.Extent.ShouldBe(dock.Viewport);
            surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");
            await surface.Pointer.WheelAsync(dock, new Point(0, 0), wheelX: 1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        }
    }

    /// <summary>Verifies a dock shrunk to one cell gives that cell to the first docked child,
    /// leaves every later child empty without negative bounds, and recovers on growth.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostShrinksToOneCell_KeepsTheFirstEdgeAndRecoversAsync()
    {
        // Arrange
        var top = Cell("T");
        var fill = Cell("F");
        Dock.SetSide(top, DockSide.Top);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, fill }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(3, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("T  \nF  ");

        // Act
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        surface.ShouldRender("T");
        fill.Bounds.Width.ShouldBe(1);
        fill.Bounds.Height.ShouldBe(0);
        fill.Bounds.Y.ShouldBe(1);

        // Act
        await surface.ResizeAsync(new Size(3, 2));

        // Assert
        surface.ShouldRender("T  \nF  ");
    }

    /// <summary>Verifies an empty dock renders nothing and lays out children added after the
    /// first frame, including a collapsed child that consumes no edge until made visible.</summary>
    [Fact]
    public async Task Children_WhenAddedAfterLayout_ArrangeIntoTheEmptyDockAsync()
    {
        // Arrange
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("    \n    ");
        var top = Cell("TTTT");
        var fill = Cell("F");
        Dock.SetSide(top, DockSide.Top);
        top.Visibility = Visibility.Collapsed;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                dock.Children.Add(top);
                dock.Children.Add(fill);
            },
            "add a collapsed top child and a fill child");

        // Assert the collapsed child consumes no edge
        surface.ShouldRender("F   \n    ");

        // Act
        await surface.UpdateAsync(() => top.Visibility = Visibility.Visible, "show the top child");

        // Assert
        surface.ShouldRender("TTTT\nF   ");
    }
}
