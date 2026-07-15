// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the ComboBox control with live popup-selection specimens.</summary>
internal sealed class ComboBoxPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ComboBox";

    /// <inheritdoc/>
    protected override Control Build()
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

        return Doc.Page(
            Title,
            "Displays one selected value and opens an owned popup-style List for keyboard or pointer choice.",
            Doc.Example(
                "Popup choice field",
                "Click, Enter, or Space opens the drop-down. The owned list handles arrow navigation while open; Enter commits the highlighted item and closes the popup, while Escape dismisses it and keeps the previous selection.",
                Doc.Column(stage, density)),
            Doc.Example(
                "Capped drop-down height",
                "DropDownHeight caps how many rows the popup shows regardless of item count. With more items than fit, the owned list's own scrolling takes over inside the capped popup.",
                Doc.Column(tallStage, tallStatus)),
            Doc.Example(
                "Disabled",
                "A disabled ComboBox keeps its current selection visible while refusing focus, so it can neither open its popup nor change its value.",
                disabled));
    }
}
