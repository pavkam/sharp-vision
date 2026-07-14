// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Grid control with fixed, percentage, star, and spanning track specimens.</summary>
internal sealed class GridPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Grid";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Grid grid = new()
        {
            Width = Length.Cells(40),
            Height = Length.Cells(9),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        grid.Rows.Add(Track.Cells(2));
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Cells(8));
        grid.Columns.Add(Track.Percent(35));
        grid.Columns.Add(Track.Star(1));
        AddCell(grid, "Fixed", 0, 0);
        AddCell(grid, "35%", 0, 1);
        AddCell(grid, "Star", 0, 2);
        Border spanning = Card("ColumnSpan = 2");
        Grid.SetRow(spanning, 1);
        Grid.SetColumn(spanning, 0);
        Grid.SetColumnSpan(spanning, 2);
        grid.Children.Add(spanning);
        AddCell(grid, "Auto / Star", 2, 2);

        return Doc.Page(
            Title,
            "Allocates fixed, automatic, percentage, and proportional tracks across rows and columns with exact integer rounding and spans.",
            Doc.Example(
                "Track kinds and spans",
                "The first row is a fixed 2-cell strip, the second sizes to its Auto content, and the third takes a Star share of whatever height remains. Columns mix a fixed 8-cell strip, a 35% share, and a Star share. A child with ColumnSpan set to 2 stretches across the first two columns on the Auto row.",
                grid));
    }

    private static void AddCell(Grid grid, string text, int row, int column)
    {
        Border cell = Card(text);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static Border Card(string text) => new()
    {
        Child = new Text(text),
        BorderThickness = new Thickness(1),
        Glyphs = Glyphs.Light,
        Padding = new Thickness(1, 0),
    };
}
