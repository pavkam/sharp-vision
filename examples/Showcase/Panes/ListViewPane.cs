// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the ListView control with live, themed selection specimens.</summary>
internal sealed class ListViewPane: CompositeControlBase
{
    internal ListViewPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ListView";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var status = new Text("Selected item: Beta");
        var active = new ListView
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            Items = new object?[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" },
            SelectedIndex = 1
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
        var multiple = new ListView
        {
            Width = Length.Cells(20),
            Height = Length.Cells(5),
            Items = new object?[] { "Alpha", "Beta", "Gamma", "Delta" },
            SelectionMode = ListSelectionMode.Multiple
        };
        multiple.SelectionChanged += (_, _) =>
            multipleStatus.Content = multiple.SelectedItems.Count == 0
                ? "Multiple: none"
                : $"Multiple: {string.Join(", ", multiple.SelectedItems)}";
        var selectMultiple = new Button { Text = "&Select Alpha and Gamma" };
        selectMultiple.Click += (_, _) =>
        {
            _ = multiple.SetSelected(0, true);
            _ = multiple.SetSelected(2, true);
        };
        var selectAllMultiple = new Button { Text = "Select &all" };
        selectAllMultiple.Click += (_, _) =>
        {
            for (var index = 0; index < multiple.Items.Count; index++)
            {
                _ = multiple.SetSelected(index, true);
            }
        };
        var clearMultiple = new Button { Text = "&Clear" };
        clearMultiple.Click += (_, _) =>
        {
            for (var index = 0; index < multiple.Items.Count; index++)
            {
                _ = multiple.SetSelected(index, false);
            }
        };

        var templated = new ListView
        {
            Height = Length.Cells(7),
            Items = new object?[] { "Renderer", "Input", "Layout" },
            ItemTemplate = item => new DocCard(new DocColumn(
                new Text(item?.ToString() ?? "(null)")
                {
                    Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
                },
                new Text("Ordinary controls in a realized row")))
        };

        var snapshotStatus = new Text("Snapshot: 8 items");
        var replace = new Button { Text = "&Replace item snapshot" };
        replace.Click += (_, _) =>
        {
            active.Items = new object?[] { "One", "Beta", "Three" };
            snapshotStatus.Content = "Snapshot: 3 items, Beta selection retained";
        };

        var longList = new ListView
        {
            Width = Length.Cells(18),
            Height = Length.Cells(5),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            Items = new object?[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" }
        };

        var disabled = new ListView
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            ItemTemplate = item =>
            {
                var row = new Text(item?.ToString() ?? string.Empty);

                if (Equals(item, "Beta"))
                {
                    row.IsEnabled = false;
                }

                return row;
            },
            Items = new object?[] { "Alpha", "Beta", "Gamma" }
        };

        var empty = new ListView
        {
            Width = Length.Cells(18),
            Height = Length.Cells(2),
            Items = Array.Empty<object?>()
        };

        var virtualizedStatus = new Text("Active: Row 0");
        var virtualized = new ListView
        {
            Width = Length.Cells(20),
            Height = Length.Cells(6),
            RowHeight = 1,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            Items = Enumerable.Range(0, 20_000).Select(value => (object?) $"Row {value:D5}").ToArray()
        };
        virtualized.SelectionChanged += (_, _) =>
            virtualizedStatus.Content = virtualized.SelectedIndex >= 0
                ? $"Active: {virtualized.Items[virtualized.SelectedIndex]}"
                : "Active: none";

        return new DocPage(
            Title,
            "<info>ListView</info> realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.",
            new DocSection(
                "📋",
                "Single selection",
                "The quiet list background keeps selection prominent. Keyboard navigation moves selection immediately; <reverse>Enter</reverse> invokes it separately.",
                new DocExample(
                    "Selectable result list",
                    "Use arrows or paging to select and <reverse>Enter</reverse> to invoke. The status names the <success>committed operation</success>.",
                    new DocColumn(active, status),
                    "var results = new ListView\n{\n    Items = files,\n    SelectionMode = ListSelectionMode.Single,\n};")),
            new DocSection(
                "☑️",
                "Selection modes",
                "None permits navigation only, Single retains at most one row, and Multiple owns a sorted selected set.",
                new DocExample(
                    "Multiple selection",
                    "<reverse>Space</reverse> toggles individual items. Activate the programmatic recipe to select Alpha and Gamma.",
                    new DocColumn(multiple, new DocRow(selectMultiple, selectAllMultiple, clearMultiple), multipleStatus),
                    "list.SelectionMode = ListSelectionMode.Multiple;\nlist.SetSelected(0, true);")),
            new DocSection(
                "🧩",
                "Templates",
                "ItemTemplate creates one standalone control subtree for each item.",
                new DocExample(
                    "Rich realized rows",
                    "Each row contains bold identity and secondary status text while retaining ListView focus and selection behavior.",
                    templated)),
            new DocSection(
                "📜",
                "Long data",
                "Items scroll using the container's scrolling policy when they exceed the visible area.",
                new DocExample(
                    "Paging and bring-into-view",
                    "Use <reverse>Home</reverse>, <reverse>End</reverse>, <reverse>Page Up</reverse>, <reverse>Page Down</reverse>, arrows, or the thin rail; the active row remains visible.",
                    longList)),
            new DocSection(
                "🔄",
                "Snapshot replacement",
                "Replacing Items copies a new snapshot, normalizes selection, and publishes the same index delta in eager and windowed modes.",
                new DocExample(
                    "Replace the data set",
                    "Activate the button to swap eight rows for three without reusing owned template controls.",
                    new DocColumn(replace, snapshotStatus))),
            new DocSection(
                "🚫",
                "Unavailable and empty",
                "Unavailable rows remain visible while focus, selection, and invocation are suppressed; dynamic disable or hide transitions repair committed selection.",
                new DocExample(
                    "Disabled item and empty list",
                    "Beta is disabled inside an otherwise enabled list. The second specimen renders an empty Items snapshot.",
                    new DocColumn(disabled, empty))),
            new DocSection(
                "🪟",
                "Virtualization",
                "Setting RowHeight opts into windowed realization: only rows inside the viewport plus a bounded overscan margin are ever realized.",
                new DocExample(
                    "20,000 rows, viewport-bounded realization",
                    "Scroll, page, or use <reverse>Home</reverse>/<reverse>End</reverse> across 20,000 rows without realizing them all up front.",
                    new DocColumn(virtualized, virtualizedStatus),
                    "list.RowHeight = 1;\nlist.Items = hugeCollection;")));
    }
}
