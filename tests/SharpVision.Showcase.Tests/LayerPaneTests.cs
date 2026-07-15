// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies layered, popup, and window showcase recipes.</summary>
public sealed class LayerPaneTests
{
    /// <summary>Verifies decorative Overlay content can render without intercepting pointer input.</summary>
    [Fact]
    public void Overlay_WhenPointerTransparencyBuilds_ContainsTransparentDecoration()
    {
        // Arrange
        using var page = new OverlayPane();
        new Engine().Layout(page, new Size(100, 120));

        // Act
        var decoration = ControlTree.FindAll<ControlText>(page).Single(value =>
            value.Content == "Decorative overlay");

        // Assert
        decoration.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies Popup reports Closing before Closed through its public lifecycle.</summary>
    [Fact]
    public void Popup_WhenLifecycleCloseRuns_ReportsOrderedEvents()
    {
        // Arrange
        using var page = new PopupPane();
        new Engine().Layout(page, new Size(100, 140));
        var close = FindButton(page, "Close lifecycle popup");

        // Act
        close.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Lifecycle: Closing → Closed");
    }

    /// <summary>Verifies four placement actions move one open Popup around one central anchor.</summary>
    [Theory]
    [InlineData("Above", PopupPlacement.Above)]
    [InlineData("Below", PopupPlacement.Below)]
    [InlineData("Left", PopupPlacement.Left)]
    [InlineData("Right", PopupPlacement.Right)]
    public void Popup_WhenPlacementActionRuns_MovesSamePopupAroundAnchor(
        string action,
        PopupPlacement placement)
    {
        using var page = new PopupPane();
        var engine = new Engine();
        var size = new Size(100, 160);
        engine.Layout(page, size);
        var anchor = FindButton(page, "Preview anchor");
        var popup = ControlTree.FindAll<Popup>(page).Single(value =>
            value.Child is ControlText { Content: "Placement preview" });
        var trigger = FindButton(page, action);

        trigger.PerformClick();
        engine.Layout(page, size);

        popup.Placement.ShouldBe(placement);
        popup.IsOpen.ShouldBeTrue();
        ControlTree.Text(page).ShouldContain($"Requested side: {action}");

        switch (placement)
        {
            case PopupPlacement.Above:
                popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(anchor.Bounds.Y);
                break;
            case PopupPlacement.Below:
                popup.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(anchor.Bounds.Bottom);
                break;
            case PopupPlacement.Left:
                popup.SurfaceBounds.Right.ShouldBeLessThanOrEqualTo(anchor.Bounds.X);
                break;
            case PopupPlacement.Right:
                popup.SurfaceBounds.X.ShouldBeGreaterThanOrEqualTo(anchor.Bounds.Right);
                break;
            default:
                throw new InvalidOperationException("The test placement is unknown.");
        }
    }

    /// <summary>Verifies Window shows both shadow modes and a safe long-title boundary specimen.</summary>
    [Fact]
    public void Window_WhenBoundaryExamplesBuild_ContainsShadowAndLongTitleVariants()
    {
        // Arrange
        using var page = new WindowPane();
        new Engine().Layout(page, new Size(120, 160));

        // Act
        var windows = ControlTree.FindAll<Window>(page);

        // Assert
        windows.ShouldContain(value => value.ShadowMode == ShadowMode.Composite && value.HasShadow);
        windows.ShouldContain(value => value.ShadowMode == ShadowMode.BlockGlyph && value.HasShadow);
        windows.ShouldContain(value => !value.HasShadow);
        windows.ShouldContain(value => value.Title.StartsWith("A deliberately long title", StringComparison.Ordinal));
    }

    /// <summary>Verifies primary Popup and Window demonstrations overlap populated application surfaces.</summary>
    [Fact]
    public void FloatingExamples_WhenBuilt_OverlapPopulatedBackgroundStages()
    {
        using var popupPage = new PopupPane();
        using var windowPage = new WindowPane();
        var engine = new Engine();
        engine.Layout(popupPage, new Size(100, 180));
        engine.Layout(windowPage, new Size(120, 180));
        var popup = ControlTree.FindAll<Popup>(popupPage).Single(value =>
            value.Child is ControlText { Content: "Placement preview" });
        var popupBackdrop = ControlTree.FindAll<ControlText>(popupPage).Single(value =>
            value.Content.Contains("Files", StringComparison.Ordinal) &&
            value.Content.Contains("Ready", StringComparison.Ordinal));
        var window = ControlTree.FindAll<Window>(windowPage).Single(value => value.Title == "Project settings");
        var windowBackdrop = ControlTree.FindAll<ControlText>(windowPage).Single(value =>
            value.Content.Contains("Workspace", StringComparison.Ordinal) &&
            value.Content.Contains("2 tasks", StringComparison.Ordinal));

        popup.SurfaceBounds.Intersect(popupBackdrop.Parent.ShouldNotBeNull().Bounds).Width.ShouldBeGreaterThan(0);
        popup.SurfaceBounds.Intersect(popupBackdrop.Parent.Bounds).Height.ShouldBeGreaterThan(0);
        window.Bounds.Intersect(windowBackdrop.Parent.ShouldNotBeNull().Bounds).Width.ShouldBeGreaterThan(0);
        window.Bounds.Intersect(windowBackdrop.Parent.Bounds).Height.ShouldBeGreaterThan(0);
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
