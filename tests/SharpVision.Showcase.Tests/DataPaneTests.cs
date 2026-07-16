// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies menu, range, and tabular showcase recipes.</summary>
public sealed class DataPaneTests
{
    /// <summary>Verifies Menu demonstrates conventional Popup composition.</summary>
    [Fact]
    public void Menu_WhenPopupRecipeBuilds_ContainsPopupWithMenuChild()
    {
        // Arrange
        using var page = new MenuPane();
        new Engine().Layout(page, new Size(100, 120));

        // Act
        var popup = ControlTree.FindAll<Popup>(page).Single();

        // Assert
        _ = popup.Content.ShouldBeOfType<Menu>();
    }

    /// <summary>Verifies ScrollBar live geometry updates without replacing the control.</summary>
    [Fact]
    public void ScrollBar_WhenViewportActionRuns_UpdatesViewportAndStatus()
    {
        // Arrange
        using var page = new ScrollBarPane();
        new Engine().Layout(page, new Size(100, 120));
        var scrollBar = ControlTree.FindAll<ScrollBar>(page).Single(value =>
            value.Maximum == 100 && value.ViewportSize == 20);
        var increase = FindButton(page, "Increase viewport");

        // Act
        increase.PerformClick();

        // Assert
        scrollBar.ViewportSize.ShouldBe(30);
        ControlTree.Text(page).ShouldContain("Range: 0..100, value 20, viewport 30");
    }

    /// <summary>Verifies Table dynamically accepts a newly detached row.</summary>
    [Fact]
    public void Table_WhenAddRowActionRuns_OwnsNewInteractiveRow()
    {
        // Arrange
        using var page = new TablePane();
        new Engine().Layout(page, new Size(100, 140));
        var table = ControlTree.FindAll<Table>(page).Single(value =>
            value.Columns.Count == 2 &&
            value.Rows.Count == 1 &&
            value.Columns[0].Header == "Release");
        var add = FindButton(page, "Add release row");

        // Act
        add.PerformClick();

        // Assert
        table.Rows.Count.ShouldBe(2);
        ControlTree.Text(page).ShouldContain("Rows: 2");
    }

    /// <summary>Verifies interactive table cells remain intrinsic and the shortcut reference needs no horizontal rail.</summary>
    [Fact]
    public void Table_WhenCompactExamplesBuild_ContainsIntrinsicOptionAndVisibleShortcuts()
    {
        using var page = new TablePane();
        new Engine().Layout(page, new Size(100, 160));
        var option = ControlTree.FindAll<CheckBox>(page).Single(value =>
            value.Content is ControlText { Content: "Include integration tests" });
        var shortcuts = ControlTree.FindAll<Table>(page).Single(value =>
            !value.ShowHeader && value.Rows.Count == 4);

        option.Bounds.Width.ShouldBe(option.DesiredSize.Width);
        option.Bounds.Height.ShouldBe(option.DesiredSize.Height);
        shortcuts.Extent.Width.ShouldBeLessThanOrEqualTo(shortcuts.Viewport.Width);
        ControlTree.Text(shortcuts).ShouldContain("Ctrl+S");
        ControlTree.Text(shortcuts).ShouldContain("Open shortcut guide");
    }

    /// <summary>Verifies the dedicated overflow specimen deliberately exposes both scrolling axes.</summary>
    [Fact]
    public void Table_WhenScrollableExampleBuilds_OverflowsBothAxesDeliberately()
    {
        using var page = new TablePane();
        new Engine().Layout(page, new Size(100, 180));
        var scrolling = ControlTree.FindAll<Table>(page).Single(value =>
            value.Columns.Count == 3 && value.Rows.Count == 10);

        scrolling.Extent.Width.ShouldBeGreaterThan(scrolling.Viewport.Width);
        scrolling.Extent.Height.ShouldBeGreaterThan(scrolling.Viewport.Height);
        scrolling.HorizontalOffset = 3;
        scrolling.VerticalOffset = 2;

        Should.NotThrow(() => new Engine().Layout(page, new Size(100, 180)));
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
