// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a mounted AutoScroll Dock rebases a Percent or Star edge length against the
/// visible Viewport instead of the scroll-inflated Extent, mirroring Grid and Stack.</summary>
public sealed class DockScrollingInteractionTests
{
    // A Top-docked child with a fixed Width never consumes the horizontal sequential axis (Top
    // only competes on the vertical axis), but its own measured DesiredSize.Width still inflates
    // the Dock's overall Extent - exactly the "unrelated wider sibling" shape needed to make
    // Extent exceed Viewport without the fixed and Percent/Star Left edges alone accounting for it.
    private static ControlText WideTop()
    {
        var wide = new ControlText("T") { Width = Length.Cells(20) };
        Dock.SetSide(wide, DockSide.Top);
        return wide;
    }

    private static ControlText FixedLeftEdge()
    {
        var fixedEdge = new ControlText("F") { Width = Length.Cells(2) };
        Dock.SetSide(fixedEdge, DockSide.Left);
        return fixedEdge;
    }

    /// <summary>Verifies a Percent-width Left edge resolves against the viewport, not the extent
    /// an unrelated wider sibling inflates: 50% of an 8-cell viewport is 4 cells, not 50% of the
    /// larger sequential leftover a 20-cell extent would otherwise leave.</summary>
    [Fact]
    public async Task Layout_WhenAutoScrollDockHasPercentEdge_ResolvesAgainstViewportNotExtentAsync()
    {
        // Arrange
        var percentEdge = new ControlText("P") { Width = Length.Percent(50) };
        Dock.SetSide(percentEdge, DockSide.Left);
        var dock = new Dock
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            LastChildFills = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { WideTop(), FixedLeftEdge(), percentEdge }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        dock.Viewport.Width.ShouldBe(8);
        dock.Extent.Width.ShouldBe(20);
        percentEdge.Bounds.Width.ShouldBe(4);
    }

    /// <summary>Verifies a Star-weighted Left edge divides the viewport's own leftover space, not
    /// the scroll-inflated extent's leftover, once a fixed sibling has consumed part of the axis:
    /// with an 8-cell viewport and a 2-cell fixed sibling, the sole Star claims the remaining 8
    /// viewport cells rather than the 18-cell sequential leftover a 20-cell extent would leave.
    /// </summary>
    [Fact]
    public async Task Layout_WhenAutoScrollDockHasStarEdge_ResolvesAgainstViewportNotExtentAsync()
    {
        // Arrange
        var starEdge = new ControlText("S") { Width = Length.Star(1) };
        Dock.SetSide(starEdge, DockSide.Left);
        var dock = new Dock
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            LastChildFills = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { WideTop(), FixedLeftEdge(), starEdge }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        dock.Viewport.Width.ShouldBe(8);
        dock.Extent.Width.ShouldBe(20);
        starEdge.Bounds.Width.ShouldBe(8);
    }
}
