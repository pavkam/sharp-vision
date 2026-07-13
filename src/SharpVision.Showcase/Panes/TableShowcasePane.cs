namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Text;

/// <summary>Documents and demonstrates the Table control.</summary>
internal sealed class TableShowcasePane: ShowcasePane
{
    internal const string Title = "Table";
    private const string _catalogSummary =
        "Owns typed rows and column definitions to render aligned rich terminal cells with optional headers and grid lines.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Columns", "Choose fixed, automatic, percentage, or fill widths", "Each header and row cell shares one resolved track."),
        new InteractionDescription("Rows", "Add detached controls matching the column count", "Cells are owned by generated borders while their semantic controls remain interactive."),
        new InteractionDescription("Resize", "Change the available table width", "Percentage and fill columns recompute through the shared Grid allocator."),
        new InteractionDescription("Pointer and keyboard", "Interact with a focusable cell control", "The cell control receives normal routed input without bypassing the table layout."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Columns", "TableColumns", "empty", "Owns non-empty titled fixed, automatic, percentage, or proportional column definitions."),
        new PropertyDescription("Rows", "TableRows", "empty", "Owns rows whose detached controls exactly match the defined column count."),
        new PropertyDescription("ShowHeader", "bool", "true", "Renders a padded header row from each TableColumn Header value."),
        new PropertyDescription("CellPadding", "Thickness", "0", "Deflates every header and data cell with non-negative terminal-cell padding."),
        new PropertyDescription("RowSpacing / ColumnSpacing", "int", "0 / 0", "Add non-negative space between cells while preserving contained track geometry."),
        new PropertyDescription("ShowGridLines", "bool", "true", "Draws light Unicode lines in available table gaps using the configurable grid-line foreground."),
    ];

    /// <summary>Initializes the Table showcase page and composes its specimens.</summary>
    internal TableShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var primary = new ControlTable
        {
            Width = Length.Cells(58),
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            GridLineColor = Palette.Border,
            CellPadding = new Thickness(1, 0),
            RowSpacing = 1,
        };
        primary.Columns.Add(TableColumn.Fixed("Name", 12));
        primary.Columns.Add(TableColumn.Percent("Status", 25));
        primary.Columns.Add(TableColumn.Fill("Details"));
        primary.Rows.Add(new TableRow([
            new ControlText("Terminal core"),
            new ControlText("Stable") { Foreground = Palette.Success },
            new ControlText("ANSI, OSC, CSI, and input decoding."),
        ]));
        var linked = new ControlRichText { Wrapping = Wrapping.Word };
        linked.Inlines.Add(new ControlRun("Open "));
        linked.Inlines.Add(new Hyperlink("protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));
        primary.Rows.Add(new TableRow([
            new ControlText("UI toolkit"),
            new ControlText("Preview") { Foreground = Palette.Warning },
            linked,
        ]));

        var compact = new ControlTable
        {
            Width = Length.Cells(42),
            ShowHeader = false,
            ShowGridLines = false,
            CellPadding = new Thickness(1, 0),
            ColumnSpacing = 2,
        };
        compact.Columns.Add(TableColumn.Auto("Key"));
        compact.Columns.Add(TableColumn.Fill("Meaning"));
        compact.Rows.Add(new TableRow([new ControlText("Enter"), new ControlText("Apply the default action")]));
        compact.Rows.Add(new TableRow([new ControlText("Escape"), new ControlText("Dismiss a popup or cancel a window")]));

        examples.Children.Add(PaneSupport.SampleSection(
            "Mixed column sizing",
            "Fixed identity, percentage status, and fill details stay contained while the rich detail cell wraps and preserves its OSC 8 link.",
            primary));
        examples.Children.Add(PaneSupport.SampleSection(
            "Headerless key/value table",
            "A compact table can omit headers and grid lines when spacing and cell padding carry the structure.",
            compact));
    }
}
