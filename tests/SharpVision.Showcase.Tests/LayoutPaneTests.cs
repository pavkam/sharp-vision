// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies live responsive layout recipes in the showcase.</summary>
public sealed class LayoutPaneTests
{
    /// <summary>Verifies Stack demonstrates the geometric difference between hidden and collapsed children.</summary>
    [Fact]
    public void Stack_WhenVisibilityExamplesBuild_ContainsHiddenAndCollapsedChildren()
    {
        // Arrange
        using var page = new StackPane();
        new Engine().Layout(page, new Size(100, 100));

        // Act
        var controls = ControlTree.FindAll<ControlText>(page);

        // Assert
        controls.Single(value => value.Content == "Hidden keeps its track").Visibility.ShouldBe(Visibility.Hidden);
        controls.Single(value => value.Content == "Collapsed releases its track").Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies Dock's shell recipe lets its fill reclaim a collapsed sidebar.</summary>
    [Fact]
    public void Dock_WhenSidebarToggleRuns_ReportsCollapsedShell()
    {
        // Arrange
        using var page = new DockPane();
        new Engine().Layout(page, new Size(100, 100));
        var toggle = FindButton(page, "Toggle sidebar");

        // Act
        toggle.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Sidebar: collapsed; main reclaimed the remainder");
    }

    /// <summary>Verifies Grid includes ordinary editors in its responsive form recipe.</summary>
    [Fact]
    public void Grid_WhenResponsiveFormBuilds_ContainsEditableFieldsAndValidation()
    {
        // Arrange
        using var page = new GridPane();
        new Engine().Layout(page, new Size(100, 120));

        // Act
        var editors = ControlTree.FindAll<TextInput>(page);

        // Assert
        editors.Count.ShouldBeGreaterThanOrEqualTo(2);
        ControlTree.Text(page).ShouldContain("Validation wraps beneath the finite field width.");
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
