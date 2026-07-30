// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates the cost of populating a TreeView with many items.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TreeViewPerformanceTests
{
    /// <summary>Verifies populating a tree inside BeginUpdate/EndUpdate scales with item count
    /// rather than its square — every individual Items.Add otherwise triggers its own complete
    /// flat-list rebuild, so populating an n-item tree one item at a time costs O(n^2) instead of
    /// O(n) (see #47).</summary>
    [Fact]
    public void BeginUpdate_WhenPopulatingManyItems_ScalesLinearlyWithItemCount()
    {
        var small = Elapsed(itemCount: 500);
        var large = Elapsed(itemCount: 4_000);

        // A quadratic per-item rebuild would grow ~64x for 8x more items; a single deferred
        // rebuild should stay close to linear (~8x). A generous 24x budget comfortably clears
        // linear noise while still rejecting the O(n^2) shape well short of 64x.
        large.TotalMilliseconds.ShouldBeLessThan((small.TotalMilliseconds * 24) + 20);
    }

    private static TimeSpan Elapsed(int itemCount)
    {
        var tree = new TreeView();
        var watch = Stopwatch.StartNew();

        tree.BeginUpdate();

        for (var index = 0; index < itemCount; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        tree.EndUpdate();
        watch.Stop();
        return watch.Elapsed;
    }
}
