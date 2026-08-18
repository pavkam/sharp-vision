// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies that a huge logical hierarchy reachable only through asynchronous on-demand
/// child loading keeps materialized work proportional to the expanded frontier, not the full
/// logical size a caller never asked to load.</summary>
public sealed partial class TreeViewPerformanceTests
{
    /// <summary>A child source whose logical hierarchy is effectively unbounded - each node reports
    /// a fixed fan-out - but only ever answers the one request it receives, so materialized work
    /// stays proportional to what is actually expanded rather than the logical hierarchy size.</summary>
    private sealed class HugeFanOutChildSource: ITreeViewChildSource
    {
        private const int _fanOut = 50;

        public Task<IReadOnlyList<TreeViewChildDescription>> GetChildrenAsync(
            TreeViewChildContext context,
            CancellationToken cancellationToken)
        {
            var parentKey = (string?) context.Key ?? string.Empty;
            IReadOnlyList<TreeViewChildDescription> children = [.. Enumerable.Range(0, _fanOut)
                .Select(index => new TreeViewChildDescription($"{parentKey}/{index}", $"Node {index}"))];

            return Task.FromResult(children);
        }
    }

    /// <summary>Verifies expanding one root against an effectively unbounded logical hierarchy
    /// materializes only that root's immediate frontier - the fan-out of one level - never the
    /// deeper hierarchy no descendant ever asked to load.</summary>
    [Fact]
    public async Task Expanded_WhenHierarchyIsEffectivelyUnbounded_MaterializesOnlyTheExpandedFrontierAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new HugeFanOutChildSource();
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
            root.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        // Only the root's own 50-node frontier materialized - not one grandchild, despite every
        // materialized node itself carrying a ChildSource capable of answering another 50.
        root.Children.Count.ShouldBe(50);

        foreach (var child in root.Children)
        {
            child.ChildState.ShouldBe(TreeViewChildState.Unloaded, "an unexpanded child must never have requested its own children");
        }
    }
}
