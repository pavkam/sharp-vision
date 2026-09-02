// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted Grid behavior when track definitions mutate after layout, when
/// tracks are zero-sized or exceed the host, when tracks carry limits, and across extreme host
/// resizes, through rendered cells and hit targets.</summary>
public sealed class GridInteractionTests
{
    /// <summary>Verifies inserting, replacing, and removing column definitions after layout
    /// reflows the children - which keep their column indices - both visually and for hit
    /// testing.</summary>
    [Fact]
    public async Task Columns_WhenMutatedAfterLayout_ReflowsRenderedCellsAndHitTargetsAsync()
    {
        // Arrange
        var first = new ControlText("A");
        var second = new ControlText("B");
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Columns.Add(Track.Cells(3));
        grid.Columns.Add(Track.Star(1));
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("A  B      ");

        // Act insert a leading column: the children keep their indices, so both land in new tracks
        await surface.UpdateAsync(() => grid.Columns.Insert(0, Track.Cells(5)), "insert a leading column");

        // Assert
        surface.ShouldRender("A    B    ");
        await surface.Pointer.MoveToAsync(grid, new Point(5, 0));
        surface.ShouldHaveState(second, VisualState.IsPointerOver);

        // Act replace the leading column
        await surface.UpdateAsync(() => grid.Columns[0] = Track.Cells(2), "narrow the leading column");
        surface.ShouldRender("A B       ");

        // Act remove the leading column
        await surface.UpdateAsync(() => grid.Columns.RemoveAt(0), "remove the leading column");

        // Assert
        surface.ShouldRender("A  B      ");
        await surface.Pointer.MoveToAsync(grid, new Point(0, 0));
        surface.ShouldHaveState(first, VisualState.IsPointerOver);
        await surface.Pointer.MoveToAsync(grid, new Point(3, 0));
        surface.ShouldHaveState(second, VisualState.IsPointerOver);
    }

    /// <summary>Verifies mutating row definitions after layout moves children vertically.</summary>
    [Fact]
    public async Task Rows_WhenMutatedAfterLayout_ReflowsRenderedRowsAsync()
    {
        // Arrange
        var top = new ControlText("T");
        var bottom = new ControlText("B");
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Rows.Add(Track.Cells(1));
        grid.Rows.Add(Track.Cells(1));
        Grid.SetRow(bottom, 1);
        grid.Children.Add(top);
        grid.Children.Add(bottom);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(2, 4),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("T \nB \n  \n  ");

        // Act
        await surface.UpdateAsync(() => grid.Rows[0] = Track.Cells(3), "grow the first row");

        // Assert
        surface.ShouldRender("T \n  \n  \nB ");
        bottom.Bounds.Y.ShouldBe(3);

        // Act removing the row the second child occupies is refused before mutation
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(() => grid.Rows.RemoveAt(1)),
            "refuse removing an occupied row");
        grid.Rows.Count.ShouldBe(2);

        // Act shrink back through a leading insert and removal that keep the count valid
        await surface.UpdateAsync(
            () =>
            {
                grid.Rows.Insert(0, Track.Cells(1));
                grid.Rows.RemoveAt(1);
            },
            "restore the first row");

        // Assert
        surface.ShouldRender("T \nB \n  \n  ");
    }

    /// <summary>Verifies a zero-cell track collapses its child to empty bounds that render nothing
    /// and are never a hit target, while the neighboring track starts at the same edge.</summary>
    [Fact]
    public async Task Layout_WhenATrackIsZeroCells_CollapsesItsChildAndKeepsTheNeighborAtTheEdgeAsync()
    {
        // Arrange
        var hidden = new ControlText("H");
        var shown = new ControlText("S");
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Columns.Add(Track.Cells(0));
        grid.Columns.Add(Track.Cells(3));
        Grid.SetColumn(shown, 1);
        grid.Children.Add(hidden);
        grid.Children.Add(shown);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("S   ");
        hidden.Bounds.Width.ShouldBe(0);
        shown.Bounds.X.ShouldBe(0);
        await surface.Pointer.MoveToAsync(grid, new Point(0, 0));
        surface.ShouldHaveState(shown, VisualState.IsPointerOver);
        surface.ShouldHaveState(hidden, VisualState.Normal);
    }

    /// <summary>Verifies a fixed column wider than the host is clipped at the host edge without
    /// any scroll affordance, and gains a horizontal scrollbar only once the grid scrolls.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Layout_WhenAFixedTrackExceedsTheHost_ClipsOrScrollsAsync(bool autoScroll)
    {
        // Arrange
        var text = new ControlText("ABCDEFGHIJKLMNOPQRST");
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = autoScroll,
            ScrollBars = ScrollBars.Both
        };
        grid.Columns.Add(Track.Cells(20));
        grid.Children.Add(text);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("H");
        grid.Bounds.Width.ShouldBe(8);

        if (autoScroll)
        {
            grid.Extent.Width.ShouldBe(20);
            surface.Cell(new Point(0, 1)).Text.ShouldNotBe(" ");
            await surface.Pointer.WheelAsync(grid, new Point(0, 0), wheelX: 1);
            grid.HorizontalOffset.ShouldBe(1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("B");
        }
        else
        {
            grid.Extent.ShouldBe(grid.Viewport);
            grid.Viewport.ShouldBe(new Size(8, 2));
            surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
            await surface.Pointer.WheelAsync(grid, new Point(0, 0), wheelX: 1);
            surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        }
    }

    /// <summary>Verifies star tracks honor an absolute maximum and a percent minimum across
    /// resizes, redistributing the remainder to the sibling.</summary>
    [Fact]
    public async Task ResizeAsync_WhenStarTracksCarryLimits_ClampsAndRedistributesAsync()
    {
        // Arrange
        var left = new ControlText("LLLLLLLLLL");
        var right = new ControlText("RRRRRRRRRR");
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Columns.Add(Track.Star(1, maximum: Length.Cells(3)));
        grid.Columns.Add(Track.Star(1, minimum: Length.Percent(60)));
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert: the 60% minimum reserves 6, the remaining 4 split evenly under the cap
        surface.ShouldRender("LLRRRRRRRR");

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert: 60% of 4 rounds to 2, the remaining 2 split evenly
        surface.ShouldRender("LRRR");

        // Act
        await surface.ResizeAsync(new Size(20, 1));

        // Assert: the cap binds at 3 and the sibling absorbs the rest
        left.Bounds.Width.ShouldBe(3);
        right.Bounds.Width.ShouldBe(17);
    }

    /// <summary>Verifies a grid survives a one-cell host and recovers its full layout.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostShrinksToOneCell_KeepsOneCellAndRecoversAsync()
    {
        // Arrange
        var first = new ControlText("A");
        var second = new ControlText("B");
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Columns.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("A B ");

        // Act
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        surface.ShouldRender("A");
        second.Bounds.Width.ShouldBe(0);

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        surface.ShouldRender("A B ");
    }
}
