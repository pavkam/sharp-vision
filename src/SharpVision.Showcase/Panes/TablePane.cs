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
        var primary = new Table()
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
        var linked = new Text(
            "Open <link=https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>protocol guide</link>")
        {
            Overflow = Overflow.Wrap,
        };
        primary.Rows.Add(new TableRow([
            new Text("UI toolkit"),
            new Text("Preview"),
            linked,
        ]));

        var compact = new Table()
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

        var interactive = new Table
        {
            Width = Length.Cells(48),
            ShowGridLines = true,
            CellPadding = new Thickness(1, 0),
        };
        interactive.Columns.Add(TableColumn.Fixed("Action", 16));
        interactive.Columns.Add(TableColumn.Fill("Configuration"));
        interactive.Rows.Add(new TableRow([
            new Button { Content = new Text("Run checks") },
            new CheckBox { Content = new Text("Include integration tests"), IsChecked = true },
        ]));

        var dynamic = new Table
        {
            Width = Length.Cells(42),
            CellPadding = new Thickness(1, 0),
        };
        dynamic.Columns.Add(TableColumn.Fixed("Release", 14));
        dynamic.Columns.Add(TableColumn.Fill("State"));
        dynamic.Rows.Add(new TableRow([new Text("1.0"), new Text("Stable")]));
        var rowStatus = new Text("Rows: 1");
        var addRow = new Button { Content = new Text("Add release row") };
        addRow.Click += (_, _) =>
        {
            dynamic.Rows.Add(new TableRow([new Text("1.1"), new Text("Preview")]));
            rowStatus.Content = $"Rows: {dynamic.Rows.Count}";
        };

        var unicode = new Table
        {
            Width = Length.Cells(44),
            CellPadding = new Thickness(1, 0),
            ShowGridLines = true,
        };
        unicode.Columns.Add(TableColumn.Auto("Name"));
        unicode.Columns.Add(TableColumn.Fill("Details"));
        unicode.Rows.Add(new TableRow([
            new Text("你好 👩‍💻"),
            new Text("Wide graphemes and wrapped details keep complete cell ownership.")
            {
                Overflow = Overflow.Wrap,
            },
        ]));

        var headerOnly = new Table { Width = Length.Cells(28), ShowHeader = true };
        headerOnly.Columns.Add(TableColumn.Fill("Header only"));
        var tiny = new Table { Width = Length.Cells(8), ShowGridLines = true };
        tiny.Columns.Add(TableColumn.Fixed("A", 6));
        tiny.Columns.Add(TableColumn.Fill("B"));
        tiny.Rows.Add(new TableRow([new Text("Alpha"), new Text("Beta")]));

        return Doc.Page(
            Title,
            "Owns typed rows and column definitions to render aligned rich terminal cells with optional headers and grid lines.",
            Doc.Section(
                "Column sizing",
                "Fixed, automatic, percentage, and fill columns share the same finite track allocator.",
                Doc.Example(
                    "Mixed data columns",
                    "Fixed identity, percentage status, and fill details stay contained while marked detail text wraps.",
                    primary,
                    "table.Columns.Add(TableColumn.Fixed(\"Name\", 12));\ntable.Columns.Add(TableColumn.Percent(\"Status\", 25));\ntable.Columns.Add(TableColumn.Fill(\"Details\"));")),
            Doc.Section(
                "Header and grid chrome",
                "Headers and grid lines are optional; padding and spacing can carry simpler key/value structure.",
                Doc.Example(
                    "Compact headerless table",
                    "The key/value table omits headers and lines while retaining aligned ordinary Text cells.",
                    compact)),
            Doc.Section(
                "Interactive cells",
                "Every cell is an ordinary control, so focus, keyboard, pointer, and routed events remain available.",
                Doc.Example(
                    "Actions and options",
                    "Tab into the Button and CheckBox; Table contributes layout only and does not intercept their semantics.",
                    interactive)),
            Doc.Section(
                "Dynamic rows",
                "Rows transfer unique detached controls into table ownership and may be added or removed at runtime.",
                Doc.Example(
                    "Append a release",
                    "Add a fresh row and observe the owned row count update without rebuilding the Table.",
                    Doc.Column(dynamic, addRow, rowStatus))),
            Doc.Section(
                "Responsive text",
                "Marked links, CJK, emoji, and wrapping use the normal control and Unicode geometry pipeline inside cells.",
                Doc.Example(
                    "Wide and wrapped cells",
                    "Narrow the page and the detail cell reflows while wide graphemes retain their continuation cells.",
                    unicode)),
            Doc.Section(
                "Boundary states",
                "Header-only and tiny tables reserve only geometry they can safely contain.",
                Doc.Example(
                    "No phantom row and constrained columns",
                    "The first table has no data rows; the second saturates its narrow columns without drawing outside bounds.",
                    Doc.Column(headerOnly, tiny))));
    }
}
