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

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);
}
