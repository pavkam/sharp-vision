// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the Grid control.</summary>
internal sealed class GridShowcasePane: ShowcasePane
{
    internal const string Title = "Grid";
    private const string _catalogSummary =
        "Allocates fixed, automatic, percentage, and proportional tracks with exact integer rounding and spans.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Layout", "Set rows, columns, and spans", "Children receive committed cells from the shared track allocator."),
        new InteractionDescription("Resize", "Change the available bounds", "Percentage and proportional tracks resolve from final cells with deterministic rounding."),
        new InteractionDescription("Focus", "Move focus with Tab or Shift+Tab", "Traversal follows stable child order, not visual row or column order."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Rows / Columns", "TrackCollection", "one implicit Auto", "Define validated track lengths, minimums, and maximums for each axis."),
        new PropertyDescription("RowSpacing", "int", "0", "Adds non-negative cells between resolved row tracks while preserving containment."),
        new PropertyDescription("ColumnSpacing", "int", "0", "Adds non-negative cells between resolved column tracks while preserving containment."),
        new PropertyDescription("Row / Column", "int", "0", "Attach a zero-based starting track to each child."),
        new PropertyDescription("RowSpan / ColumnSpan", "int", "1", "Attach positive contiguous spans that contribute intrinsic size across tracks."),
    ];

    /// <summary>Initializes the Grid showcase page and composes its specimens.</summary>
    internal GridShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlGrid grid = new()
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
        PaneSupport.AddGrid(grid, "Fixed", 0, 0);
        PaneSupport.AddGrid(grid, "35%", 0, 1);
        PaneSupport.AddGrid(grid, "Star", 0, 2);
        ControlBorder spanning = PaneSupport.Card(new ControlText("ColumnSpan = 2"), Glyphs.Rounded);
        ControlGrid.SetRow(spanning, 1);
        ControlGrid.SetColumn(spanning, 0);
        ControlGrid.SetColumnSpan(spanning, 2);
        grid.Children.Add(spanning);
        PaneSupport.AddGrid(grid, "Auto / Star", 2, 2);
        examples.Children.Add(grid);
    }
}
