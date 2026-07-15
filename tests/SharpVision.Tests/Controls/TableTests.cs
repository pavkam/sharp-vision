// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;





/// <summary>Verifies table ownership, track geometry, headers, grid cells, and row validation.</summary>
public sealed class TableTests
{
    /// <summary>Verifies every public row insertion boundary reports its own null parameter.</summary>
    [Fact]
    public void Rows_WhenNullIsInserted_ReportsPublicParameterName()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));

        var add = Should.Throw<ArgumentNullException>(() => table.Rows.Add(null!));
        var insert = Should.Throw<ArgumentNullException>(() => table.Rows.Insert(0, null!));

        add.ParamName.ShouldBe("item");
        insert.ParamName.ShouldBe("item");
        table.Rows.ShouldBeEmpty();
    }

    /// <summary>Verifies row replacement reports the public indexer value parameter before mutation.</summary>
    [Fact]
    public void Rows_WhenNullReplacesRow_ReportsValueParameterWithoutMutation()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        var original = new TableRow([new ControlText("Original")]);
        table.Rows.Add(original);

        var exception = Should.Throw<ArgumentNullException>(() => table.Rows[0] = null!);

        exception.ParamName.ShouldBe("value");
        table.Rows.ShouldBe([original]);
    }

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
        second.Bounds.ShouldBe(new Rect(6, 2, 5, 1));
        third.Bounds.ShouldBe(new Rect(16, 2, 4, 1));
        table.DesiredSize.ShouldBe(new Size(20, 3));
    }

    /// <summary>Verifies an ordinary interactive cell keeps its measured size inside a larger row slot.</summary>
    [Fact]
    public void Layout_WhenCellUsesIntrinsicAlignment_KeepsMeasuredBounds()
    {
        var option = new CheckBox
        {
            Content = new ControlText("Include integration tests"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var table = new Table
        {
            Width = Length.Cells(48),
            CellPadding = new Thickness(1, 0),
        };
        table.Columns.Add(TableColumn.Fixed("Action", 16));
        table.Columns.Add(TableColumn.Fill("Configuration"));
        table.Rows.Add(new TableRow([
            new Button { Content = new ControlText("Run checks") },
            option,
        ]));

        new Engine().Layout(table, new Size(48, 8));

        option.Bounds.Width.ShouldBe(option.DesiredSize.Width);
        option.Bounds.Height.ShouldBe(option.DesiredSize.Height);
    }

    /// <summary>Verifies an explicitly stretched cell continues to consume its complete resolved track slot.</summary>
    [Fact]
    public void Layout_WhenCellExplicitlyStretches_FillsResolvedTrackSlot()
    {
        var option = new CheckBox
        {
            Content = new ControlText("Option"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var table = new Table
        {
            Width = Length.Cells(20),
            ShowHeader = false,
            ShowGridLines = false,
        };
        table.Columns.Add(TableColumn.Fixed("Action", 10));
        table.Columns.Add(TableColumn.Fixed("Choice", 10));
        table.Rows.Add(new TableRow([
            new Button { Content = new ControlText("Run") },
            option,
        ]));

        new Engine().Layout(table, new Size(20, 3));

        option.Bounds.ShouldBe(new Rect(10, 0, 10, 3));
    }

    /// <summary>Verifies horizontally scrolled headers, grid lines, row cells, hit testing, and rail chrome stay aligned.</summary>
    [Fact]
    public void Render_WhenHorizontallyScrolled_TranslatesCompleteTableContent()
    {
        var first = new ControlText("12345678");
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("ABCDEFGH", 8));
        table.Columns.Add(TableColumn.Fixed("IJKLMNOP", 8));
        table.Rows.Add(new TableRow([first, new ControlText("abcdefgh")]));
        var size = new Size(10, 4);
        var engine = new Engine();
        engine.Layout(table, size);
        table.HorizontalOffset = 3;

        engine.Layout(table, size);
        using Frame frame = new(size);
        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("D");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("4");
        table.HitTest(new Point(0, 2)).ShouldBeSameAs(first);
        _ = table.HitTest(new Point(0, 3)).ShouldBeOfType<ScrollBar>();
    }

    /// <summary>Verifies simultaneous offsets may move the content origin above and left of the viewport.</summary>
    [Fact]
    public void Layout_WhenBothAxesScroll_AllowsSignedContentOrigin()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));

        for (var index = 0; index < 8; index++)
        {
            table.Rows.Add(new TableRow([
                new ControlText($"A{index}"),
                new ControlText($"B{index}"),
            ]));
        }

        var engine = new Engine();
        var size = new Size(10, 5);
        engine.Layout(table, size);
        table.HorizontalOffset = 3;
        table.VerticalOffset = 3;

        engine.Layout(table, size);

        table.HorizontalOffset.ShouldBe(3);
        table.VerticalOffset.ShouldBe(3);
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
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        var table = new Table() { Style = style };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var size = new Size(14, 4);
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
        var style = ThemeTestSupport.OverlayStyle<Table>(
            (State.Normal, new ThemeOverlay(
                attributes: Attributes.RapidBlink,
                underline: Underline.Dashed,
                underlineColor: Color.Indexed(5))));
        var table = new Table() { Style = style };
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(new TableRow([new ControlText("A")]));
        var size = new Size(6, 3);
        new Engine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        var header = frame.GetCell(default).Style;
        var grid = frame.GetCell(new Point(0, 1)).Style;
        header.Attributes.ShouldBe(Attributes.RapidBlink);
        header.Underline.ShouldBe(Underline.Dashed);
        header.UnderlineColor.ShouldBe(Color.Indexed(5));
        grid.ShouldBe(header);
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
        using Frame frame = new(new Size(20, 10));

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
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        table.DesiredSize.ShouldBe(new Size(11, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a table taller than its viewport exposes vertical scroll via the intrinsic scroll surface.</summary>
    [Fact]
    public void Extent_WhenRowsExceedViewport_ExposesVerticalScroll()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Fill("Value"));

        for (var index = 0; index < 40; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Row {index}"), new ControlText("Value")]));
        }

        new Engine().Layout(table, new Size(30, 10));

        table.Extent.Height.ShouldBeGreaterThan(table.Viewport.Height);
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
        table.GetType().GetProperty("Children").ShouldBeNull();
        cell.Parent.ShouldBeNull();
    }
}
