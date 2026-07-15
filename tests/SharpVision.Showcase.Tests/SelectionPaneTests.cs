// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies selection and editing recipes exposed by showcase pages.</summary>
public sealed class SelectionPaneTests
{
    /// <summary>Verifies the ComboBox page demonstrates a valid empty selection.</summary>
    [Fact]
    public void ComboBox_WhenClearActionRuns_CommitsNoSelection()
    {
        // Arrange
        using var page = new ComboBoxPane();
        new Engine().Layout(page, new Size(100, 100));
        var comboBox = ControlTree.FindAll<ComboBox>(page).Single(value =>
            value.Items.Count == 2 && Equals(value.Items[0], "Clearable one"));
        var clear = FindButton(page, "Clear selection");

        // Act
        clear.PerformClick();

        // Assert
        comboBox.SelectedIndex.ShouldBe(-1);
        ControlTree.Text(page).ShouldContain("No selection.");
    }

    /// <summary>Verifies the List page demonstrates deterministic multiple selection.</summary>
    [Fact]
    public void List_WhenMultipleSelectionActionRuns_ReportsSortedItems()
    {
        // Arrange
        using var page = new ListPane();
        new Engine().Layout(page, new Size(100, 120));
        var list = ControlTree.FindAll<List>(page).Single(value =>
            value.SelectionMode == SelectionMode.Multiple);
        var select = FindButton(page, "Select Alpha and Gamma");

        // Act
        select.PerformClick();

        // Assert
        list.SelectedItems.ShouldBe(["Alpha", "Gamma"]);
        ControlTree.Text(page).ShouldContain("Multiple: Alpha, Gamma");
    }

    /// <summary>Verifies the TextInput page selects complete Unicode text through its public API.</summary>
    [Fact]
    public void TextInput_WhenSelectAllActionRuns_SelectsCompleteText()
    {
        // Arrange
        using var page = new TextInputPane();
        new Engine().Layout(page, new Size(100, 140));
        var editor = ControlTree.FindAll<TextInput>(page).Single(value =>
            value.Text == "Select café 👩‍💻");
        var select = FindButton(page, "Select all");

        // Act
        select.PerformClick();

        // Assert
        editor.SelectionStart.ShouldBe(0);
        editor.SelectionLength.ShouldBe(editor.Text.Length);
        ControlTree.Text(page).ShouldContain($"Selection: 0..{editor.Text.Length}");
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
