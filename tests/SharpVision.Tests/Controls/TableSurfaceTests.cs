// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Table columns, rows, Unicode, scrolling, mutation, resize, and hit targets through mounted surfaces.</summary>
public sealed class TableSurfaceTests
{
    /// <summary>Verifies every column kind aligns headers, grid lines, combining text, and wide cells exactly.</summary>
    [Fact]
    public async Task Render_WhenColumnsMixKinds_DrawsExactHeaderGridAndUnicodeCellsAsync()
    {
        // Arrange
        var name = new ControlText("A\u0301界");
        var state = new ControlText("Ready");
        var wide = new ControlText("界");
        var details = new ControlText("Tail");
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Percent("State", 25));
        table.Columns.Add(TableColumn.Auto("Wide"));
        table.Columns.Add(TableColumn.Fill("Details"));
        table.Rows.Add(new TableRow([name, state, wide, details]));
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(24, 4),
            TestContext.Current.CancellationToken);

        // Assert
        name.Bounds.ShouldBe(new Rect(0, 2, 3, 1));
        state.Bounds.ShouldBe(new Rect(6, 2, 5, 1));
        wide.Bounds.ShouldBe(new Rect(12, 2, 2, 1));
        details.Bounds.ShouldBe(new Rect(17, 2, 4, 1));
        surface.ShouldRender("""
            Name │State│Wide│Details
            ─────┼─────┼────┼───────
            Á界  │Ready│界  │Tail
                 │     │    │
            """);
        surface.Cell(default).Text.ShouldBe("N");
        surface.Cell(new Point(1, 2)).Text.ShouldBe("界");
        surface.Cell(new Point(2, 2)).IsContinuation.ShouldBeTrue();
        surface.Cell(new Point(13, 2)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies row removal and reinsertion reflows clickable cells and clears abandoned rows.</summary>
    [Fact]
    public async Task UpdateAsync_WhenClickableRowIsRemovedAndReused_ReflowsWithoutStaleCellsAsync()
    {
        // Arrange
        var clicked = string.Empty;
        var one = Row("One");
        var two = Row("Two");
        var three = Row("Three");
        var oneRow = new TableRow([one]);
        var twoRow = new TableRow([two]);
        var threeRow = new TableRow([three]);
        one.Click += (_, _) => clicked = "One";
        two.Click += (_, _) => clicked = "Two";
        three.Click += (_, _) => clicked = "Three";
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        table.Columns.Add(TableColumn.Fixed("Value", 8));
        table.Rows.Add(oneRow);
        table.Rows.Add(twoRow);
        table.Rows.Add(threeRow);
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
            One
            Two
            Three
            """);

        // Act remove first row and hit moved row
        await surface.UpdateAsync(() => table.Rows.RemoveAt(0), "remove first Table row");
        await surface.Pointer.ClickAsync(two);

        // Assert removal
        one.Parent.ShouldBeNull();
        two.Bounds.ShouldBe(new Rect(0, 0, 8, 1));
        three.Bounds.ShouldBe(new Rect(0, 1, 8, 1));
        clicked.ShouldBe("Two");
        surface.ShouldRender("""
            Two
            Three

            """);

        // Act reuse detached row
        await surface.UpdateAsync(() => table.Rows.Add(oneRow), "reuse detached Table row");
        await surface.Pointer.ClickAsync(one);

        // Assert reuse
        one.Bounds.ShouldBe(new Rect(0, 2, 8, 1));
        clicked.ShouldBe("One");
        surface.ShouldHaveState(one, State.Hovered | State.Focused);
        surface.ShouldRender("""
            Two
            Three
            One
            """);
    }

    /// <summary>Verifies both-axis wheel scrolling stays aligned and resize clamps offsets while revealing all rows.</summary>
    [Fact]
    public async Task ResizeAsync_WhenBothAxesWereWheeled_ClampsOffsetsAndRestoresContentAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ShowGridLines = false,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));

        for (var index = 0; index < 6; index++)
        {
            table.Rows.Add(new TableRow([
                new ControlText($"A{index}"),
                new ControlText($"B{index}"),
            ]));
        }

        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        // Act wheel both axes
        await surface.Pointer.WheelAsync(table, default, wheelY: -1);
        await surface.Pointer.WheelAsync(table, default, wheelX: -1);

        // Assert scrolled cells
        table.HorizontalOffset.ShouldBe(1);
        table.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
            1      B1
            2      B2
            3      B3
            4      B4
            """);

        // Act resize beyond extent
        await surface.ResizeAsync(new Size(20, 8));

        // Assert offset repair and full reveal
        table.HorizontalOffset.ShouldBe(0);
        table.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
            A0      B0
            A1      B1
            A2      B2
            A3      B3
            A4      B4
            A5      B5


            """);
    }

    /// <summary>Creates one stretched borderless clickable table cell.</summary>
    private static Button Row(string value) => new()
    {
        Content = new ControlText(value),
        BorderThickness = default,
        Padding = default,
        Height = Length.Cells(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
}
