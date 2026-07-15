// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the List control with live, themed selection specimens.</summary>
internal sealed class ListPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "List";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var status = new Text("Selected item: Beta");
        var active = new List()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Items = new object?[]
            {
                "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
            },
            SelectedIndex = 1,
        };
        active.SelectionChanged += (_, _) =>
        {
            status.Content = active.SelectedIndex >= 0
                ? $"Selected item: {active.Items[active.SelectedIndex]}"
                : "No item selected.";
        };
        active.ItemInvoked += (_, eventArgs) =>
            status.Content = $"Activated {eventArgs.Item} via {eventArgs.Cause}.";

        var multipleStatus = new Text("Multiple: none");
        var multiple = new List
        {
            Width = Length.Cells(20),
            Height = Length.Cells(5),
            Items = new object?[] { "Alpha", "Beta", "Gamma", "Delta" },
            SelectionMode = SelectionMode.Multiple,
        };
        multiple.SelectionChanged += (_, _) =>
            multipleStatus.Content = multiple.SelectedItems.Count == 0
                ? "Multiple: none"
                : $"Multiple: {string.Join(", ", multiple.SelectedItems)}";
        var selectMultiple = new Button() { Content = new Text("Select Alpha and Gamma") };
        selectMultiple.Click += (_, _) =>
        {
            _ = multiple.SetSelected(0, true);
            _ = multiple.SetSelected(2, true);
        };

        var templated = new List
        {
            Width = Length.Cells(30),
            Height = Length.Cells(7),
            Items = new object?[] { "Renderer", "Input", "Layout" },
            ItemTemplate = item => Doc.Card(Doc.Column(
                new Text(item?.ToString() ?? "(null)") { Attributes = TerminalAttributes.Bold },
                new Text("Ordinary controls in a realized row"))),
        };

        var snapshotStatus = new Text("Snapshot: 8 items");
        var replace = new Button() { Content = new Text("Replace item snapshot") };
        replace.Click += (_, _) =>
        {
            active.Items = new object?[] { "One", "Two", "Three" };
            snapshotStatus.Content = "Snapshot: 3 items, selection normalized";
        };

        var longList = new List
        {
            Width = Length.Cells(18),
            Height = Length.Cells(5),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Items = new object?[]
            {
                "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            },
        };

        var disabled = new List()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Items = new object?[] { "Alpha", "Beta", "Gamma" },
        };

        return Doc.Page(
            Title,
            "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.",
            Doc.Section(
                "📋",
                "Single selection",
                "Active navigation, committed selection, and invocation are related but distinct states.",
                Doc.Example(
                    "Selectable result list",
                    "Use arrows or paging to move, Space to select, and Enter to invoke. The status names the committed operation.",
                    Doc.Column(active, status),
                    "var results = new List\n{\n    Items = files,\n    SelectionMode = SelectionMode.Single,\n};")),
            Doc.Section(
                "📋",
                "Selection modes",
                "None permits navigation only, Single retains at most one row, and Multiple owns a sorted selected set.",
                Doc.Example(
                    "Multiple selection",
                    "Use Control to toggle and Shift for a range, or activate the programmatic recipe to select Alpha and Gamma.",
                    Doc.Column(multiple, selectMultiple, multipleStatus),
                    "list.SelectionMode = SelectionMode.Multiple;\nlist.SetSelected(0, true);")),
            Doc.Section(
                "📋",
                "Templates",
                "ItemTemplate creates one unique detached ordinary control tree for each item.",
                Doc.Example(
                    "Rich realized rows",
                    "Each row contains bold identity and secondary status text while retaining List focus and selection behavior.",
                    templated)),
            Doc.Section(
                "📋",
                "Long data",
                "The first milestone realizes its snapshot and scrolls it through the shared container policy.",
                Doc.Example(
                    "Paging and bring-into-view",
                    "Use Home, End, Page Up, Page Down, arrows, or the thin rail; the active row remains visible.",
                    longList)),
            Doc.Section(
                "📋",
                "Snapshot replacement",
                "Replacing Items copies a new snapshot and normalizes selection after the candidate tree validates.",
                Doc.Example(
                    "Replace the data set",
                    "Activate the button to swap eight rows for three without reusing owned template controls.",
                    Doc.Column(replace, snapshotStatus))),
            Doc.Section(
                "📋",
                "Unavailable items",
                "Unavailable context may remain visible while focus, selection, and invocation are suppressed.",
                Doc.Example(
                    "Disabled list",
                    "These rows remain readable but cannot receive focus or change selection.",
                    disabled)));
    }
}
