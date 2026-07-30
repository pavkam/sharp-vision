// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies hierarchical tree view ownership, selection, expand/collapse, and keyboard navigation.</summary>
public sealed class TreeViewTests
{
    /// <summary>Verifies a tree view starts as a framed surface with a visible border and semantic background.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesFramedBackgroundDefaults()
    {
        // Arrange and act
        var tree = new TreeView();

        // Assert
        tree.ActualBorder.Sides.ShouldBe(BorderSide.All);
        tree.Face.Background.ShouldBe(ThemeColor.Surface);
    }

    /// <summary>
    /// Verifies changing the selection mode publishes its own property notification, which a
    /// two-way binding needs and the selection notifications cannot substitute for.
    /// </summary>
    [Fact]
    public void SelectionMode_WhenChanged_RaisesPropertyChanged()
    {
        var tree = new TreeView();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.SelectionMode = TreeSelectionMode.Multiple;

        tree.SelectionMode.ShouldBe(TreeSelectionMode.Multiple);
        changed.ShouldContain(nameof(TreeView.SelectionMode));
    }

    /// <summary>
    /// Verifies narrowing the mode publishes the mode change alongside the selection it normalized,
    /// and publishes the mode first so an observer already sees the new configuration.
    /// </summary>
    [Fact]
    public void SelectionMode_WhenNarrowedWithSelection_RaisesModeBeforeSelection()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var first = new TreeViewItem { Header = "a" };
        var second = new TreeViewItem { Header = "b" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        tree.SelectAll();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.SelectionMode = TreeSelectionMode.Single;

        changed.ShouldContain(nameof(TreeView.SelectionMode));
        changed.IndexOf(nameof(TreeView.SelectionMode))
            .ShouldBeLessThan(changed.IndexOf(nameof(TreeView.SelectedItems)));
    }

    /// <summary>Verifies assigning the same selection mode publishes nothing.</summary>
    [Fact]
    public void SelectionMode_WhenUnchanged_RaisesNothing()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        tree.SelectionMode = TreeSelectionMode.Multiple;

        changed.ShouldBeEmpty();
    }

    /// <summary>Verifies items are added through the typed collection.</summary>
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var tree = new TreeView();
        tree.Items.Add(new TreeViewItem { Header = "Node 1" });
        tree.Items.Add(new TreeViewItem { Header = "Node 2" });
        tree.Items.Add(new TreeViewItem { Header = "Node 3" });

        tree.Items.Count.ShouldBe(3);
    }

    /// <summary>Verifies nesting depth is assigned internally when items are added to the tree.</summary>
    [Fact]
    public void Items_WhenNested_TracksDepth()
    {
        var tree = new TreeView();
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        var grandchild = new TreeViewItem { Header = "Grandchild" };
        child.Children.Add(grandchild);
        root.Children.Add(child);
        tree.Items.Add(root);

        root.Depth.ShouldBe(0);
        child.Depth.ShouldBe(1);
        grandchild.Depth.ShouldBe(2);
    }

    /// <summary>Verifies callers select an owned item programmatically.</summary>
    [Fact]
    public void SelectItem_WhenOwned_UpdatesSelection()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "Node" };
        tree.Items.Add(item);

        tree.SelectItem(item);

        tree.SelectedItem.ShouldBeSameAs(item);
        item.IsSelected.ShouldBeTrue();
    }

    /// <summary>Verifies programmatic selection rejects an item from another tree view.</summary>
    [Fact]
    public void SelectItem_WhenForeign_ThrowsArgumentException()
    {
        var tree = new TreeView();
        var other = new TreeView();
        var item = new TreeViewItem { Header = "Foreign" };
        other.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => tree.SelectItem(item));
    }

    /// <summary>Verifies SelectionChanged provides old and new items in typed event args.</summary>
    [Fact]
    public void SelectionChanged_WhenItemSelected_ProvidesOldAndNewInEventArgs()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        tree.Items.Add(a);
        tree.Items.Add(b);

        TreeViewItem? previousItem = null;
        TreeViewItem? currentItem = null;
        var raised = 0;
        tree.SelectionChanged += (_, eventArgs) =>
        {
            previousItem = eventArgs.PreviousItem;
            currentItem = eventArgs.CurrentItem;
            raised++;
        };

        tree.SelectItem(a);

        raised.ShouldBe(1);
        previousItem.ShouldBeNull();
        currentItem.ShouldBeSameAs(a);

        tree.SelectItem(b);

        raised.ShouldBe(2);
        previousItem.ShouldBeSameAs(a);
        currentItem.ShouldBeSameAs(b);
    }

    /// <summary>Verifies collapsing a parent removes its children from the visible flat list.</summary>
    [Fact]
    public void IsExpanded_WhenToggled_RebuildsVisibleItems()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent" };
        var child1 = new TreeViewItem { Header = "Child 1" };
        var child2 = new TreeViewItem { Header = "Child 2" };
        parent.Children.Add(child1);
        parent.Children.Add(child2);
        tree.Items.Add(parent);

        // Children are visible when expanded; selecting succeeds.
        tree.SelectItem(child1);
        tree.SelectedItem.ShouldBeSameAs(child1);

        // Collapse the parent; children disappear from navigation but retain selection state.
        parent.IsExpanded = false;

        tree.SelectedItem.ShouldBeSameAs(child1);

        // Re-expand; children reappear and become selectable again.
        parent.IsExpanded = true;

        tree.SelectItem(child2);
        tree.SelectedItem.ShouldBeSameAs(child2);
    }

    /// <summary>Verifies multiple selection, select-all, clear, and disabled-node filtering.</summary>
    [Fact]
    public void SelectionMode_WhenMultiple_SelectsEnabledItemsAndSupportsClear()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var first = new TreeViewItem { Header = "First" };
        var disabled = new TreeViewItem { Header = "Disabled", IsEnabled = false };
        var last = new TreeViewItem { Header = "Last" };
        tree.Items.Add(first);
        tree.Items.Add(disabled);
        tree.Items.Add(last);

        tree.SelectAll();

        tree.SelectedItems.ShouldBe([first, last]);
        tree.SelectedItem.ShouldBeSameAs(first);
        disabled.IsSelected.ShouldBeFalse();

        tree.ClearSelection();

        tree.SelectedItems.ShouldBeEmpty();
        first.IsSelected.ShouldBeFalse();
        last.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies check state propagates down and reports mixed child state on a parent.</summary>
    [Fact]
    public void Checkable_WhenChildrenDiffer_ParentBecomesIndeterminate()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var first = new TreeViewItem { Header = "First", IsCheckable = true };
        var second = new TreeViewItem { Header = "Second", IsCheckable = true };
        parent.Children.Add(first);
        parent.Children.Add(second);
        tree.Items.Add(parent);

        parent.IsChecked = true;
        first.IsChecked = false;

        parent.IsChecked.ShouldBeNull();
        first.IsChecked.ShouldBe(false);
        second.IsChecked.ShouldBe(true);

        parent.IsChecked = false;

        parent.IsChecked.ShouldBe(false);
        first.IsChecked.ShouldBe(false);
        second.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies changing child checkability reports each effective ancestor transition once.</summary>
    [Fact]
    public void IsCheckable_WhenChanged_PropagatesEffectiveCheckStateWithoutDuplicateEvents()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var child = new TreeViewItem { Header = "Child", IsCheckable = true, IsChecked = true };
        parent.Children.Add(child);
        tree.Items.Add(parent);

        var parentChanges = new List<CheckChangedEventArgs>();
        var propertyChanges = 0;
        parent.CheckStateChanged += (_, args) => parentChanges.Add(args);
        parent.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TreeViewItem.IsChecked))
            {
                propertyChanges++;
            }
        };

        child.IsCheckable = false;
        child.IsCheckable = true;

        parentChanges.Count.ShouldBe(2);
        parentChanges[0].Previous.ShouldBe(true);
        parentChanges[0].Current.ShouldBe(false);
        parentChanges[1].Previous.ShouldBe(false);
        parentChanges[1].Current.ShouldBe(true);
        propertyChanges.ShouldBe(2);
    }

    /// <summary>Verifies check-state transitions notify every ancestor exactly once.</summary>
    [Fact]
    public void IsChecked_WhenDeepDescendantChanges_NotifiesEveryAncestorOnce()
    {
        var tree = new TreeView();
        var root = new TreeViewItem { Header = "Root", IsCheckable = true };
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var leaf = new TreeViewItem { Header = "Leaf", IsCheckable = true };
        parent.Children.Add(leaf);
        root.Children.Add(parent);
        tree.Items.Add(root);

        var rootChanges = 0;
        var parentChanges = 0;
        var rootProperties = 0;
        var parentProperties = 0;
        root.CheckStateChanged += (_, _) => rootChanges++;
        parent.CheckStateChanged += (_, _) => parentChanges++;
        root.PropertyChanged += (_, args) => rootProperties += args.PropertyName == nameof(TreeViewItem.IsChecked) ? 1 : 0;
        parent.PropertyChanged += (_, args) => parentProperties += args.PropertyName == nameof(TreeViewItem.IsChecked) ? 1 : 0;

        leaf.IsChecked = true;
        leaf.IsChecked = false;

        rootChanges.ShouldBe(2);
        parentChanges.ShouldBe(2);
        rootProperties.ShouldBe(2);
        parentProperties.ShouldBe(2);
    }

    /// <summary>Verifies a detached candidate cannot be inserted into a descendant collection.</summary>
    [Fact]
    public void Children_WhenDestinationIsInsideCandidateSubtree_Throws()
    {
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        var grandchild = new TreeViewItem { Header = "Grandchild" };
        root.Children.Add(child);
        child.Children.Add(grandchild);

        _ = Should.Throw<InvalidOperationException>(() => child.Children.Add(root));
        _ = Should.Throw<InvalidOperationException>(() => grandchild.Children.Add(root));
    }

    /// <summary>Verifies a non-checkable item rejects check state mutation.</summary>
    [Fact]
    public void IsChecked_WhenItemIsNotCheckable_Throws()
    {
        var item = new TreeViewItem { Header = "Leaf" };

        _ = Should.Throw<InvalidOperationException>(() => item.IsChecked = true);
    }

    /// <summary>Verifies expanding every node in the tree.</summary>
    [Fact]
    public void ExpandAll_WhenCalled_ExpandsEntireTree()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A", IsExpanded = false };
        var b = new TreeViewItem { Header = "B", IsExpanded = false };
        var c = new TreeViewItem { Header = "C" };
        b.Children.Add(c);
        a.Children.Add(b);
        tree.Items.Add(a);

        tree.ExpandAll();

        a.IsExpanded.ShouldBeTrue();
        b.IsExpanded.ShouldBeTrue();
    }

    /// <summary>Verifies collapsing every node in the tree.</summary>
    [Fact]
    public void CollapseAll_WhenCalled_CollapsesEntireTree()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };
        b.Children.Add(c);
        a.Children.Add(b);
        tree.Items.Add(a);

        a.IsExpanded.ShouldBeTrue();
        b.IsExpanded.ShouldBeTrue();

        tree.CollapseAll();

        a.IsExpanded.ShouldBeFalse();
        b.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies items added inside a batch do not appear in the visible tree until the
    /// matching EndUpdate, which then commits every addition in one rebuild.</summary>
    [Fact]
    public void BeginUpdate_WhenItemsAreAddedDuringABatch_DefersTheVisibleRebuildUntilEndUpdate()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };

        tree.BeginUpdate();
        tree.Items.Add(a);
        tree.Items.Add(b);
        tree.Items.Add(c);

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBeEmpty();

        tree.EndUpdate();

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([a, b, c]);
    }

    /// <summary>Verifies nested BeginUpdate/EndUpdate pairs defer the rebuild until the outermost
    /// EndUpdate returns, matching common nesting conventions.</summary>
    [Fact]
    public void BeginUpdate_WhenCallsAreNested_RebuildsOnlyAtTheOutermostEndUpdate()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };

        tree.BeginUpdate();
        tree.BeginUpdate();
        tree.Items.Add(a);
        tree.EndUpdate();

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBeEmpty("the outer batch is still active");

        tree.EndUpdate();

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([a]);
    }

    /// <summary>Verifies an unmatched EndUpdate is rejected rather than silently ignored.</summary>
    [Fact]
    public void EndUpdate_WhenCalledWithoutBeginUpdate_ThrowsInvalidOperationException()
    {
        var tree = new TreeView();

        _ = Should.Throw<InvalidOperationException>(tree.EndUpdate);
    }

    /// <summary>Verifies removing a selected item clears the selection.</summary>
    [Fact]
    public async Task Items_WhenSelectedItemRemoved_ClearsSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            var b = new TreeViewItem { Header = "B" };
            tree.Items.Add(a);
            tree.Items.Add(b);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            tree.SelectItem(a);
            tree.SelectedItem.ShouldBeSameAs(a);

            _ = tree.Items.Remove(a);

            tree.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Down arrow navigates between visible items via the bubble handler.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_NavigatesBetweenItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            var b = new TreeViewItem { Header = "B" };
            var c = new TreeViewItem { Header = "C" };
            tree.Items.Add(a);
            tree.Items.Add(b);
            tree.Items.Add(c);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            // First Down selects the first item.
            var down1 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down1);

            down1.Handled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(a);

            // Second Down moves to the next item.
            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);

            down2.Handled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Left arrow collapses an expanded parent or navigates to the parent item.</summary>
    [Fact]
    public async Task Dispatch_WhenLeftKeyPressed_CollapsesOrNavigatesToParentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var parent = new TreeViewItem { Header = "Parent" };
            var child = new TreeViewItem { Header = "Child" };
            parent.Children.Add(child);
            tree.Items.Add(parent);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            // Navigate to parent (first visible item).
            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down);
            tree.SelectedItem.ShouldBeSameAs(parent);
            parent.IsExpanded.ShouldBeTrue();

            // Left on an expanded parent collapses it.
            var left1 = new KeyEventArgs(new Stroke(
                Code.Left, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, left1);

            left1.Handled.ShouldBeTrue();
            parent.IsExpanded.ShouldBeFalse();

            // Re-expand and navigate down to the child.
            parent.IsExpanded = true;
            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);
            tree.SelectedItem.ShouldBeSameAs(child);

            // Left on a child navigates to its parent.
            var left2 = new KeyEventArgs(new Stroke(
                Code.Left, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, left2);

            left2.Handled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(parent);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Right arrow expands a collapsed parent or navigates to its first child.</summary>
    [Fact]
    public async Task Dispatch_WhenRightKeyPressed_ExpandsOrNavigatesToChildAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var parent = new TreeViewItem { Header = "Parent", IsExpanded = false };
            var child = new TreeViewItem { Header = "Child" };
            parent.Children.Add(child);
            tree.Items.Add(parent);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            // Navigate to parent (first visible item).
            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down);
            tree.SelectedItem.ShouldBeSameAs(parent);
            parent.IsExpanded.ShouldBeFalse();

            // Right on a collapsed parent expands it.
            var right1 = new KeyEventArgs(new Stroke(
                Code.Right, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, right1);

            right1.Handled.ShouldBeTrue();
            parent.IsExpanded.ShouldBeTrue();

            // Right on an already expanded parent navigates to the first child.
            var right2 = new KeyEventArgs(new Stroke(
                Code.Right, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, right2);

            right2.Handled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a selected descendant remains owned after its ancestor is collapsed.</summary>
    [Fact]
    public async Task CollapsedDescendant_WhenDisabled_RemovesSelectionAndRepairsAnchorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
            var parent = new TreeViewItem { Header = "Parent" };
            var first = new TreeViewItem { Header = "First" };
            var second = new TreeViewItem { Header = "Second" };
            parent.Children.Add(first);
            parent.Children.Add(second);
            tree.Items.Add(parent);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);

            tree.SelectAll();
            parent.IsExpanded = false;
            var selectionChanged = 0;
            tree.SelectionChanged += (_, _) => selectionChanged++;

            first.IsEnabled = false;

            tree.SelectedItems.ShouldBe([parent, second]);
            tree.SelectedItem.ShouldBeSameAs(parent);
            selectionChanged.ShouldBe(1);
            first.IsSelected.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing a selected descendant below a collapsed ancestor repairs selection.</summary>
    [Fact]
    public async Task CollapsedDescendant_WhenRemoved_RemovesSelectionAndRepairsAnchorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
            var parent = new TreeViewItem { Header = "Parent" };
            var first = new TreeViewItem { Header = "First" };
            var second = new TreeViewItem { Header = "Second" };
            parent.Children.Add(first);
            parent.Children.Add(second);
            tree.Items.Add(parent);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);

            tree.SelectAll();
            parent.IsExpanded = false;
            var selectionChanged = 0;
            tree.SelectionChanged += (_, _) => selectionChanged++;

            _ = parent.Children.Remove(first);

            tree.SelectedItems.ShouldBe([parent, second]);
            tree.SelectedItem.ShouldBeSameAs(parent);
            selectionChanged.ShouldBe(1);
            first.IsSelected.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies nested mutations notify the tree even while every ancestor is collapsed.</summary>
    [Fact]
    public async Task CollapsedGrandchildCollection_WhenMutated_RebuildsVisibleItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var root = new TreeViewItem { Header = "Root", IsExpanded = false };
            var branch = new TreeViewItem { Header = "Branch", IsExpanded = false };
            root.Children.Add(branch);
            tree.Items.Add(root);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);

            var leaf = new TreeViewItem { Header = "Leaf" };
            branch.Children.Add(leaf);

            tree.SelectItem(leaf);
            tree.SelectedItem.ShouldBeSameAs(leaf);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the tree view is the only tab stop; individual items are not focusable.</summary>
    [Fact]
    public async Task Focus_WhenCreated_IsOnlyTabStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            var b = new TreeViewItem { Header = "B" };
            tree.Items.Add(a);
            tree.Items.Add(b);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);

            a.IsTabStop.ShouldBeFalse();
            b.IsTabStop.ShouldBeFalse();
            a.CanFocus.ShouldBeFalse();
            b.CanFocus.ShouldBeFalse();
            focus.Focus(tree).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }
}
