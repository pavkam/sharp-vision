// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Grid control with fixed, star, auto, and spanning track specimens.</summary>
internal sealed class GridPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Grid";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Grid fixedTracks = new()
        {
            Width = Length.Cells(33),
            Height = Length.Cells(4),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        fixedTracks.Rows.Add(Track.Cells(3));
        fixedTracks.Columns.Add(Track.Cells(9));
        fixedTracks.Columns.Add(Track.Cells(9));
        fixedTracks.Columns.Add(Track.Cells(9));
        AddCell(fixedTracks, "9 cells", 0, 0);
        AddCell(fixedTracks, "9 cells", 0, 1);
        AddCell(fixedTracks, "9 cells", 0, 2);

        Grid proportionalTracks = new()
        {
            Width = Length.Cells(40),
            Height = Length.Cells(14),
            RowSpacing = 1,
        };
        proportionalTracks.Rows.Add(Track.Auto());
        proportionalTracks.Rows.Add(Track.Star(2));
        proportionalTracks.Rows.Add(Track.Star(1));
        proportionalTracks.Columns.Add(Track.Star(1));
        AddTrackRegion(proportionalTracks, "Auto = intrinsic 3 rows", 0, ThemeColors.Surface);
        AddTrackRegion(proportionalTracks, "2* = 6 rows", 1, ThemeColors.Accent);
        AddTrackRegion(proportionalTracks, "1* = 3 rows", 2, ThemeColors.Info);

        Grid spans = new()
        {
            Width = Length.Cells(36),
            Height = Length.Cells(9),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        spans.Rows.Add(Track.Star(1));
        spans.Rows.Add(Track.Star(1));
        spans.Rows.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        var rowSpan = Card("RowSpan = 2");
        Grid.SetRow(rowSpan, 0);
        Grid.SetColumn(rowSpan, 0);
        Grid.SetRowSpan(rowSpan, 2);
        spans.Children.Add(rowSpan);
        var columnSpan = Card("ColumnSpan = 2");
        Grid.SetRow(columnSpan, 0);
        Grid.SetColumn(columnSpan, 1);
        Grid.SetColumnSpan(columnSpan, 2);
        spans.Children.Add(columnSpan);
        var both = Card("Row + Column span");
        Grid.SetRow(both, 1);
        Grid.SetColumn(both, 1);
        Grid.SetRowSpan(both, 2);
        Grid.SetColumnSpan(both, 2);
        spans.Children.Add(both);
        AddCell(spans, "1x1", 2, 0);

        var percentage = new Grid
        {
            Width = Length.Cells(40),
            Height = Length.Cells(5),
            ColumnSpacing = 1,
        };
        percentage.Columns.Add(Track.Percent(40, minimum: 10, maximum: 16));
        percentage.Columns.Add(Track.Star(1, minimum: 8));
        percentage.Rows.Add(Track.Star(1));
        AddCell(percentage, "40% min 10 max 16", 0, 0);
        AddCell(percentage, "Star min 8", 0, 1);

        var implicitGrid = new Grid { Width = Length.Cells(28), Height = Length.Cells(3) };
        implicitGrid.Children.Add(Card("Implicit auto row + column"));

        var form = new Grid
        {
            Width = Length.Cells(42),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        form.Columns.Add(Track.Cells(10));
        form.Columns.Add(Track.Star(1, minimum: 12));
        form.Rows.Add(Track.Auto());
        form.Rows.Add(Track.Auto());
        form.Rows.Add(Track.Auto());
        AddFormCell(form, new Text("Project"), 0, 0);
        AddFormCell(form, new TextInput { Text = "SharpVision" }, 0, 1);
        AddFormCell(form, new Text("Owner"), 1, 0);
        AddFormCell(form, new TextInput { Text = "Terminal team" }, 1, 1);
        var validation = new Text("Validation wraps beneath the finite field width.")
        {
            Overflow = Overflow.Wrap,
        };
        Grid.SetRow(validation, 2);
        Grid.SetColumn(validation, 1);
        form.Children.Add(validation);

        var constrained = new Grid
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            ColumnSpacing = 3,
        };
        constrained.Columns.Add(Track.Cells(8));
        constrained.Columns.Add(Track.Star(1));
        constrained.Rows.Add(Track.Star(1));
        AddCell(constrained, "Fixed", 0, 0);
        AddCell(constrained, "Star", 0, 1);

        return Doc.Page(
            Title,
            "Allocates fixed, automatic, percentage, and proportional tracks across rows and columns with exact integer rounding and spans.",
            Doc.Section(
                "🧱",
                "Track fundamentals",
                "Combine fixed, automatic, and proportional tracks on both axes.",
                Doc.Example(
                    "Fixed columns",
                    "Each column keeps exactly nine cells while spacing remains outside the track widths.",
                    fixedTracks,
                    "grid.Columns.Add(Track.Cells(9));\ngrid.Columns.Add(Track.Star(1));"),
                Doc.Example(
                    "Auto and star allocation",
                    "The Surface-backed regions make the allocation visible: Auto keeps its three-row intrinsic card, then 2* receives six rows and 1* receives three.",
                    proportionalTracks)),
            Doc.Section(
                "🧱",
                "Percentage and limits",
                "Percentage and star tracks honor visible minimum and maximum cell constraints.",
                Doc.Example(
                    "Bounded responsive tracks",
                    "Resize the page: the percentage track stays between ten and sixteen cells while star absorbs the safe remainder.",
                    percentage)),
            Doc.Section(
                "🧱",
                "Spans",
                "A child may own the union of adjacent rows, columns, or both, including internal gaps.",
                Doc.Example(
                    "Row and column spans",
                    "The three labeled cards occupy a vertical span, horizontal span, and combined area without inventing nested layout.",
                    spans)),
            Doc.Section(
                "🧱",
                "Implicit grid",
                "Empty row and column definitions behave exactly like one automatic track on each axis.",
                Doc.Example(
                    "Definition-free single cell",
                    "Use the implicit grid for one cell; add explicit tracks only when the layout needs them.",
                    implicitGrid)),
            Doc.Section(
                "🧱",
                "Responsive form",
                "Finite column widths remeasure wrapped controls so text growth can influence automatic rows.",
                Doc.Example(
                    "Labels, editors, and validation",
                    "Narrow the terminal and the validation message wraps beneath its field while the label column stays fixed.",
                    form)),
            Doc.Section(
                "🧱",
                "Constrained space",
                "When tracks and gaps cannot fit, spacing saturates and tracks shrink deterministically without negative geometry.",
                Doc.Example(
                    "Tiny two-column grid",
                    "The fixed request and wide gap leave only a contained remainder for the star track.",
                    constrained)));
    }

    private static void AddCell(Grid grid, string text, int row, int column)
    {
        var cell = Card(text);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static void AddFormCell(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static void AddTrackRegion(Grid grid, string text, int row, Color background)
    {
        var region = Card(text);
        region.Background = background;
        region.FillMode = FillMode.Opaque;
        Grid.SetRow(region, row);
        grid.Children.Add(region);
    }

    private static Dock Card(string text) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
        Padding = new Thickness(1, 0),
        Children = { new Text(text) },
    };
}
