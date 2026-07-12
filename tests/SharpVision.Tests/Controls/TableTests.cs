using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;
using UiStyle = SharpVision.Styling.Style;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies table ownership, track geometry, headers, grid cells, and row validation.</summary>
public sealed class TableTests
{
    /// <summary>Verifies fixed, percentage, and fill columns resolve exact contained cell slots.</summary>
    [Fact]
    public void Layout_WhenColumnsMixFixedPercentAndFill_ResolvesContainedCellSlots()
    {
        var first = new ControlText("Alpha");
        var second = new ControlText("Ready");
        var third = new ControlText("Details");
        var table = new Table();
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
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var size = new Size(14, 4);
        new Engine().Layout(table, size);
        using var frame = new Frame(size);

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
        var style = new UiStyle();
        style.Set(State.Normal, new Appearance(foreground: Color.Indexed(45)));
        var table = new Table { Style = style };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var size = new Size(14, 4);
        new Engine().Layout(table, size);
        using var frame = new Frame(size);
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        table.Render(frame.Canvas);

        frame.GetCell(new Point(5, 0)).Style.Background.ShouldBe(Color.Indexed(238));
        frame.GetCell(new Point(5, 2)).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies an offset table keeps its header divider in the table's absolute coordinate space.</summary>
    [Fact]
    public void Render_WhenTableIsOffset_DrawsHeaderDividerBelowItsOwnHeader()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        table.Measure(new Constraint(width: 14, height: 4));
        table.Arrange(new Rect(2, 3, 14, 4));
        using var frame = new Frame(new Size(20, 10));

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 4)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a header-only table has no phantom row gap or divider beneath its header.</summary>
    [Fact]
    public void Layout_WhenTableHasNoRows_UsesOnlyTheHeaderHeight()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fixed("Value", 5));
        var size = new Size(12, 4);
        new Engine().Layout(table, size);
        using var frame = new Frame(size);

        table.Render(frame.Canvas);

        table.DesiredSize.ShouldBe(new Size(11, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a row must match the complete column count before any cells are attached.</summary>
    [Fact]
    public void Rows_WhenCellCountDiffersFromColumns_RejectsRowWithoutOwnershipTransfer()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("One"));
        table.Columns.Add(TableColumn.Auto("Two"));
        var cell = new ControlText("Only one");
        var row = new TableRow([cell]);

        _ = Should.Throw<ArgumentException>(() => table.Rows.Add(row));

        table.Rows.Count.ShouldBe(0);
        table.Children.Count.ShouldBe(0);
        cell.Parent.ShouldBeNull();
    }
}
