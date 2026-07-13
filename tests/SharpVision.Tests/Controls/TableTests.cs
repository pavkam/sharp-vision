// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




using ControlText = SharpVision.Controls.Text;
using TerminalStyle = Style;

/// <summary>Verifies table ownership, track geometry, headers, grid cells, and row validation.</summary>
public sealed class TableTests
{
    /// <summary>Verifies fixed, percentage, and fill columns resolve exact contained cell slots.</summary>
    [Fact]
    public void Layout_WhenColumnsMixFixedPercentAndFill_ResolvesContainedCellSlots()
    {
        ControlText first = new("Alpha");
        ControlText second = new("Ready");
        ControlText third = new("Details");
        Table table = new();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Percent("Status", 50));
        table.Columns.Add(TableColumn.Fill("Details"));
        table.Rows.Add(new TableRow([first, second, third]));

        new Engine().Layout(table, new Size(20, 4));

        first.Bounds.ShouldBe(new Rect(0, 2, 5, 1));
        second.Bounds.ShouldBe(new Rect(6, 2, 9, 1));
        third.Bounds.ShouldBe(new Rect(16, 2, 4, 1));
        table.DesiredSize.ShouldBe(new Size(20, 3));
    }

    /// <summary>Verifies headers and light grid lines render around ordinary owned cell controls.</summary>
    [Fact]
    public void Render_WhenHeaderAndGridLinesAreEnabled_WritesHeaderCellsAndIntersections()
    {
        Table table = new();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        Size size = new(14, 4);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("V");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 1)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(6, 2)).ShouldBe("B");
    }

    /// <summary>Verifies table dividers inherit the painted surface background.</summary>
    [Fact]
    public void Render_WhenTableStyleHasForegroundOnly_PreservesSurfaceBackgroundOnDividers()
    {
        ControlStyle<Control> style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        Table table = new() { Style = style };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        Size size = new(14, 4);
        new Engine().Layout(table, size);
        using Frame frame = new(size);
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        table.Render(frame.Canvas);

        frame.GetCell(new Point(5, 0)).Style.Background.ShouldBe(Color.Indexed(238));
        frame.GetCell(new Point(5, 2)).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies table header and grid projections preserve semantic decorations.</summary>
    [Fact]
    public void Render_WhenStyleUsesModernDecorations_PreservesChromeStyle()
    {
        ControlStyle<Table> style = ThemeTestSupport.OverlayStyle<Table>(
            (State.Normal, new ThemeOverlay(
                attributes: Attributes.RapidBlink,
                underline: Underline.Dashed,
                underlineColor: Color.Indexed(5))));
        Table table = new() { Style = style };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(new TableRow([new ControlText("A")]));
        Size size = new(6, 3);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        TerminalStyle header = frame.GetCell(default).Style;
        TerminalStyle grid = frame.GetCell(new Point(0, 1)).Style;
        header.Attributes.ShouldBe(Attributes.RapidBlink);
        header.Underline.ShouldBe(Underline.Dashed);
        header.UnderlineColor.ShouldBe(Color.Indexed(5));
        grid.ShouldBe(header);
    }

    /// <summary>Verifies an offset table keeps its header divider in the table's absolute coordinate space.</summary>
    [Fact]
    public void Render_WhenTableIsOffset_DrawsHeaderDividerBelowItsOwnHeader()
    {
        Table table = new();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        table.Measure(new Constraint(width: 14, height: 4));
        table.Arrange(new Rect(2, 3, 14, 4));
        using Frame frame = new(new Size(20, 10));

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 4)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a header-only table has no phantom row gap or divider beneath its header.</summary>
    [Fact]
    public void Layout_WhenTableHasNoRows_UsesOnlyTheHeaderHeight()
    {
        Table table = new();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fixed("Value", 5));
        Size size = new(12, 4);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        table.DesiredSize.ShouldBe(new Size(11, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a row must match the complete column count before any cells are attached.</summary>
    [Fact]
    public void Rows_WhenCellCountDiffersFromColumns_RejectsRowWithoutOwnershipTransfer()
    {
        Table table = new();
        table.Columns.Add(TableColumn.Auto("One"));
        table.Columns.Add(TableColumn.Auto("Two"));
        ControlText cell = new("Only one");
        TableRow row = new([cell]);

        _ = Should.Throw<ArgumentException>(() => table.Rows.Add(row));

        table.Rows.Count.ShouldBe(0);
        table.Children.Count.ShouldBe(0);
        cell.Parent.ShouldBeNull();
    }
}
