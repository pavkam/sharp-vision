// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>
/// Verifies TreeView reports which items a transition selected and deselected, and permits
/// programmatic single-item selection changes.
/// </summary>
public sealed class TreeViewSelectionDeltaTests
{
    /// <summary>Verifies adding one item to a multiple selection reports only that addition.</summary>
    [Fact]
    public void SetSelected_WhenAddingToMultipleSelection_ReportsOnlyTheAddition()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(first, true);
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        var changed = tree.SetSelected(second, true);

        changed.ShouldBeTrue();
        tree.SelectedItems.ShouldBe([first, second]);
        _ = observed.ShouldNotBeNull();
        observed.AddedItems.ShouldBe([second]);
        observed.RemovedItems.ShouldBeEmpty();
    }

    /// <summary>Verifies removing one item reports only that removal and keeps the rest selected.</summary>
    [Fact]
    public void SetSelected_WhenRemovingFromMultipleSelection_ReportsOnlyTheRemoval()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(first, true);
        _ = tree.SetSelected(second, true);
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        var changed = tree.SetSelected(first, false);

        changed.ShouldBeTrue();
        tree.SelectedItems.ShouldBe([second]);
        _ = observed.ShouldNotBeNull();
        observed.AddedItems.ShouldBeEmpty();
        observed.RemovedItems.ShouldBe([first]);
    }

    /// <summary>Verifies a redundant request commits nothing and raises no event.</summary>
    [Fact]
    public void SetSelected_WhenAlreadyInRequestedState_ReportsNoChange()
    {
        var tree = Build(out var first, out _, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(first, true);
        var raised = 0;
        tree.SelectionChanged += (_, _) => raised++;

        var changed = tree.SetSelected(first, true);

        changed.ShouldBeFalse();
        raised.ShouldBe(0);
    }

    /// <summary>Verifies single mode replaces the selection, matching what input does.</summary>
    [Fact]
    public void SetSelected_WhenModeIsSingle_ReplacesSelectionAndReportsBothSides()
    {
        var tree = Build(out var first, out var second, out _);
        _ = tree.SetSelected(first, true);
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        _ = tree.SetSelected(second, true);

        tree.SelectedItems.ShouldBe([second]);
        _ = observed.ShouldNotBeNull();
        observed.AddedItems.ShouldBe([second]);
        observed.RemovedItems.ShouldBe([first]);
        observed.PreviousItem.ShouldBeSameAs(first);
        observed.CurrentItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies selecting under a mode that forbids it is rejected, not silently ignored.</summary>
    [Fact]
    public void SetSelected_WhenModeIsNone_RejectsSelection()
    {
        var tree = Build(out var first, out _, out _);
        tree.SelectionMode = TreeSelectionMode.None;

        _ = Should.Throw<InvalidOperationException>(() => tree.SetSelected(first, true));

        // Deselection stays permitted, so cleanup never has to inspect the mode first.
        tree.SetSelected(first, false).ShouldBeFalse();
    }

    /// <summary>Verifies a disabled item cannot be selected and reports no change.</summary>
    [Fact]
    public void SetSelected_WhenItemIsDisabled_ReportsNoChange()
    {
        var tree = Build(out var first, out _, out _);
        first.Enabled = false;

        tree.SetSelected(first, true).ShouldBeFalse();
        tree.SelectedItems.ShouldBeEmpty();
    }

    /// <summary>Verifies an existing selection survives a rebuild while an ancestor is disabled -
    /// EffectiveIsEnabled walked the whole ancestor chain, so a disabled ancestor wiped every
    /// realized item's selection on the very next rebuild even though nothing about the item
    /// itself changed.</summary>
    [Fact]
    public void CommitSelection_WhenAncestorIsDisabledOnRebuild_RetainsExistingSelection()
    {
        var tree = Build(out var first, out _, out _);
        var host = new Stack();
        host.Children.Add(tree);
        _ = tree.SetSelected(first, true);

        host.Enabled = false;
        tree.Items.Add(new TreeViewItem { Header = "third" });

        tree.SelectedItems.ShouldBe([first]);
    }

    /// <summary>Verifies collapsing an unrelated branch never erases selection, matching the
    /// control's own "collapsing a branch does not erase state" comment - even while an ancestor
    /// is disabled, which previously wiped every realized item's selection on this exact
    /// trigger.</summary>
    [Fact]
    public void CommitSelection_WhenCollapsingUnrelatedBranchUnderDisabledAncestor_RetainsSelection()
    {
        var tree = Build(out var first, out var second, out _);
        var host = new Stack();
        host.Children.Add(tree);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(second, true);

        host.Enabled = false;
        first.Expanded = false;

        tree.SelectedItems.ShouldBe([second]);
    }

    /// <summary>Verifies a foreign item is rejected before any mutation.</summary>
    [Fact]
    public void SetSelected_WhenItemIsNotOwned_Throws()
    {
        var tree = Build(out _, out _, out _);

        _ = Should.Throw<ArgumentNullException>(() => tree.SetSelected(null!, true));
        _ = Should.Throw<ArgumentException>(
            () => tree.SetSelected(new TreeViewItem { Header = "foreign" }, true));
    }

    /// <summary>Verifies select-all reports every item it added rather than only the first.</summary>
    [Fact]
    public void SelectAll_WhenMultipleSelection_ReportsEveryAddedItem()
    {
        var tree = Build(out var first, out var second, out var child);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        tree.SelectAll();

        _ = observed.ShouldNotBeNull();
        observed.AddedItems.ShouldBe([first, child, second]);
        observed.RemovedItems.ShouldBeEmpty();
    }

    /// <summary>Verifies narrowing the mode reports the items it deselected.</summary>
    [Fact]
    public void SelectionMode_WhenNarrowed_ReportsRemovedItems()
    {
        var tree = Build(out var first, out var second, out var child);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        tree.SelectAll();
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        tree.SelectionMode = TreeSelectionMode.Single;

        _ = observed.ShouldNotBeNull();
        observed.AddedItems.ShouldBeEmpty();
        observed.RemovedItems.ShouldBe([child, second]);
        tree.SelectedItems.ShouldBe([first]);
    }

    /// <summary>Verifies structural removal reports the detached item as deselected.</summary>
    [Fact]
    public void Remove_WhenSelectedItemIsDetached_ReportsItAsRemoved()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(first, true);
        _ = tree.SetSelected(second, true);
        TreeViewSelectionChangedEventArgs? observed = null;
        tree.SelectionChanged += (_, eventArgs) => observed = eventArgs;

        _ = tree.Items.Remove(second);

        _ = observed.ShouldNotBeNull();
        observed.RemovedItems.ShouldBe([second]);
        tree.SelectedItems.ShouldBe([first]);
    }


    /// <summary>
    /// Verifies a cancelled proposal precedes no commit and leaves the previous selection intact,
    /// matching the ListView transaction contract.
    /// </summary>
    [Fact]
    public void SetSelected_WhenChangingIsCancelled_PreservesState()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        List<string> order = [];
        tree.SelectionChanging += (_, eventArgs) =>
        {
            order.Add($"changing:{Names(eventArgs.AddedItems)}:{Names(eventArgs.RemovedItems)}");
            eventArgs.Cancel = eventArgs.AddedItems.Any(item => ReferenceEquals(item, second));
        };
        tree.SelectionChanged += (_, eventArgs) =>
            order.Add($"changed:{Names(eventArgs.AddedItems)}:{Names(eventArgs.RemovedItems)}");

        var accepted = tree.SetSelected(first, true);
        var refused = tree.SetSelected(second, true);

        accepted.ShouldBeTrue();
        refused.ShouldBeFalse();
        tree.SelectedItems.ShouldBe([first]);
        order.ShouldBe([
            "changing:first:",
            "changed:first:",
            "changing:second:"
        ]);
    }

    /// <summary>
    /// Verifies a handler that changes the selection itself abandons the proposal it was shown,
    /// so a stale delta is never committed on top of the handler's own decision.
    /// </summary>
    [Fact]
    public void SetSelected_WhenHandlerReentersAndChangesSelection_AbandonsTheProposal()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        var reentered = false;
        tree.SelectionChanging += (_, _) =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            _ = tree.SetSelected(first, true);
        };

        var accepted = tree.SetSelected(second, true);

        accepted.ShouldBeFalse();
        tree.SelectedItems.ShouldBe([first]);
    }

    /// <summary>Verifies normalization the control performs on its own behalf is not cancellable.</summary>
    [Fact]
    public void SelectionMode_WhenNarrowed_IgnoresCancellation()
    {
        var tree = Build(out var first, out _, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        tree.SelectAll();
        tree.SelectionChanging += (_, eventArgs) => eventArgs.Cancel = true;

        tree.SelectionMode = TreeSelectionMode.Single;

        // Honouring the cancel would leave several items selected under Single mode.
        tree.SelectedItems.ShouldBe([first]);
    }

    /// <summary>Verifies structural repair is not cancellable either.</summary>
    [Fact]
    public void Remove_WhenSelectedItemIsDetached_IgnoresCancellation()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        _ = tree.SetSelected(first, true);
        _ = tree.SetSelected(second, true);
        tree.SelectionChanging += (_, eventArgs) => eventArgs.Cancel = true;

        _ = tree.Items.Remove(second);

        tree.SelectedItems.ShouldBe([first]);
    }

    private static string Names(IReadOnlyList<TreeViewItem> items) =>
        string.Join(',', items.Select(static item => item.Header));

    private static TreeView Build(out TreeViewItem first, out TreeViewItem second, out TreeViewItem child)
    {
        var tree = new TreeView();
        first = new TreeViewItem { Header = "first", Expanded = true };
        child = new TreeViewItem { Header = "child" };
        second = new TreeViewItem { Header = "second" };
        first.Children.Add(child);
        tree.Items.Add(first);
        tree.Items.Add(second);

        return tree;
    }
}
