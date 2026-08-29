// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using SharpVision.Tests.Performance;

/// <summary>Verifies a progressive Table survives a logical extent far larger than anything an
/// eager Table could hold as owned rows, while keeping realized controls and fetch traffic bounded
/// by the viewport rather than the logical count.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TableDataControllerPerformanceTests
{
    private sealed record Item(int Id, string Name);

    private static TableRow BuildRow(Item item) => new([new ControlText(item.Name)]);

    /// <summary>Verifies a 150,000-row known-count source mounts, reports its full logical extent,
    /// and scrolls to its end - all while the realized window and fetch count stay bounded by the
    /// viewport and cache eviction policy instead of the logical count.</summary>
    [Fact]
    public async Task Rewindow_WhenLogicalCountIsVeryLarge_KeepsRealizedControlsAndFetchesBoundedAsync()
    {
        const int total = 150_000;
        var source = new FakeTableDataSource<Item>(
            Enumerable.Range(0, total).Select(static id => new Item(id, $"Row{id}")),
            static item => item.Id,
            total);
        var table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ShowHeader = false,
            ShowGridLines = false
        };
        table.Columns.Add(TableColumn.Fixed("Name", 10));

        await using var surface = await ComponentSurface.MountAsync(
            table, new Size(20, 24), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => table.SetDataSource(source, BuildRow, Length.Cells(1)), "bind a 150k-row source");

        var controller = table.ProgressiveController!;
        controller.LogicalCount.ShouldBe(total);

        // A 24-row viewport realizes at most a small multiple of that many rows, never anything
        // approaching the logical count.
        controller.WindowCount.ShouldBeLessThan(200);
        source.LoadCallCount.ShouldBeLessThan(20);

        // Jumping to the far end and back must still resolve without realizing (or fetching) the
        // whole logical range - only the cache eviction radius around each visited window survives.
        await surface.UpdateAsync(() => table.VerticalOffset = total - 24, "scroll to the very end");
        controller.WindowCount.ShouldBeLessThan(200);

        await surface.UpdateAsync(() => table.VerticalOffset = 0, "scroll back to the start");
        controller.WindowCount.ShouldBeLessThan(200);

        // Total fetch traffic across mount plus two large jumps stays a small constant, never
        // scaling anywhere near the 150k logical rows involved.
        source.LoadCallCount.ShouldBeLessThan(30);
    }
}
