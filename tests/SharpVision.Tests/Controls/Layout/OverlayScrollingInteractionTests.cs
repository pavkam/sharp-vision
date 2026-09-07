// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a mounted AutoScroll Overlay rebases a Percent-width positioned child against
/// the visible Viewport instead of the scroll-inflated Extent, mirroring Grid, Stack, and Dock.
/// </summary>
public sealed class OverlayScrollingInteractionTests
{
    /// <summary>Verifies a Left-anchored Percent-width child resolves against the viewport, not
    /// the extent an unrelated wider, unpositioned sibling inflates: 50% of an 8-cell viewport is
    /// 4 cells, not 50% of the 20-cell scroll-inflated extent.</summary>
    [Fact]
    public async Task Layout_WhenAutoScrollOverlayHasPercentChild_ResolvesAgainstViewportNotExtentAsync()
    {
        // Arrange
        var wide = new ControlText("W") { Width = Length.Cells(20) };
        var percentChild = new ControlText("P") { Width = Length.Percent(50) };
        Overlay.SetLeft(percentChild, Length.Cells(0));
        var overlay = new Overlay
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { wide, percentChild }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        overlay.Viewport.Width.ShouldBe(8);
        overlay.Extent.Width.ShouldBe(20);
        percentChild.Bounds.Width.ShouldBe(4);
    }
}
