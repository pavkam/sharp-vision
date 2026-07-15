// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the ComboBox control with live popup-selection specimens.</summary>
internal sealed class ComboBoxPane: CompositeControl
{

    internal ComboBoxPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ComboBox";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var density = new Text("Selected: Comfortable");
        var comboBox = new ComboBox()
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
            DropDownHeight = 4,
        };
        comboBox.SelectionChanged += (_, _) =>
            density.Content = comboBox.SelectedIndex >= 0
                ? $"Selected: {comboBox.Items[comboBox.SelectedIndex]}."
                : "No selection.";
        var bordered = new ComboBox
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 0,
        };
        var commitStatus = new Text("Committed: Comfortable");
        var commitCombo = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
        };
        commitCombo.SelectionChanged += (_, _) =>
            commitStatus.Content = commitCombo.SelectedIndex >= 0
                ? $"Committed: {commitCombo.Items[commitCombo.SelectedIndex]}"
                : "Committed: none";

        var emptyStatus = new Text("Selected: Clearable one.");
        var emptyCombo = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Clearable one", "Clearable two"],
            SelectedIndex = 0,
        };
        emptyCombo.SelectionChanged += (_, _) =>
            emptyStatus.Content = emptyCombo.SelectedIndex >= 0
                ? $"Selected: {emptyCombo.Items[emptyCombo.SelectedIndex]}."
                : "No selection.";
        var clearSelection = new Button() { Content = new Text("Clear selection") };
        clearSelection.Click += (_, _) => emptyCombo.SelectedIndex = -1;
        var stage = new Canvas()
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            ClipToBounds = false,
        };
        stage.Children.Add(comboBox);

        var manyItems = new object?[12];
        for (var index = 0; index < manyItems.Length; index++)
        {
            manyItems[index] = $"Item {index + 1}";
        }

        var tallStatus = new Text("Selected: Item 1");
        var tall = new ComboBox()
        {
            Width = Length.Cells(24),
            Items = manyItems,
            SelectedIndex = 0,
            DropDownHeight = 6,
        };
        tall.SelectionChanged += (_, _) =>
            tallStatus.Content = tall.SelectedIndex >= 0
                ? $"Selected: {tall.Items[tall.SelectedIndex]}."
                : "No selection.";
        var tallStage = new Canvas()
        {
            Width = Length.Cells(26),
            Height = Length.Cells(8),
            ClipToBounds = false,
        };
        tallStage.Children.Add(tall);

        var disabled = new ComboBox()
        {
            Width = Length.Cells(24),
            Items = ["Locked choice"],
            SelectedIndex = 0,
            IsEnabled = false,
        };

        var edgeStatus = new Text("Open near the lower edge to observe fallback placement.");
        var edgeChoice = new ComboBox
        {
            Width = Length.Cells(24),
            Items = ["Above when needed", "Clamped to host"],
            SelectedIndex = 0,
            DropDownHeight = 4,
        };
        edgeChoice.SelectionChanged += (_, _) =>
            edgeStatus.Content = $"Committed: {edgeChoice.Items[edgeChoice.SelectedIndex]}";
        var edgeStage = new Canvas
        {
            Width = Length.Cells(30),
            Height = Length.Cells(5),
            ClipToBounds = false,
        };
        Canvas.SetTop(edgeChoice, Length.Cells(3));
        edgeStage.Children.Add(edgeChoice);

        return Doc.Page(
            Title,
            "Displays one selected value and opens an owned popup-style List for keyboard or pointer choice.",
            Doc.Section(
                "🔽",
                "Start here",
                "Choose one compact value while keeping the full choice list available on demand.",
                Doc.Example(
                    "Default and bordered fields",
                    "Both fields own an opaque Surface. Click, Enter, or Space opens the list; Enter commits, Escape or an outside click or wheel dismisses without changing the value.",
                    Doc.Column(
                        new Text("Default field") { Attributes = TerminalAttributes.Bold },
                        stage,
                        new Text("Explicit rounded border") { Attributes = TerminalAttributes.Bold },
                        bordered,
                        density),
                    "var density = new ComboBox\n{\n    Items = [\"Compact\", \"Comfortable\", \"Spacious\"],\n    SelectedIndex = 1,\n};")),
            Doc.Section(
                "🔽",
                "Commit versus dismiss",
                "The popup keeps hover and keyboard highlight separate from the committed selection, and outside input closes it cleanly.",
                Doc.Example(
                    "Enter commits; Escape dismisses",
                    "Open the first field, move with arrows, then compare Enter with Escape. Escape closes without replacing the previous value.",
                    Doc.Column(commitCombo, commitStatus))),
            Doc.Section(
                "🔽",
                "Long choices",
                "Cap popup height and let the owned List provide ordinary scrolling for the remaining choices.",
                Doc.Example(
                    "Capped drop-down",
                    "Only six rows are visible. Arrow, wheel, and paging input scroll inside the popup before the page.",
                    Doc.Column(tallStage, tallStatus),
                    "combo.DropDownHeight = 6;\ncombo.ShowScrollBars = ShowScrollBars.WhenNeeded;")),
            Doc.Section(
                "🔽",
                "No selection",
                "SelectedIndex -1 is a valid explicit empty state.",
                Doc.Example(
                    "Clear the committed value",
                    "Activate Clear selection. The field and status update through the same SelectionChanged contract.",
                    Doc.Column(clearSelection, emptyCombo, emptyStatus))),
            Doc.Section(
                "🔽",
                "Constrained placement",
                "The owned Popup prefers below, flips when the lower edge cannot fit, and clamps to its host.",
                Doc.Example(
                    "Lower-edge field",
                    "Open this field near the stage bottom and resize the terminal to watch placement recompute.",
                    Doc.Column(edgeStage, edgeStatus))),
            Doc.Section(
                "🔽",
                "Unavailable state",
                "Keep a locked choice visible without allowing focus or popup activation.",
                Doc.Example(
                    "Disabled field",
                    "The committed value remains readable while keyboard and pointer changes are ignored.",
                    disabled)));
    }
}
