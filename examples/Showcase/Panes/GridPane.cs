// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Grid control with fixed, star, auto, and spanning track specimens.</summary>
internal sealed class GridPane: CompositeControlBase
{
    internal GridPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Grid";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Fixed column tracks keep exact cell widths while spacing stays outside the tracks.
        Grid fixedTracks = new()
        {
            Width = Length.Cells(33),
            Height = Length.Cells(4),
            RowSpacing = 1,
            ColumnSpacing = 1
        };
        fixedTracks.Rows.Add(Track.Cells(3));
        fixedTracks.Columns.Add(Track.Cells(9));
        fixedTracks.Columns.Add(Track.Cells(9));
        fixedTracks.Columns.Add(Track.Cells(9));
        AddCell(fixedTracks, "9 cells", 0, 0);
        AddCell(fixedTracks, "9 cells", 0, 1);
        AddCell(fixedTracks, "9 cells", 0, 2);

        // Auto and star rows share one column; the bordered cards make allocation visible.
        Grid proportionalTracks = new() { Width = Length.Cells(40), Height = Length.Cells(14), RowSpacing = 1 };
        proportionalTracks.Rows.Add(Track.Auto());
        proportionalTracks.Rows.Add(Track.Star(2));
        proportionalTracks.Rows.Add(Track.Star(1));
        proportionalTracks.Columns.Add(Track.Star(1));
        AddTrackRegion(proportionalTracks, "Auto = intrinsic 3 rows", 0);
        AddTrackRegion(proportionalTracks, "2* = 6 rows", 1);
        AddTrackRegion(proportionalTracks, "1* = 3 rows", 2);

        // Row, column, and combined spans occupy adjacent track unions including internal gaps.
        Grid spans = new() { Width = Length.Cells(36), Height = Length.Cells(13), RowSpacing = 1, ColumnSpacing = 1 };
        spans.Rows.Add(Track.Star(1));
        spans.Rows.Add(Track.Star(1));
        spans.Rows.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        spans.Columns.Add(Track.Star(1));
        var rowSpan = ShowcasePaneHelpers.Card("RowSpan = 2", BorderGlyphStyle.Light);
        Grid.SetRow(rowSpan, 0);
        Grid.SetColumn(rowSpan, 0);
        Grid.SetRowSpan(rowSpan, 2);
        spans.Children.Add(rowSpan);
        var columnSpan = ShowcasePaneHelpers.Card("ColumnSpan = 2", BorderGlyphStyle.Light);
        Grid.SetRow(columnSpan, 0);
        Grid.SetColumn(columnSpan, 1);
        Grid.SetColumnSpan(columnSpan, 2);
        spans.Children.Add(columnSpan);
        var both = ShowcasePaneHelpers.Card("Row + Column span", BorderGlyphStyle.Light);
        Grid.SetRow(both, 1);
        Grid.SetColumn(both, 1);
        Grid.SetRowSpan(both, 2);
        Grid.SetColumnSpan(both, 2);
        spans.Children.Add(both);
        AddCell(spans, "1x1", 2, 0);

        // Percentage and star tracks honor minimum and maximum cell constraints.
        var percentage = new Grid { Width = Length.Cells(40), Height = Length.Cells(5), ColumnSpacing = 1 };
        percentage.Columns.Add(Track.Percent(40, minimum: 10, maximum: 16));
        percentage.Columns.Add(Track.Star(1, minimum: 8));
        percentage.Rows.Add(Track.Star(1));
        AddCell(percentage, "40%", 0, 0);
        AddCell(percentage, "Star", 0, 1);

        // An empty definition list behaves like one automatic track on each axis.
        var implicitGrid = new Grid { Width = Length.Cells(28), Height = Length.Cells(3) };
        implicitGrid.Children.Add(ShowcasePaneHelpers.Card("Implicit auto row + column", BorderGlyphStyle.Light));

        // Labels stay fixed while validation wraps beneath the star-width field column.
        var form = new Grid { Width = Length.Cells(42), RowSpacing = 1, ColumnSpacing = 1 };
        form.Columns.Add(Track.Cells(10));
        form.Columns.Add(Track.Star(1, minimum: 12));
        form.Rows.Add(Track.Auto());
        form.Rows.Add(Track.Auto());
        form.Rows.Add(Track.Auto());
        AddFormCell(form, new Text("Project"), 0, 0);
        AddFormCell(form, new TextInput { Text = "SharpVision" }, 0, 1);
        AddFormCell(form, new Text("Owner"), 1, 0);
        AddFormCell(form, new TextInput { Text = "Terminal team" }, 1, 1);
        var validation = new Text("Validation wraps beneath the finite field width.") { Overflow = Overflow.Wrap };
        Grid.SetRow(validation, 2);
        Grid.SetColumn(validation, 1);
        form.Children.Add(validation);

        // When tracks and gaps cannot fit, spacing saturates and tracks shrink deterministically.
        var constrained = new Grid { Width = Length.Cells(13), Height = Length.Cells(3), ColumnSpacing = 3 };
        constrained.Columns.Add(Track.Cells(8));
        constrained.Columns.Add(Track.Star(1));
        constrained.Rows.Add(Track.Star(1));
        AddCell(constrained, "Fixed", 0, 0);
        AddCell(constrained, "Star", 0, 1);

        return new DocPage(
            Title,
            "<info>Grid</info> allocates fixed, automatic, percentage, and proportional tracks across rows and columns with exact integer rounding and spans.",
            new DocSection(
                "🧱",
                "Track fundamentals",
                "Combine fixed, automatic, and proportional tracks on both axes.",
                new DocExample(
                    "Fixed columns",
                    "Each column keeps exactly nine cells while spacing remains outside the track widths.",
                    fixedTracks,
                    "grid.Columns.Add(Track.Cells(9));\ngrid.Columns.Add(Track.Cells(9));\ngrid.Columns.Add(Track.Cells(9));"),
                new DocExample(
                    "Auto and star allocation",
                    "The bordered regions make the allocation visible: Auto keeps its three-row intrinsic card, then 2* receives six rows and 1* receives three.",
                    proportionalTracks)),
            new DocSection(
                "📐",
                "Percentage and limits",
                "Percentage and star tracks honor enforced minimum and maximum cell constraints.",
                new DocExample(
                    "Bounded responsive tracks",
                    "Resize the page: the percentage track stays between ten and sixteen cells while star absorbs the safe remainder.",
                    percentage)),
            new DocSection(
                "↔️",
                "Spans",
                "A child may own the union of adjacent rows, columns, or both, including internal gaps.",
                new DocExample(
                    "Row and column spans",
                    "The three labeled cards occupy a vertical span, horizontal span, and combined area without inventing nested layout.",
                    spans)),
            new DocSection(
                "🔲",
                "Implicit grid",
                "Empty row and column definitions behave exactly like one automatic track on each axis.",
                new DocExample(
                    "Definition-free single cell",
                    "Use the implicit grid for one cell; add explicit tracks only when the layout needs them.",
                    implicitGrid)),
            new DocSection(
                "📄",
                "Responsive form",
                "When column widths are fixed, reflowing text can cause automatic rows to grow.",
                new DocExample(
                    "Labels, editors, and validation",
                    "Narrow the terminal and the validation message wraps beneath its field while the label column stays fixed.",
                    form)),
            new DocSection(
                "📏",
                "Constrained space",
                "When tracks and gaps cannot fit, spacing saturates and tracks shrink deterministically without negative geometry.",
                new DocExample(
                    "Tiny two-column grid",
                    "The fixed request and wide gap leave only a contained remainder for the star track.",
                    constrained)));
    }

    private static void AddCell(Grid grid, string text, int row, int column)
    {
        var cell = ShowcasePaneHelpers.Card(text, BorderGlyphStyle.Light);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static void AddFormCell(Grid grid, ControlBase control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static void AddTrackRegion(Grid grid, string text, int row)
    {
        var region = ShowcasePaneHelpers.Card(text, BorderGlyphStyle.Light);
        Grid.SetRow(region, row);
        grid.Children.Add(region);
    }
}
