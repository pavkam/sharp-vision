// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a mounted Table with horizontal scrolling armed rebases a Percent column
/// (through its private TablePresenter) against the visible Viewport instead of the scroll-
/// inflated Extent, mirroring Grid, Stack, Dock, and Overlay.</summary>
public sealed class TableScrollingInteractionTests
{
    /// <summary>Verifies a Percent column resolves against the viewport, not the extent a wider
    /// fixed sibling column inflates: 50% of an 8-cell viewport is 4 cells, so the two columns
    /// settle at a 24-cell extent instead of shrinking or inflating around a stale width.</summary>
    [Fact]
    public async Task Layout_WhenHorizontallyScrollingTableHasPercentColumn_ResolvesAgainstViewportNotExtentAsync()
    {
        // Arrange
        var table = new Table
        {
            ShowHeader = false,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Wide", 20));
        table.Columns.Add(TableColumn.Percent("Pct", 50));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            table,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        table.Viewport.Width.ShouldBe(8);
        table.Extent.Width.ShouldBe(24);
    }
}
