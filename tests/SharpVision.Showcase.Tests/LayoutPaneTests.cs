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

    /// <summary>Verifies the auto/star specimen gives visible interiors to a committed two-to-one remainder.</summary>
    [Fact]
    public void Grid_WhenAutoAndStarExampleBuilds_ShowsTwoToOneTrackAllocation()
    {
        using var page = new GridPane();
        new Engine().Layout(page, new Size(100, 140));
        var twoStar = FindCard(page, "2* = 6 rows");
        var oneStar = FindCard(page, "1* = 3 rows");

        twoStar.Bounds.Height.ShouldBe(oneStar.Bounds.Height * 2);
        twoStar.Bounds.Height.ShouldBeGreaterThan(2);
        oneStar.Bounds.Height.ShouldBeGreaterThan(2);
        twoStar.FillMode.ShouldBe(FillMode.Opaque);
        oneStar.FillMode.ShouldBe(FillMode.Opaque);
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);

    private static Dock FindCard(Control root, string content) =>
        ControlTree.FindAll<Dock>(root).Single(value =>
            value.Children.Count == 1 &&
            value.Children[0] is ControlText text &&
            text.Content == content);
}
