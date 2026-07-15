// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the interactive command and choice showcase recipes.</summary>
public sealed class InputPaneTests
{
    /// <summary>Verifies the Button page demonstrates the public programmatic activation path.</summary>
    [Fact]
    public void Button_WhenProgrammaticExampleRuns_ReportsProgrammaticCause()
    {
        // Arrange
        using var page = new ButtonPane();
        new Engine().Layout(page, new Size(100, 80));
        var trigger = FindButton(page, "Run programmatically");

        // Act
        trigger.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Programmatic log: Programmatic");
    }

    /// <summary>Verifies the CheckBox page includes caller-defined validated state marks.</summary>
    [Fact]
    public void CheckBox_WhenPageBuilds_ContainsCustomMarks()
    {
        // Arrange
        using var page = new CheckBoxPane();
        new Engine().Layout(page, new Size(100, 80));

        // Act
        var checkBox = ControlTree.FindAll<CheckBox>(page).Single(value =>
            value.Content is ControlText { Content: "Custom marks" });

        // Assert
        checkBox.Marks.ShouldBe(new Marks(new Rune('·'), new Rune('✓'), new Rune('~')));
    }

    /// <summary>Verifies the RadioButton page begins one group empty and selects it programmatically.</summary>
    [Fact]
    public void RadioButton_WhenEmptyGroupIsSelected_ReportsCommittedMember()
    {
        // Arrange
        using var page = new RadioButtonPane();
        new Engine().Layout(page, new Size(100, 80));
        var trigger = FindButton(page, "Select first programmatically");

        // Act
        trigger.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Empty group: First");
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
