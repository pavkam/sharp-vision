// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies layered, popup, and window showcase recipes.</summary>
public sealed class LayerPaneTests
{
    /// <summary>Verifies documentation never opens promoted popup surfaces before user activation.</summary>
    [Fact]
    public void Popup_WhenPageBuilds_AllSurfacesStartClosed()
    {
        using var page = new PopupPane();
        new Engine().Layout(page, new Size(100, 180));

        var popups = ControlTree.FindAll<Popup>(page);

        popups.Count.ShouldBeGreaterThan(1);
        popups.ShouldAllBe(popup => !popup.IsOpen);
        popups.ShouldAllBe(popup => popup.SurfaceBounds == default);
    }

    /// <summary>Verifies every Popup trigger keeps its intrinsic height inside the live specimen.</summary>
    [Fact]
    public void Popup_WhenPageLaysOut_AnchorsRemainContentSized()
    {
        // Arrange
        using var page = new PopupPane();

        // Act
        new Engine().Layout(page, new Size(100, 180));
        var anchors = ControlTree.FindAll<Popup>(page)
            .Select(popup => popup.Anchor.ShouldNotBeNull())
            .ToArray();

        // Assert
        anchors.ShouldAllBe(anchor => anchor.Bounds.Height == anchor.DesiredSize.Height);
    }

    /// <summary>Verifies open Popup surfaces stay inside their specimens without covering an action.</summary>
    [Fact]
    public void Popup_WhenEachSurfaceOpens_StaysInsideItsStageWithoutCoveringButtons()
    {
        // Arrange
        using var page = new PopupPane();
        var engine = new Engine();
        var size = new Size(100, 180);
        engine.Layout(page, size);
        var popups = ControlTree.FindAll<Popup>(page);
        var buttons = ControlTree.FindAll<Button>(page);

        foreach (var popup in popups)
        {
            // Act
            popup.IsOpen = true;
            engine.Layout(page, size);

            // Assert
            popup.SurfaceBounds.ShouldNotBe(default);
            popup.SurfaceBounds.Intersect(popup.Parent.ShouldNotBeNull().Bounds)
                .ShouldBe(popup.SurfaceBounds);
            foreach (var button in buttons)
            {
                var overlap = popup.SurfaceBounds.Intersect(button.Bounds);
                (overlap.Width == 0 || overlap.Height == 0).ShouldBeTrue();
            }

            popup.IsOpen = false;
            engine.Layout(page, size);
        }
    }

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

    /// <summary>Verifies example surfaces use bounded square frames with a consistent title.</summary>
    [Fact]
    public void Overlay_WhenPageIsWide_BoundsAndTitlesEveryExampleSurface()
    {
        // Arrange
        using var page = new OverlayPane();

        // Act
        new Engine().Layout(page, new Size(200, 120));
        var surfaces = ControlTree.FindAll<GroupBox>(page)
            .Where(value =>
                value.Header == "Example" &&
                value.Background == ColorRole.Surface &&
                value.BorderThickness == new Thickness(1) &&
                value.Padding == new Thickness(1))
            .ToArray();

        // Assert
        surfaces.Length.ShouldBe(7);
        surfaces.ShouldAllBe(surface => surface.MaxWidth == 100);
        surfaces.ShouldAllBe(surface => surface.Bounds.Width <= 100);
        surfaces.ShouldAllBe(surface => surface.Glyphs == Glyphs.Light);
    }

    /// <summary>Verifies Popup reports Closing before Closed through its public lifecycle.</summary>
    [Fact]
    public void Popup_WhenLifecycleCloseRuns_ReportsOrderedEvents()
    {
        // Arrange
        using var page = new PopupPane();
        new Engine().Layout(page, new Size(100, 140));
        var trigger = FindButton(page, "Show lifecycle popup");

        // Act
        trigger.PerformClick();
        trigger.PerformClick();

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
            value.Content is ControlText { Content: "Placement preview" });
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
    public void Window_WhenBoundaryExamplesBuild_ContainsShadowAndTitleVariants()
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
        windows.Where(value => value.Visibility == Visibility.Visible)
            .ShouldNotContain(value => value.Bounds.Width <= 2);
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
            value.Content is ControlText { Content: "Placement preview" });
        var popupBackdrop = ControlTree.FindAll<ControlText>(popupPage).Single(value =>
            value.Content.Contains("Files", StringComparison.Ordinal) &&
            value.Content.Contains("Ready", StringComparison.Ordinal));
        var window = ControlTree.FindAll<Window>(windowPage).Single(value => value.Title == "Draggable settings");
        var windowBackdrop = ControlTree.FindAll<ControlText>(windowPage).Single(value =>
            value.Content.Contains("Dashboard", StringComparison.Ordinal) &&
            value.Content.Contains("Drag the window", StringComparison.Ordinal));

        FindButton(popupPage, "Below").PerformClick();
        engine.Layout(popupPage, new Size(100, 180));

        popup.SurfaceBounds.Intersect(popupBackdrop.Parent.ShouldNotBeNull().Bounds).Width.ShouldBeGreaterThan(0);
        popup.SurfaceBounds.Intersect(popupBackdrop.Parent.Bounds).Height.ShouldBeGreaterThan(0);
        window.Bounds.Intersect(windowBackdrop.Parent.ShouldNotBeNull().Bounds).Width.ShouldBeGreaterThan(0);
        window.Bounds.Intersect(windowBackdrop.Parent.Bounds).Height.ShouldBeGreaterThan(0);
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
