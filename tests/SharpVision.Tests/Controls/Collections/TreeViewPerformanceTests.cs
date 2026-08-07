// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using SharpVision.Tests.Performance;

/// <summary>
/// Verifies TreeView survives caller-controlled hierarchy depth and keeps incremental construction
/// affordable.
/// </summary>
/// <remarks>
/// Depth here is a valid public input, so exhausting the process stack is not an acceptable
/// failure: <c>StackOverflowException</c> cannot be caught, so a regression would terminate the
/// host rather than fail a test. Each depth case therefore stays well above the recursion limit the
/// previous implementation had.
/// </remarks>
[Collection(PerformanceGroup.Name)]
public sealed class TreeViewPerformanceTests
{
    /// <summary>Depth for paths that walk the hierarchy without materializing visual children.</summary>
    private const int _deep = 20_000;

    /// <summary>
    /// Depth for paths that expand every node. Flattening an expanded chain necessarily realizes
    /// one visual child per node, so these stay far below <see cref="_deep"/> to keep the suite
    /// affordable while still exceeding any plausible recursion budget.
    /// </summary>
    private const int _deepVisible = 1_500;

    /// <summary>Verifies attaching a deep prebuilt chain to an owned root does not exhaust the stack.</summary>
    [Fact]
    public void Add_WhenPrebuiltChainIsDeep_PropagatesOwnershipWithoutRecursion()
    {
        var tree = new TreeView();
        var (root, leaf) = Chain(_deep);

        tree.Items.Add(root);

        // Ownership must reach the deepest node, which is exactly what the recursive propagation
        // could not survive.
        leaf.Children.Owner.ShouldBeSameAs(tree);
        tree.Items.ShouldHaveSingleItem().ShouldBeSameAs(root);
    }

    /// <summary>Verifies cycle detection over a deep hierarchy stays iterative.</summary>
    [Fact]
    public void Add_WhenCandidateIsDeepDescendant_RejectsWithoutRecursion()
    {
        var tree = new TreeView();
        var (root, leaf) = Chain(_deep);
        tree.Items.Add(root);

        _ = Should.Throw<InvalidOperationException>(() => leaf.Children.Add(root));
    }

    /// <summary>Verifies expanding and flattening a deep chain stays iterative.</summary>
    [Fact]
    public void ExpandAll_WhenHierarchyIsDeep_FlattensWithoutRecursion()
    {
        var tree = new TreeView();
        var (root, leaf) = Chain(_deepVisible);
        tree.Items.Add(root);

        tree.ExpandAll();

        root.Expanded.ShouldBeTrue();
        _ = leaf.Parent.ShouldNotBeNull();

        tree.CollapseAll();

        root.Expanded.ShouldBeFalse();
    }

    /// <summary>Verifies selection repair over a deep expanded chain stays iterative.</summary>
    [Fact]
    public void SelectItem_WhenHierarchyIsDeep_RepairsSelectionWithoutRecursion()
    {
        var tree = new TreeView();
        var (root, leaf) = Chain(_deepVisible);
        tree.Items.Add(root);
        tree.ExpandAll();

        tree.SelectItem(leaf);

        tree.SelectedItem.ShouldBeSameAs(leaf);
        _ = tree.Items.Remove(root);
        tree.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies three-state propagation and aggregation over a deep chain stay iterative.</summary>
    [Fact]
    public void IsChecked_WhenCheckableChainIsDeep_PropagatesWithoutRecursion()
    {
        var tree = new TreeView();
        var (root, leaf) = Chain(_deep, checkable: true);
        tree.Items.Add(root);

        root.IsChecked = true;

        leaf.IsChecked.ShouldBe(true);
        root.IsChecked.ShouldBe(true);

        leaf.IsChecked = false;

        // Every level of a chain has exactly one checkable child, so the cleared leaf propagates
        // upward as a definite false rather than a mixed indeterminate.
        root.IsChecked.ShouldBe(false);
    }

    /// <summary>
    /// Verifies incremental construction stays affordable. Sequential adds previously rebuilt the
    /// whole visible presentation per mutation, so allocation grew several-fold on every doubling.
    /// </summary>
    /// <param name="nodes">The number of nested items added one at a time.</param>
    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void Items_WhenAddedSequentially_StaysWithinAllocationBudget(int nodes)
    {
        var tree = new TreeView();
        Warm(tree);
        // Thread-local, so a parallel test cannot pollute the measurement.
        var before = GC.GetAllocatedBytesForCurrentThread();

        var current = tree.Items;

        for (var index = 0; index < nodes; index++)
        {
            var item = new TreeViewItem { Header = "n", Expanded = true };
            current.Add(item);
            current = item.Children;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The previous implementation measured 291 MB at 100 nodes and 2.05 GB at 200. A linear
        // ceiling with generous headroom fails that shape without pinning an exact figure.
        allocated.ShouldBeLessThan(nodes * 200L * 1024L);
    }

    private static void Warm(TreeView tree)
    {
        var warm = new TreeViewItem { Header = "warm" };
        tree.Items.Add(warm);
        _ = tree.Items.Remove(warm);
    }

    private static (TreeViewItem Root, TreeViewItem Leaf) Chain(
        int depth,
        bool checkable = false,
        bool expanded = false)
    {
        // Items expand by default, which would realize one visual child per node the moment the
        // chain is owned. Building collapsed keeps the structural cases measuring traversal rather
        // than presentation.
        var root = new TreeViewItem { Header = "0", Checkable = checkable, Expanded = expanded };
        var current = root;

        for (var index = 1; index < depth; index++)
        {
            var child = new TreeViewItem { Header = "n", Checkable = checkable, Expanded = expanded };
            current.Children.Add(child);
            current = child;
        }

        return (root, current);
    }
}
