// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates SetSort tie-break cost against row count.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TablePerformanceTests
{
    /// <summary>Verifies sorting rows that all tie on the sort key scales with row count rather than
    /// its square — the tie-break previously resolved each row's original position with an O(n)
    /// `_sourceRows.IndexOf` scan, so every SetSort call cost O(n^2) on top of the O(n log n) sort
    /// itself (see #118).</summary>
    [Fact]
    public void SortBy_WhenEveryRowTiesOnTheSortKey_ScalesLinearlyWithRowCount()
    {
        var small = Elapsed(rowCount: 200);
        var large = Elapsed(rowCount: 3_200);

        // A quadratic tie-break would grow by ~256x (16^2) for a 16x larger row count; an O(n log n)
        // sort with an O(1) tie-break grows by roughly 16x*log-factor. A 50x budget comfortably
        // clears that noise while still rejecting the O(n^2) shape well short of 256x.
        large.TotalMilliseconds.ShouldBeLessThan((small.TotalMilliseconds * 50) + 5);
    }

    private static TimeSpan Elapsed(int rowCount)
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Key"));

        for (var index = 0; index < rowCount; index++)
        {
            table.Rows.Add(new TableRow([new ControlText("same")]));
        }

        // The first call settles a stable order and pays the one-time O(n) ReorderRows/Rows.Add
        // population churn; repeated calls with an unchanged tie-break result short-circuit
        // ReorderRows entirely (Rows already equals the freshly computed order), isolating the
        // sort/tie-break cost this test targets from that unrelated per-row attachment overhead.
        table.SetSort(0, TableSortDirection.Ascending);

        var watch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < 20; iteration++)
        {
            table.SetSort(0, TableSortDirection.Ascending);
        }

        watch.Stop();
        return watch.Elapsed;
    }
}
