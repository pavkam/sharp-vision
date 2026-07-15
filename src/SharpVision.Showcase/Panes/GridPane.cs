// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Grid control with fixed, star, auto, and spanning track specimens.</summary>
internal sealed class GridPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Grid";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var fixedTracks = new Grid()
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

        var proportionalTracks = new Grid()
        {
            Width = Length.Cells(40),
            Height = Length.Cells(7),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        proportionalTracks.Rows.Add(Track.Auto());
        proportionalTracks.Rows.Add(Track.Star(2));
        proportionalTracks.Rows.Add(Track.Star(1));
        proportionalTracks.Columns.Add(Track.Star(1));
        proportionalTracks.Columns.Add(Track.Star(2));
        AddCell(proportionalTracks, "Auto", 0, 0);
        var autoWide = Card("Auto sizes to this content");
        Grid.SetRow(autoWide, 0);
        Grid.SetColumn(autoWide, 1);
        proportionalTracks.Children.Add(autoWide);
        AddCell(proportionalTracks, "Star 1", 1, 0);
        AddCell(proportionalTracks, "Star 2", 1, 1);
        AddCell(proportionalTracks, "Star 1", 2, 0);
        AddCell(proportionalTracks, "Star 2", 2, 1);

        var spans = new Grid()
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

        return Doc.Page(
            Title,
            "Allocates fixed, automatic, percentage, and proportional tracks across rows and columns with exact integer rounding and spans.",
            Doc.Example(
                "Fixed tracks",
                "Every column is a Track.Cells fixed width, so each cell keeps exactly the same 9-cell width regardless of the Grid's overall size.",
                fixedTracks),
            Doc.Example(
                "Auto and star tracks",
                "The first row uses Track.Auto and sizes to its widest cell's content. The remaining rows split the leftover height between a Track.Star(2) and a Track.Star(1) row, a 2:1 ratio; columns split width the same way.",
                proportionalTracks),
            Doc.Example(
                "Row and column spans",
                "Grid.SetRowSpan and Grid.SetColumnSpan stretch one child across multiple tracks: a row span down the left column, a column span across the top right, and a child spanning both directions where they would otherwise overlap.",
                spans));
    }

    private static void AddCell(Grid grid, string text, int row, int column)
    {
        var cell = Card(text);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static Dock Card(string text) => new()
    {
        Children = { new Text(text) },
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
        Padding = new Thickness(1, 0),
    };
}
