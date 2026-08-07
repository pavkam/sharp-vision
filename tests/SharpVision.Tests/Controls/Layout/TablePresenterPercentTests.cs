// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies how <c>TablePresenter</c> resolves Percent columns against the axis its own
/// spacing has already been taken out of.
///
/// <para><c>Tracks.Resolve</c> lets a caller separate the axis it allocates within from the axis
/// percentages resolve against. <c>docs/concepts/layout.md</c> makes the split normative: Grid and
/// <c>TablePresenter.MeasureCells</c> reserve spacing out of the incoming axis and pass no
/// <c>percentBase</c>, so their Percent tracks resolve against the reduced area, while Stack passes
/// its pre-spacing axis and resolves against the whole one. Grid and Stack are both asserted; the
/// table shares the allocator and was not, so nothing held it to the side of that split its own
/// documentation puts it on.</para>
///
/// <para>The gap the table reserves is <c>max(ColumnSpacing, grid-line thickness)</c>, not their
/// sum, which is why <c>ShowGridLines</c> and <c>ColumnSpacing</c> are exercised separately and
/// together.</para>
/// </summary>
public sealed class TablePresenterPercentTests
{
    /// <summary>Verifies Percent columns resolve against the axis left after ColumnSpacing, not the
    /// full one. Over 40 cells with a 4-cell gap the two halves are 18 each, not the 20 they would
    /// be if the table resolved percentages against the incoming axis the way Stack does.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsHaveColumnSpacing_ResolveAgainstTheSpacingReducedAxis()
    {
        var (table, first, second) = CreateTable(columnSpacing: 4, showGridLines: false);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - 4 = 36; cumulative edges at 50% and 100% of 36 give 18 and 36.
        first.Bounds.Width.ShouldBe(18);
        second.Bounds.Width.ShouldBe(18);
        second.Bounds.X.ShouldBe(22);
        ShouldExactlyTile(first, second, gap: 4, axis: 40);
    }

    /// <summary>Verifies grid lines reserve an axis gap of their own with no ColumnSpacing set, and
    /// that the odd remainder tiles rather than being lost: 39 cells split 20 and 19.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsShowGridLines_ReserveTheGridLineGap()
    {
        var (table, first, second) = CreateTable(columnSpacing: 0, showGridLines: true);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - 1 = 39; RoundPercent is AwayFromZero, so the 19.5 edge lands on 20 and
        // the second column takes the rest of the axis rather than rounding independently.
        first.Bounds.Width.ShouldBe(20);
        second.Bounds.Width.ShouldBe(19);
        second.Bounds.X.ShouldBe(21);
        ShouldExactlyTile(first, second, gap: 1, axis: 40);
    }

    /// <summary>The distinguishing case. With both set, the gap is the larger of the two and not
    /// their sum, so 37 cells remain rather than 36 - which is what separates a correct
    /// <c>max</c> from a plausible-looking <c>+</c>.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsHaveBothSpacingAndGridLines_ReserveTheLargerGapNotTheSum()
    {
        var (table, first, second) = CreateTable(columnSpacing: 3, showGridLines: true);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - max(3, 1) = 37, not 40 - (3 + 1) = 36. Summing would give 18 and 18.
        first.Bounds.Width.ShouldBe(19);
        second.Bounds.Width.ShouldBe(18);
        second.Bounds.X.ShouldBe(22);
        ShouldExactlyTile(first, second, gap: 3, axis: 40);
    }

    /// <summary>Pins the divergence itself. Identical Percent(50) participants and identical
    /// spacing over an identical axis give the table narrower columns than Stack, because only
    /// Stack resolves percentages against the pre-spacing axis. This is intentional; the point of
    /// asserting it is that changing either side becomes a deliberate act.</summary>
    [Fact]
    public void Layout_WhenTableAndStackShareParticipantsAndSpacing_TablePercentsAreNarrower()
    {
        var (table, tableFirst, _) = CreateTable(columnSpacing: 4, showGridLines: false);
        var stackFirst = new ControlText("Alpha") { Width = Length.Percent(50), HorizontalAlignment = HorizontalAlignment.Stretch };
        var stackSecond = new ControlText("Ready") { Width = Length.Percent(50), HorizontalAlignment = HorizontalAlignment.Stretch };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { stackFirst, stackSecond }
        };

        new LayoutEngine().Layout(table, new Size(40, 4));
        new LayoutEngine().Layout(stack, new Size(40, 4));

        tableFirst.Bounds.Width.ShouldBe(18, "the table resolves 50% of the 36 cells left after spacing");
        stackFirst.Bounds.Width.ShouldBe(20, "Stack resolves 50% of the full 40-cell axis");
        tableFirst.Bounds.Width.ShouldBeLessThan(stackFirst.Bounds.Width);
    }

    // Every cell of the axis is either a column or the single gap between them - no cell is
    // double-counted and none is dropped, which a per-column rounding bug would break even when the
    // individual widths still looked plausible.
    private static void ShouldExactlyTile(ControlBase first, ControlBase second, int gap, int axis)
    {
        second.Bounds.X.ShouldBe(first.Bounds.X + first.Bounds.Width + gap);
        (first.Bounds.Width + gap + second.Bounds.Width).ShouldBe(axis);
    }

    // ShowHeader and CellPadding are switched off so the assertions above read the column
    // allocation itself rather than a header row or a deflated content box.
    private static (Table Table, ControlText First, ControlText Second) CreateTable(
        int columnSpacing,
        bool showGridLines)
    {
        // Stretched so each cell's arranged width IS its column's width; a content-sized cell would
        // report its own text length instead and the assertions would stop reading the allocation.
        var first = new ControlText("Alpha") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var second = new ControlText("Ready") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var table = new Table
        {
            ShowHeader = false,
            Style = TableStyle.Default with { CellPadding = default },
            ColumnSpacing = columnSpacing,
            ShowGridLines = showGridLines
        };

        table.Columns.Add(TableColumn.Percent("Name", 50));
        table.Columns.Add(TableColumn.Percent("Status", 50));
        table.Rows.Add(new TableRow([first, second]));
        return (table, first, second);
    }
}
