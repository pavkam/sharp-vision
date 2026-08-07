// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies hierarchical tree view ownership, selection, expand/collapse, and keyboard navigation.</summary>
public sealed class TreeViewTests
{
    /// <summary>Verifies the published selection snapshot cannot be rewritten by a consumer.</summary>
    [Fact]
    public void SelectedItems_WhenConsumerAttemptsMutation_RejectsTheChange()
    {
        var item = new TreeViewItem("selected");
        var tree = new TreeView { Items = { item } };
        tree.SelectItem(item);

        var snapshot = (IList<TreeViewItem>) tree.SelectedItems;

        _ = Should.Throw<NotSupportedException>(snapshot.Clear);
        snapshot.ShouldBe([item]);
        tree.SelectedItems.ShouldBe([item]);
    }

    /// <summary>Verifies a tree view starts as a framed surface with a visible border and semantic background.</summary>
    [ComponentUnitEvidence(typeof(TreeView))]
    [Fact]
    public void Constructor_WhenCreated_UsesFramedBackgroundDefaults()
    {
        // Arrange and act
        var tree = new TreeView();

        // Assert
        tree.ActualBorder.Sides.ShouldBe(BorderSide.All);
        tree.Face.Background.ShouldBe(SemanticColor.Surface);
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
    [ComponentUnitEvidence(typeof(TreeViewItem))]
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var tree = new TreeView();
        tree.Items.Add(new TreeViewItem { Header = "Node 1" });
        tree.Items.Add(new TreeViewItem { Header = "Node 2" });
        tree.Items.Add(new TreeViewItem { Header = "Node 3" });

        tree.Items.Count.ShouldBe(3);
    }

    /// <summary>Verifies non-pointer input remains available through the inherited routed events.</summary>
    [Fact]
    public void Dispatch_WhenTreeViewItemReceivesKey_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var item = new TreeViewItem();
        var raised = 0;
        item.KeyDown += (_, _) => raised++;
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        _ = Router.Route(item, Events.Key, eventArgs);

        // Assert
        eventArgs.Handled.ShouldBeFalse();
        raised.ShouldBe(1);
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
        item.Selected.ShouldBeTrue();
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
        parent.Expanded = false;

        tree.SelectedItem.ShouldBeSameAs(child1);

        // Re-expand; children reappear and become selectable again.
        parent.Expanded = true;

        tree.SelectItem(child2);
        tree.SelectedItem.ShouldBeSameAs(child2);
    }

    /// <summary>Verifies multiple selection, select-all, clear, and disabled-node filtering.</summary>
    [Fact]
    public void SelectionMode_WhenMultiple_SelectsEnabledItemsAndSupportsClear()
    {
        var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        var first = new TreeViewItem { Header = "First" };
        var disabled = new TreeViewItem { Header = "Disabled", Enabled = false };
        var last = new TreeViewItem { Header = "Last" };
        tree.Items.Add(first);
        tree.Items.Add(disabled);
        tree.Items.Add(last);

        tree.SelectAll();

        tree.SelectedItems.ShouldBe([first, last]);
        tree.SelectedItem.ShouldBeSameAs(first);
        disabled.Selected.ShouldBeFalse();

        tree.ClearSelection();

        tree.SelectedItems.ShouldBeEmpty();
        first.Selected.ShouldBeFalse();
        last.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies check state propagates down and reports mixed child state on a parent.</summary>
    [Fact]
    public void Checkable_WhenChildrenDiffer_ParentBecomesIndeterminate()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent", Checkable = true };
        var first = new TreeViewItem { Header = "First", Checkable = true };
        var second = new TreeViewItem { Header = "Second", Checkable = true };
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
        var parent = new TreeViewItem { Header = "Parent", Checkable = true };
        var child = new TreeViewItem { Header = "Child", Checkable = true, IsChecked = true };
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

        child.Checkable = false;
        child.Checkable = true;

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
        var root = new TreeViewItem { Header = "Root", Checkable = true };
        var parent = new TreeViewItem { Header = "Parent", Checkable = true };
        var leaf = new TreeViewItem { Header = "Leaf", Checkable = true };
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
        var a = new TreeViewItem { Header = "A", Expanded = false };
        var b = new TreeViewItem { Header = "B", Expanded = false };
        var c = new TreeViewItem { Header = "C" };
        b.Children.Add(c);
        a.Children.Add(b);
        tree.Items.Add(a);

        tree.ExpandAll();

        a.Expanded.ShouldBeTrue();
        b.Expanded.ShouldBeTrue();
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

        a.Expanded.ShouldBeTrue();
        b.Expanded.ShouldBeTrue();

        tree.CollapseAll();

        a.Expanded.ShouldBeFalse();
        b.Expanded.ShouldBeFalse();
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
            parent.Expanded.ShouldBeTrue();

            // Left on an expanded parent collapses it.
            var left1 = new KeyEventArgs(new Stroke(
                Code.Left, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, left1);

            left1.Handled.ShouldBeTrue();
            parent.Expanded.ShouldBeFalse();

            // Re-expand and navigate down to the child.
            parent.Expanded = true;
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
            var parent = new TreeViewItem { Header = "Parent", Expanded = false };
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
            parent.Expanded.ShouldBeFalse();

            // Right on a collapsed parent expands it.
            var right1 = new KeyEventArgs(new Stroke(
                Code.Right, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, right1);

            right1.Handled.ShouldBeTrue();
            parent.Expanded.ShouldBeTrue();

            // Right on an already expanded parent navigates to the first child.
            var right2 = new KeyEventArgs(new Stroke(
                Code.Right, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, right2);

            right2.Handled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a selected descendant remains owned and selected after its ancestor is
    /// collapsed, even once the descendant itself is disabled. Retention filters on ownership
    /// alone, not EffectiveIsEnabled - disabling still blocks new selection requests (SetSelected,
    /// ApplyInputSelection, SelectAll), but no longer wipes an existing selection on the next
    /// rebuild, which previously happened inconsistently: a disabled item still selected via a
    /// collapsed (unparented) branch survived, while an otherwise-identical realized item did
    /// not.</summary>
    [Fact]
    public async Task CollapsedDescendant_WhenDisabled_RetainsSelectionAsync()
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
            parent.Expanded = false;
            var selectionChanged = 0;
            tree.SelectionChanged += (_, _) => selectionChanged++;

            first.Enabled = false;

            tree.SelectedItems.ShouldBe([parent, first, second]);
            tree.SelectedItem.ShouldBeSameAs(parent);
            selectionChanged.ShouldBe(0);
            first.Selected.ShouldBeTrue();
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
            parent.Expanded = false;
            var selectionChanged = 0;
            tree.SelectionChanged += (_, _) => selectionChanged++;

            _ = parent.Children.Remove(first);

            tree.SelectedItems.ShouldBe([parent, second]);
            tree.SelectedItem.ShouldBeSameAs(parent);
            selectionChanged.ShouldBe(1);
            first.Selected.ShouldBeFalse();
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
            var root = new TreeViewItem { Header = "Root", Expanded = false };
            var branch = new TreeViewItem { Header = "Branch", Expanded = false };
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

            a.CanTabStop.ShouldBeFalse();
            b.CanTabStop.ShouldBeFalse();
            a.CanFocus.ShouldBeFalse();
            b.CanFocus.ShouldBeFalse();
            focus.Focus(tree).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the generated scroll container's contract is reachable directly on
    /// TreeView, without a caller needing to know about the private items Stack.</summary>
    [Fact]
    public void ScrollBy_WhenContentExceedsViewport_MovesVerticalOffsetAndRaisesScrollChanged()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));
        List<ScrollChangedEventArgs> changes = [];
        tree.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        tree.Extent.Height.ShouldBeGreaterThan(tree.Viewport.Height);
        var moved = tree.ScrollBy(0, 3);

        moved.ShouldBeTrue();
        tree.VerticalOffset.ShouldBe(3);
        _ = changes.ShouldHaveSingleItem();
    }

    /// <summary>Verifies BringItemIntoView scrolls minimally to reveal an item below the viewport.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsBelowViewport_ScrollsToRevealIt()
    {
        var tree = new TreeView();
        TreeViewItem? last = null;

        for (var index = 0; index < 20; index++)
        {
            last = new TreeViewItem { Header = $"Item {index}" };
            tree.Items.Add(last);
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));

        var moved = tree.BringItemIntoView(last!);

        moved.ShouldBeTrue();
        tree.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies BringItemIntoView validates its argument like the underlying container does.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentNullException>(() => tree.BringItemIntoView(null!));
    }

    /// <summary>Verifies Insert places a node at the requested position without disturbing existing order.</summary>
    [Fact]
    public void Insert_WhenCalled_PlacesNodeAtRequestedPosition()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        var inserted = new TreeViewItem { Header = "Inserted" };

        tree.Items.Insert(1, inserted);

        tree.Items.ToArray().ShouldBe([first, inserted, second]);
    }

    /// <summary>Verifies an out-of-range insertion index throws before mutating the collection.</summary>
    [Fact]
    public void Insert_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "First" };
        tree.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => tree.Items.Insert(2, new TreeViewItem { Header = "New" }));

        tree.Items.ToArray().ShouldBe([item]);
    }

    /// <summary>Verifies Insert still rejects an item that would create a cycle in a nested child collection.</summary>
    [Fact]
    public void Insert_WhenCandidateIsAnAncestor_ThrowsBeforeMutation()
    {
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        root.Children.Add(child);

        _ = Should.Throw<InvalidOperationException>(() => child.Children.Insert(0, root));

        child.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies RemoveAt detaches the node at a position without disposing it.</summary>
    [Fact]
    public void RemoveAt_WhenCalled_DetachesNodeWithoutDisposal()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        tree.Items.Add(first);
        tree.Items.Add(second);

        tree.Items.RemoveAt(0);

        tree.Items.ToArray().ShouldBe([second]);
        first.ParentCollection.ShouldBeNull();
    }

    /// <summary>Verifies an out-of-range removal index throws before mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "First" };
        tree.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.Items.RemoveAt(1));

        tree.Items.ToArray().ShouldBe([item]);
    }

    /// <summary>Verifies the indexer replaces one node at a position, detaching the old node without disposal.</summary>
    [Fact]
    public void Indexer_WhenAssigned_ReplacesNodeAtPositionWithoutDisposingOld()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        var replacement = new TreeViewItem { Header = "Replacement" };

        tree.Items[0] = replacement;

        tree.Items.ToArray().ShouldBe([replacement, second]);
        first.ParentCollection.ShouldBeNull();
        replacement.ParentCollection.ShouldBeSameAs(tree.Items);
    }

    /// <summary>Verifies the indexer rejects a candidate already owned by another collection.</summary>
    [Fact]
    public void Indexer_WhenCandidateIsAlreadyOwned_ThrowsAndLeavesCollectionUnchanged()
    {
        var tree = new TreeView();
        var other = new TreeView();
        var item = new TreeViewItem { Header = "First" };
        tree.Items.Add(item);
        var owned = new TreeViewItem { Header = "Owned" };
        other.Items.Add(owned);

        _ = Should.Throw<InvalidOperationException>(() => tree.Items[0] = owned);

        tree.Items.ToArray().ShouldBe([item]);
        other.Items.ToArray().ShouldBe([owned]);
    }

    /// <summary>Verifies assigning null through the indexer throws.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_Throws()
    {
        var tree = new TreeView();
        tree.Items.Add(new TreeViewItem { Header = "First" });

        _ = Should.Throw<ArgumentNullException>(() => tree.Items[0] = null!);
    }

    /// <summary>Verifies Move repositions an owned node while preserving its identity and children.</summary>
    [Fact]
    public void Move_WhenCalled_RepositionsNodePreservingIdentityAndChildren()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        var third = new TreeViewItem { Header = "Third" };
        var grandchild = new TreeViewItem { Header = "Grandchild" };
        second.Children.Add(grandchild);
        tree.Items.Add(first);
        tree.Items.Add(second);
        tree.Items.Add(third);

        tree.Items.Move(1, 2);

        tree.Items.ToArray().ShouldBe([first, third, second]);
        second.Children.ToArray().ShouldBe([grandchild]);
    }

    /// <summary>Verifies an out-of-range move index throws before mutating the collection.</summary>
    [Fact]
    public void Move_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        tree.Items.Add(first);
        tree.Items.Add(second);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.Items.Move(0, 2));

        tree.Items.ToArray().ShouldBe([first, second]);
    }

    /// <summary>Verifies IndexOf reports the current position of an owned node and -1 for a foreign node.</summary>
    [Fact]
    public void IndexOf_WhenItemIsOwnedOrForeign_ReportsPositionOrNegativeOne()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        var foreign = new TreeViewItem { Header = "Foreign" };

        tree.Items.IndexOf(second).ShouldBe(1);
        tree.Items.IndexOf(foreign).ShouldBe(-1);
    }

    /// <summary>Verifies disposed collection mutations reject Insert, RemoveAt, indexer assignment, and Move.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsInsertRemoveAtIndexerAndMove()
    {
        var tree = new TreeView();
        tree.Items.Add(new TreeViewItem { Header = "First" });
        tree.Items.Add(new TreeViewItem { Header = "Second" });
        tree.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => tree.Items.Insert(0, new TreeViewItem { Header = "New" }));
        _ = Should.Throw<ObjectDisposedException>(() => tree.Items.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(() => tree.Items[0] = new TreeViewItem { Header = "New" });
        _ = Should.Throw<ObjectDisposedException>(() => tree.Items.Move(0, 1));
    }

    /// <summary>Verifies Clear() on a disposed, empty TreeView still throws ObjectDisposedException
    /// like every sibling mutation, instead of the emptiness check running before disposal is
    /// verified and silently succeeding - the one shape the other disposed-collection test above
    /// does not cover, since it disposes a TreeView with existing roots.</summary>
    [Fact]
    public void Clear_WhenOwnerIsDisposedAndCollectionIsEmpty_ThrowsObjectDisposedException()
    {
        var tree = new TreeView();
        tree.Dispose();

        _ = Should.Throw<ObjectDisposedException>(tree.Items.Clear);
    }

    /// <summary>Verifies a header carrying a terminal control character is rejected instead of
    /// silently dropping post-newline text or shifting later cells at render time.</summary>
    [Theory]
    [InlineData("Save\nAs")]
    [InlineData("Save\rAs")]
    [InlineData("Save\tAs")]
    public void Header_WhenContainingControlCharacter_Throws(string header)
    {
        var item = new TreeViewItem();

        _ = Should.Throw<ArgumentException>(() => item.Header = header);
    }
}
