// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Table control with mixed column sizing and headerless specimens.</summary>
internal sealed class TablePane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Table";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Table primary = new()
        {
            Width = Length.Cells(58),
            ShowHeader = true,
            CellPadding = new Thickness(1, 0),
            RowSpacing = 1,
        };
        primary.Columns.Add(TableColumn.Fixed("Name", 12));
        primary.Columns.Add(TableColumn.Percent("Status", 25));
        primary.Columns.Add(TableColumn.Fill("Details"));
        primary.Rows.Add(new TableRow([
            new Text("Terminal core"),
            new Text("Stable"),
            new Text("ANSI, OSC, CSI, and input decoding."),
        ]));
        RichText linked = new() { Wrapping = Wrapping.Word };
        linked.Inlines.Add(new Run("Open "));
        linked.Inlines.Add(new Hyperlink("protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));
        primary.Rows.Add(new TableRow([
            new Text("UI toolkit"),
            new Text("Preview"),
            linked,
        ]));

        Table compact = new()
        {
            Width = Length.Cells(42),
            ShowHeader = false,
            ShowGridLines = false,
            CellPadding = new Thickness(1, 0),
            ColumnSpacing = 2,
        };
        compact.Columns.Add(TableColumn.Auto("Key"));
        compact.Columns.Add(TableColumn.Fill("Meaning"));
        compact.Rows.Add(new TableRow([new Text("Enter"), new Text("Apply the default action")]));
        compact.Rows.Add(new TableRow([new Text("Escape"), new Text("Dismiss a popup or cancel a window")]));

        return Doc.Page(
            Title,
            "Owns typed rows and column definitions to render aligned rich terminal cells with optional headers and grid lines.",
            Doc.Example(
                "Mixed column sizing",
                "Fixed identity, percentage status, and fill details stay contained while the rich detail cell wraps and preserves its OSC 8 link.",
                primary),
            Doc.Example(
                "Headerless key/value table",
                "A compact table can omit headers and grid lines when spacing and cell padding carry the structure.",
                compact));
    }
}
