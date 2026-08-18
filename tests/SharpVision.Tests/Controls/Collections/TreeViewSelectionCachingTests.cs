// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>
/// Verifies selection state, <see cref="TreeView.SelectedItem"/>, and the Added/Removed delta stay
/// exactly correct once selection bookkeeping reads a tree-lifetime ownership cache instead of
/// re-walking the tree - each case attaches a sizeable, deliberately collapsed sibling branch that
/// contributes nothing to the operation under test, to prove the cache's presence never corrupts an
/// unrelated result.
/// </summary>
public sealed partial class TreeViewTests
{
    private const int _decoyBranchSize = 200;

    /// <summary>Verifies a multi-select snapshot reports items in stable tree order regardless of
    /// the order they were selected in, with a large collapsed sibling branch attached.</summary>
    [Fact]
    public void SelectedItems_WhenSelectedOutOfOrderWithLargeCollapsedSibling_ReturnsThemInTreeOrder()
    {
        var (tree, root, childA, childB, _) = BuildTreeWithDecoyBranch();
        tree.SelectionMode = TreeSelectionMode.Multiple;

        _ = tree.SetSelected(childB, true);
        _ = tree.SetSelected(root, true);
        _ = tree.SetSelected(childA, true);

        tree.SelectedItems.ShouldBe([root, childA, childB]);
    }

    /// <summary>Verifies single-select keyboard navigation reports exactly the one-item swap on
    /// every keystroke, with a large collapsed sibling branch attached that the moves never touch.
    /// </summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyNavigatesWithLargeCollapsedSiblingPresent_ReportsSingleItemDeltaAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var (tree, root, childA, childB, _) = BuildTreeWithDecoyBranch();
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();
            tree.SelectItem(root);

            TreeViewSelectionChangedEventArgs? observed = null;
            tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

            var down1 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down1);

            _ = observed.ShouldNotBeNull();
            observed.AddedItems.ShouldBe([childA]);
            observed.RemovedItems.ShouldBe([root]);
            tree.SelectedItem.ShouldBeSameAs(childA);

            observed = null;
            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);

            _ = observed.ShouldNotBeNull();
            observed.AddedItems.ShouldBe([childB]);
            observed.RemovedItems.ShouldBe([childA]);
            tree.SelectedItem.ShouldBeSameAs(childB);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detaching one of two selected items reports exactly that item as removed
    /// and leaves the other as <see cref="TreeView.SelectedItem"/>, with a large collapsed sibling
    /// branch attached that the removal never touches.</summary>
    [Fact]
    public void Items_WhenSelectedItemDetachedWithLargeCollapsedSiblingPresent_ReportsExactRemovedDelta()
    {
        var (tree, root, childA, childB, _) = BuildTreeWithDecoyBranch();
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(childA, true);
        _ = tree.SetSelected(childB, true);

        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        _ = root.Children.Remove(childA);

        _ = observed.ShouldNotBeNull();
        observed.RemovedItems.ShouldBe([childA]);
        observed.AddedItems.ShouldBeEmpty();
        tree.SelectedItems.ShouldBe([childB]);
        tree.SelectedItem.ShouldBeSameAs(childB);
    }

    /// <summary>Verifies SelectAll reaches every owned item, including the descendants of a
    /// collapsed branch that never became a visible row - the invariant the ownership cache exists
    /// to preserve, since the visible-row buffer alone structurally excludes them.</summary>
    [Fact]
    public void SelectAll_WhenBranchIsCollapsed_SelectsItsDescendantsToo()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var collapsedRoot = new TreeViewItem { Header = "Collapsed", IsExpanded = false };
        var hiddenChildA = new TreeViewItem { Header = "HiddenA" };
        var hiddenChildB = new TreeViewItem { Header = "HiddenB" };
        collapsedRoot.Children.Add(hiddenChildA);
        collapsedRoot.Children.Add(hiddenChildB);
        tree.Items.Add(collapsedRoot);

        tree.SelectAll();

        hiddenChildA.IsSelected.ShouldBeTrue();
        hiddenChildB.IsSelected.ShouldBeTrue();
        tree.SelectedItems.ShouldBe([collapsedRoot, hiddenChildA, hiddenChildB]);
    }

    /// <summary>Verifies SelectAll called between BeginUpdate and EndUpdate reaches an item added
    /// earlier in the same batch - the ownership cache is deliberately left unrefreshed until the
    /// batch's structural rebuild, so a reader consulting it mid-batch must catch up on demand
    /// instead of filtering out every item added since BeginUpdate as not-yet-owned.</summary>
    [Fact]
    public void SelectAll_WhenCalledMidBatchAfterAnAddition_SelectsTheItemAddedDuringTheBatch()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var root = new TreeViewItem { Header = "Root", IsExpanded = true };
        tree.Items.Add(root);

        tree.BeginUpdate();

        try
        {
            var addedDuringBatch = new TreeViewItem { Header = "AddedDuringBatch" };
            root.Children.Add(addedDuringBatch);

            tree.SelectAll();

            addedDuringBatch.IsSelected.ShouldBeTrue();
            tree.SelectedItems.ShouldBe([root, addedDuringBatch]);
        }
        finally
        {
            tree.EndUpdate();
        }
    }

    /// <summary>Verifies SetSelected called between BeginUpdate and EndUpdate accepts an item added
    /// earlier in the same batch, mirroring <see cref="SelectAll_WhenCalledMidBatchAfterAnAddition_SelectsTheItemAddedDuringTheBatch"/>
    /// for the single-item selection path.</summary>
    [Fact]
    public void SetSelected_WhenCalledMidBatchOnAnItemAddedDuringTheBatch_SelectsIt()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var root = new TreeViewItem { Header = "Root", IsExpanded = true };
        tree.Items.Add(root);

        tree.BeginUpdate();

        try
        {
            var addedDuringBatch = new TreeViewItem { Header = "AddedDuringBatch" };
            root.Children.Add(addedDuringBatch);

            var result = tree.SetSelected(addedDuringBatch, true);

            result.ShouldBeTrue();
            addedDuringBatch.IsSelected.ShouldBeTrue();
            tree.SelectedItems.ShouldBe([addedDuringBatch]);
        }
        finally
        {
            tree.EndUpdate();
        }
    }

    private static (TreeView Tree, TreeViewItem Root, TreeViewItem ChildA, TreeViewItem ChildB, TreeViewItem DecoyRoot)
        BuildTreeWithDecoyBranch()
    {
        var tree = new TreeView();
        var root = new TreeViewItem { Header = "Root", IsExpanded = true };
        var childA = new TreeViewItem { Header = "A" };
        var childB = new TreeViewItem { Header = "B" };
        root.Children.Add(childA);
        root.Children.Add(childB);

        var decoyRoot = new TreeViewItem { Header = "Decoy", IsExpanded = false };

        for (var index = 0; index < _decoyBranchSize; index++)
        {
            decoyRoot.Children.Add(new TreeViewItem { Header = $"Decoy{index}" });
        }

        tree.Items.Add(root);
        tree.Items.Add(decoyRoot);

        return (tree, root, childA, childB, decoyRoot);
    }
}
