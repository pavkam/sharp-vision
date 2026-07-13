using SharpVision.Controls;
using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.Grid;

/// <summary>Documents and demonstrates the Grid control.</summary>
internal sealed class GridPane: ShowcasePane
{
    private const string _catalogSummary =
        "Allocates fixed, automatic, percentage, and proportional tracks with exact integer rounding and spans.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Layout", "Set rows, columns, and spans", "Children receive committed cells from the shared track allocator."),
        PaneMetadata.Interaction("Resize", "Change the available bounds", "Percentage and proportional tracks resolve from final cells with deterministic rounding."),
        PaneMetadata.Interaction("Focus", "Move focus with Tab or Shift+Tab", "Traversal follows stable child order, not visual row or column order."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Rows / Columns", "TrackCollection", "one implicit Auto", "Define validated track lengths, minimums, and maximums for each axis."),
        PaneMetadata.Property("RowSpacing", "int", "0", "Adds non-negative cells between resolved row tracks while preserving containment."),
        PaneMetadata.Property("ColumnSpacing", "int", "0", "Adds non-negative cells between resolved column tracks while preserving containment."),
        PaneMetadata.Property("Row / Column", "int", "0", "Attach a zero-based starting track to each child."),
        PaneMetadata.Property("RowSpan / ColumnSpan", "int", "1", "Attach positive contiguous spans that contribute intrinsic size across tracks."),
    ];

    /// <summary>Initializes the Grid showcase page and composes its specimens.</summary>
    internal GridPane()
        : base("Grid", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Grid",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new GridPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var grid = new ControlGrid
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
        var spanning = PaneSupport.Card(new ControlText("ColumnSpan = 2"), Glyphs.Rounded);
        ControlGrid.SetRow(spanning, 1);
        ControlGrid.SetColumn(spanning, 0);
        ControlGrid.SetColumnSpan(spanning, 2);
        grid.Children.Add(spanning);
        PaneSupport.AddGrid(grid, "Auto / Star", 2, 2);
        examples.Children.Add(grid);
    }
}
