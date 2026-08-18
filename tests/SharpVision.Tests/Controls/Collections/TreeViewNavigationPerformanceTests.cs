// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>
/// Verifies keyboard navigation inside a small expanded branch costs work bounded by the change,
/// not by the total size of a large sibling branch kept collapsed elsewhere in the same tree.
/// </summary>
/// <remarks>
/// Regression coverage for arrow-key navigation walking the entire materialized tree - including
/// collapsed-but-attached branches a caller never asked to see - just to answer an ownership check
/// and a first-selected-in-tree-order lookup that a tree-lifetime cache, refreshed only on a genuine
/// structural change, now answers without a walk.
/// </remarks>
public sealed partial class TreeViewPerformanceTests
{
    private const int _largeBranchSize = 10_000;

    /// <summary>Verifies a single Down-arrow keystroke that moves within a small expanded branch
    /// performs zero full-tree preorder walks, even with a large collapsed sibling branch attached
    /// to the same tree.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyMovesWithinSmallBranch_PerformsNoFullTreeWalkAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var (tree, smallRoot, smallChildA, _) = BuildTreeWithLargeCollapsedSibling();
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            tree.SelectItem(smallRoot);

            // Building and attaching the tree itself pays one legitimate full-tree walk; only the
            // keystroke under test is being measured here.
            tree.OwnedItemsWalkCount = 0;

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down);

            down.IsHandled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(smallChildA);

            // The regression this guards against: CommitSelection, ComputeSelectionDelta, and
            // CollectSelectedItems each independently walked the whole tree - including the large
            // collapsed branch - on every keystroke.
            tree.OwnedItemsWalkCount.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a genuine structural change - expanding the large sibling branch - triggers
    /// exactly one full-tree walk, proving the ownership cache refreshes correctly rather than
    /// silently going stale after the very case the previous test proves it survives untouched.
    /// </summary>
    [Fact]
    public async Task IsExpanded_WhenLargeBranchExpands_TriggersExactlyOneFullTreeWalkAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var (tree, _, _, largeRoot) = BuildTreeWithLargeCollapsedSibling();
            tree.Attach(dispatcher);
            tree.OwnedItemsWalkCount = 0;

            largeRoot.IsExpanded = true;

            tree.OwnedItemsWalkCount.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    private static (TreeView Tree, TreeViewItem SmallRoot, TreeViewItem SmallChildA, TreeViewItem LargeRoot)
        BuildTreeWithLargeCollapsedSibling()
    {
        var tree = new TreeView();
        var smallRoot = new TreeViewItem { Header = "Small", IsExpanded = true };
        var smallChildA = new TreeViewItem { Header = "A" };
        var smallChildB = new TreeViewItem { Header = "B" };
        smallRoot.Children.Add(smallChildA);
        smallRoot.Children.Add(smallChildB);

        var largeRoot = new TreeViewItem { Header = "Large", IsExpanded = false };

        for (var index = 0; index < _largeBranchSize; index++)
        {
            largeRoot.Children.Add(new TreeViewItem { Header = $"Node{index}" });
        }

        tree.Items.Add(smallRoot);
        tree.Items.Add(largeRoot);

        return (tree, smallRoot, smallChildA, largeRoot);
    }
}
