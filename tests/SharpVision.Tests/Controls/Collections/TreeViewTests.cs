// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies hierarchical tree view ownership, selection, expand/collapse, and keyboard navigation.</summary>
public sealed partial class TreeViewTests
{
    /// <summary>Verifies select-all normalizes character case and lock state but rejects larger
    /// application-command chords.</summary>
    [Theory]
    [InlineData('a', Modifiers.Control, true)]
    [InlineData('A', Modifiers.Control | Modifiers.CapsLock, true)]
    [InlineData('a', Modifiers.Control | Modifiers.NumLock, true)]
    [InlineData('A', Modifiers.Control | Modifiers.Shift, false)]
    [InlineData('a', Modifiers.Control | Modifiers.Alt, false)]
    [InlineData('a', Modifiers.Control | Modifiers.Super, false)]
    public void Dispatch_WhenSelectAllCharacterCarriesModifiers_MatchesExactNormalizedCommand(
        char character,
        Modifiers modifiers,
        bool expectedSelection)
    {
        // Arrange
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        var tree = new TreeView
        {
            SelectionMode = TreeSelectionMode.Multiple,
            Items = { first, second }
        };
        tree.SelectItem(first);
        var key = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            modifiers,
            KeyAction.Press));

        // Act
        _ = Router.Route(tree, Events.Key, key);

        // Assert
        tree.SelectedItems.ShouldBe(expectedSelection ? [first, second] : [first]);
        key.IsHandled.ShouldBe(expectedSelection);
    }

    /// <summary>Verifies Space preserves collection-selection modifiers for ordinary and
    /// checkable nodes while rejecting application-command modifiers.</summary>
    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.Control, true)]
    [InlineData(Modifiers.Shift, true)]
    [InlineData(Modifiers.Control | Modifiers.Shift, true)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(Modifiers.Alt, false)]
    [InlineData(Modifiers.Super, false)]
    [InlineData(Modifiers.Hyper, false)]
    [InlineData(Modifiers.Meta, false)]
    [InlineData(Modifiers.Shift | Modifiers.Super, false)]
    public void Dispatch_WhenSpaceCarriesModifiers_MutatesOnlyForCollectionGesture(
        Modifiers modifiers,
        bool expectedMutation)
    {
        // Arrange
        var selectable = new TreeViewItem { Header = "Selectable" };
        var tree = new TreeView
        {
            SelectionMode = TreeSelectionMode.Multiple,
            Items = { selectable }
        };
        var checkable = new TreeViewItem { Header = "Checkable", IsCheckable = true };
        var checkTree = new TreeView { Items = { checkable } };
        tree.SelectItem(selectable);
        tree.ClearSelection();
        checkTree.SelectItem(checkable);
        checkTree.ClearSelection();
        var initialCheckState = checkable.IsChecked;
        var selectionKey = CharacterKey(tree, new Rune(' '), modifiers);
        var checkKey = CharacterKey(checkTree, new Rune(' '), modifiers);

        // Assert
        tree.SelectedItems.ShouldBe(expectedMutation ? [selectable] : []);
        checkable.IsChecked.ShouldBe(expectedMutation ? true : initialCheckState);
        selectionKey.IsHandled.ShouldBe(expectedMutation);
        checkKey.IsHandled.ShouldBe(expectedMutation);
    }

    private static KeyEventArgs CharacterKey(ControlBase target, Rune character, Modifiers modifiers)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.Character,
            character,
            nativeCode: 0,
            modifiers,
            KeyAction.Press));
        _ = Router.Route(target, Events.Key, eventArgs);
        return eventArgs;
    }

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

    /// <summary>Verifies assigning SelectedItem selects the owned item, and null clears it.</summary>
    [Fact]
    public void SelectedItem_WhenAssigned_SelectsOrClears()
    {
        var first = new TreeViewItem("First");
        var second = new TreeViewItem("Second");
        var tree = new TreeView { Items = { first, second } };
        tree.SelectedItem.ShouldBeNull();

        tree.SelectedItem = second;

        tree.SelectedItem.ShouldBeSameAs(second);
        second.IsSelected.ShouldBeTrue();

        tree.SelectedItem = null;

        tree.SelectedItem.ShouldBeNull();
        second.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies assigning SelectedItem with an item from another tree is rejected.</summary>
    [Fact]
    public void SelectedItem_WhenAssignedForeignItem_Throws()
    {
        var tree = new TreeView { Items = { new TreeViewItem("Owned") } };
        var foreign = new TreeViewItem("Foreign");

        _ = Should.Throw<ArgumentException>(() => tree.SelectedItem = foreign);
    }

    /// <summary>Verifies a tree view starts as a framed surface with a visible border and semantic background.</summary>
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

    /// <summary>Verifies unchanged LineSize assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void LineSize_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var tree = new TreeView();
        var notifications = 0;
        tree.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TreeView.LineSize))
            {
                notifications++;
            }
        };

        tree.LineSize = 3;
        tree.LineSize = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies unchanged PageOverlap assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void PageOverlap_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var tree = new TreeView();
        var notifications = 0;
        tree.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TreeView.PageOverlap))
            {
                notifications++;
            }
        };

        tree.PageOverlap = 3;
        tree.PageOverlap = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies LineSize rejects a negative value.</summary>
    [Fact]
    public void LineSize_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.LineSize = -1);
    }

    /// <summary>Verifies PageOverlap rejects a negative value.</summary>
    [Fact]
    public void PageOverlap_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.PageOverlap = -1);
    }

    /// <summary>Verifies LineSize forwards to, and reads back from, the generated scroll container.</summary>
    [Fact]
    public void LineSize_WhenSet_ForwardsToScrollContainer()
    {
        var tree = new TreeView { LineSize = 3 };

        tree.LineSize.ShouldBe(3);
    }

    /// <summary>Verifies PageOverlap forwards to, and reads back from, the generated scroll container.</summary>
    [Fact]
    public void PageOverlap_WhenSet_ForwardsToScrollContainer()
    {
        var tree = new TreeView { PageOverlap = 3 };

        tree.PageOverlap.ShouldBe(3);
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
        eventArgs.IsHandled.ShouldBeFalse();
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

    /// <summary>Verifies programmatic selection rejects a null item.</summary>
    [Fact]
    public void SelectItem_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentNullException>(() => tree.SelectItem(null!));
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

    /// <summary>Verifies ExpandedChanged event args carry the committed state.</summary>
    [Fact]
    public void ExpandedChanged_WhenToggled_EventArgsCarryCommittedState()
    {
        var item = new TreeViewItem { Header = "Parent" };
        List<bool> captured = [];
        item.ExpandedChanged += (_, eventArgs) => captured.Add(eventArgs.IsExpanded);

        item.IsExpanded = false;
        item.IsExpanded = true;

        captured.ShouldBe([false, true]);
    }

    /// <summary>Verifies a Collapsed item drops its own row and its entire subtree from
    /// realization, retains selection on an unreachable descendant, and fully recovers when
    /// restored to IsVisible.</summary>
    [Fact]
    public void Visibility_WhenParentCollapsed_RemovesOwnRowAndSubtreeFromRealization()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent" };
        var child = new TreeViewItem { Header = "Child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);

        tree.SelectItem(child);
        tree.SelectedItem.ShouldBeSameAs(child);

        parent.Visibility = Visibility.Collapsed;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBeEmpty();
        tree.SelectedItem.ShouldBeSameAs(child);

        parent.Visibility = Visibility.Visible;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([parent, child]);
        tree.SelectedItem.ShouldBeSameAs(child);
    }

    /// <summary>Verifies a Hidden item keeps its own row realized but excludes its descendants,
    /// which is what distinguishes it from Collapsed - the realized count differs by exactly the
    /// parent's own row.</summary>
    [Fact]
    public void Visibility_WhenParentHidden_KeepsOwnRowButExcludesDescendantsFromRealization()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent" };
        var child = new TreeViewItem { Header = "Child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);

        parent.Visibility = Visibility.Hidden;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([parent]);

        parent.Visibility = Visibility.Collapsed;

        OwnedTree.FindAll<TreeViewItem>(tree)
            .ShouldBeEmpty("Collapsed removes the parent's own row too, unlike Hidden");
    }

    /// <summary>Regression test: before the visibility notification hook existed, setting
    /// Visibility alone - with no accompanying IsExpanded or IsEnabled change - never rebuilt the
    /// flat list at all.</summary>
    [Fact]
    public void Visibility_WhenSetAlone_TriggersRebuildWithNoExpandedOrEnabledChange()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent" };
        var child = new TreeViewItem { Header = "Child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([parent, child]);

        parent.Visibility = Visibility.Collapsed;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBeEmpty();
    }

    /// <summary>Verifies visibility changes made inside a Begin/EndUpdate batch are applied as one
    /// rebuild at EndUpdate, the same deferral IsExpanded and structural edits already get, instead
    /// of one rebuild per changed item.</summary>
    [Fact]
    public void BeginUpdate_WhenVisibilityChangesDuringABatch_DefersTheRebuildUntilEndUpdate()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };
        tree.Items.Add(a);
        tree.Items.Add(b);
        tree.Items.Add(c);

        tree.BeginUpdate();
        a.Visibility = Visibility.Collapsed;
        b.Visibility = Visibility.Collapsed;
        c.Visibility = Visibility.Hidden;

        OwnedTree.FindAll<TreeViewItem>(tree)
            .ShouldBe([a, b, c], "no rebuild has run yet, so realization is untouched mid-batch");

        tree.EndUpdate();

        OwnedTree.FindAll<TreeViewItem>(tree)
            .ShouldBe([c], "the single rebuild at EndUpdate applies all three changes together");
    }

    /// <summary>Verifies enabled changes made inside a Begin/EndUpdate batch are applied as one
    /// rebuild at EndUpdate, the same deferral Visibility and structural edits already get, instead
    /// of one rebuild per changed item.</summary>
    [Fact]
    public void BeginUpdate_WhenEnabledChangesDuringABatch_DefersTheRebuildUntilEndUpdate()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };
        tree.Items.Add(a);
        tree.Items.Add(b);
        tree.Items.Add(c);

        tree.OwnedItemsWalkCount = 0;

        tree.BeginUpdate();
        a.IsEnabled = false;
        b.IsEnabled = false;
        c.IsEnabled = false;

        tree.OwnedItemsWalkCount.ShouldBe(0, "no rebuild has run yet, so realization is untouched mid-batch");
        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([a, b, c]);

        tree.EndUpdate();

        tree.OwnedItemsWalkCount.ShouldBe(1, "the single rebuild at EndUpdate applies all three changes together");
        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([a, b, c]);
        a.EffectiveIsEnabled.ShouldBeFalse();
        b.EffectiveIsEnabled.ShouldBeFalse();
        c.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies realization tracks each Visibility transition independently of the
    /// logical model - a collapsed subtree empties out of realization while every item stays in
    /// its owning Items/Children collection, and both sides recover together.</summary>
    [Fact]
    public void Visibility_WhenParentTransitionsThroughCollapsed_TracksRealizationSeparatelyFromLogicalOwnership()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent" };
        var child = new TreeViewItem { Header = "Child" };
        var grandchild = new TreeViewItem { Header = "Grandchild" };
        child.Children.Add(grandchild);
        parent.Children.Add(child);
        tree.Items.Add(parent);

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([parent, child, grandchild]);

        parent.Visibility = Visibility.Collapsed;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBeEmpty();
        tree.Items.Count.ShouldBe(1);
        tree.Items[0].ShouldBeSameAs(parent);
        parent.Children.Count.ShouldBe(1);
        parent.Children[0].ShouldBeSameAs(child);
        child.Children.Count.ShouldBe(1);
        child.Children[0].ShouldBeSameAs(grandchild);

        parent.Visibility = Visibility.Visible;

        OwnedTree.FindAll<TreeViewItem>(tree).ShouldBe([parent, child, grandchild]);
    }

    /// <summary>Verifies a selected grandchild keeps its selection when an ancestor collapses, and
    /// that Home, End, Up, and Down all land only on items still realized - never on a row the
    /// collapse removed.</summary>
    [Fact]
    public async Task Dispatch_WhenAncestorOfSelectedItemCollapses_KeyboardNavigationNeverLandsOnUnrealizedItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var parent = new TreeViewItem { Header = "Parent" };
            var child = new TreeViewItem { Header = "Child" };
            var grandchild = new TreeViewItem { Header = "Grandchild" };
            child.Children.Add(grandchild);
            parent.Children.Add(child);
            var sibling = new TreeViewItem { Header = "Sibling" };
            tree.Items.Add(parent);
            tree.Items.Add(sibling);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            tree.SelectItem(grandchild);
            tree.SelectedItem.ShouldBeSameAs(grandchild);

            parent.Visibility = Visibility.Collapsed;

            tree.SelectedItem.ShouldBeSameAs(grandchild);

            var home = new KeyEventArgs(new Stroke(
                Code.Home, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, home);
            tree.SelectedItem.ShouldBeSameAs(sibling);

            var end = new KeyEventArgs(new Stroke(
                Code.End, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, end);
            tree.SelectedItem.ShouldBeSameAs(sibling);

            var up = new KeyEventArgs(new Stroke(
                Code.Up, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, up);
            tree.SelectedItem.ShouldBeSameAs(sibling);

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down);
            tree.SelectedItem.ShouldBeSameAs(sibling);
        }, TestContext.Current.CancellationToken);
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

    /// <summary>Verifies ExpandAll skips a branch whose children have never been requested - it
    /// never promised to fire a remote load - leaving it collapsed and Unloaded without issuing a
    /// request, distinct from an eagerly authored branch, which it does expand.</summary>
    [Fact]
    public void ExpandAll_WhenBranchIsUnloaded_SkipsItWithoutTriggeringALoad()
    {
        var source = new FakeTreeViewChildSource();
        var tree = new TreeView();
        var loaded = new TreeViewItem { Header = "Loaded", IsExpanded = false };
        loaded.Children.Add(new TreeViewItem { Header = "Child" });
        var unloaded = new TreeViewItem { Header = "Unloaded", ChildSource = source, IsExpanded = false };
        tree.Items.Add(loaded);
        tree.Items.Add(unloaded);

        tree.ExpandAll();

        loaded.IsExpanded.ShouldBeTrue();
        unloaded.IsExpanded.ShouldBeFalse();
        unloaded.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        source.Requests.ShouldBeEmpty();
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

            down1.IsHandled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(a);

            // Second Down moves to the next item.
            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);

            down2.IsHandled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(b);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies directional navigation skips a Collapsed item exactly like a disabled one -
    /// CollectVisibleItems gates on EffectiveIsVisible, which a Collapsed item never satisfies.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_SkipsCollapsedItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            var collapsed = new TreeViewItem { Header = "Collapsed", Visibility = Visibility.Collapsed };
            var c = new TreeViewItem { Header = "C" };
            tree.Items.Add(a);
            tree.Items.Add(collapsed);
            tree.Items.Add(c);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var down1 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down1);
            tree.SelectedItem.ShouldBeSameAs(a);

            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);

            // Skips the Collapsed item entirely and lands on C.
            tree.SelectedItem.ShouldBeSameAs(c);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies directional navigation skips a Hidden item too - Hidden's usual "keeps its
    /// slot, only excludes render/input" leaf contract still fails TreeView's own
    /// EffectiveIsVisible-based eligibility gate, matching Collapsed for navigation purposes.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_SkipsHiddenItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            var hidden = new TreeViewItem { Header = "Hidden", Visibility = Visibility.Hidden };
            var c = new TreeViewItem { Header = "C" };
            tree.Items.Add(a);
            tree.Items.Add(hidden);
            tree.Items.Add(c);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var down1 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down1);
            tree.SelectedItem.ShouldBeSameAs(a);

            var down2 = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, down2);

            tree.SelectedItem.ShouldBeSameAs(c);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Collapsed top-level TreeViewItem contributes no row to the tree's own
    /// measured content height - the flattened items host is a plain vertical Stack, so this is an
    /// end-to-end integration proof through TreeView's own public surface, not a re-proof of Stack
    /// itself.</summary>
    [Fact]
    public void Measure_WhenTopLevelItemIsCollapsed_ContributesNoRow()
    {
        var tree = new TreeView();
        var a = new TreeViewItem { Header = "A" };
        var b = new TreeViewItem { Header = "B" };
        var c = new TreeViewItem { Header = "C" };
        tree.Items.Add(a);
        tree.Items.Add(b);
        tree.Items.Add(c);
        var engine = new LayoutEngine();
        var size = new Size(20, 10);
        engine.Layout(tree, size);
        var baselineHeight = tree.Extent.Height;

        b.Visibility = Visibility.Collapsed;
        engine.Layout(tree, size);

        tree.Extent.Height.ShouldBe(baselineHeight - 1);
    }

    /// <summary>Verifies an incidental Control modifier on Enter still applies selection but does
    /// not raise ItemInvoked - only the invocation is gated, not the selection carve-out.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifier_SelectsButDoesNotInvokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            tree.Items.Add(a);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();
            List<string> invoked = [];
            tree.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item.Header);

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            tree.SelectedItem.ShouldBeSameAs(a);
            invoked.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still invokes.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifier_StillInvokesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var a = new TreeViewItem { Header = "A" };
            tree.Items.Add(a);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();
            List<string> invoked = [];
            tree.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Item.Header);

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            invoked.ShouldBe(["A"]);
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

            left1.IsHandled.ShouldBeTrue();
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

            left2.IsHandled.ShouldBeTrue();
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

            right1.IsHandled.ShouldBeTrue();
            parent.IsExpanded.ShouldBeTrue();

            // Right on an already expanded parent navigates to the first child.
            var right2 = new KeyEventArgs(new Stroke(
                Code.Right, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, right2);

            right2.IsHandled.ShouldBeTrue();
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
            parent.IsExpanded = false;
            var selectionChanged = 0;
            tree.SelectionChanged += (_, _) => selectionChanged++;

            first.IsEnabled = false;

            tree.SelectedItems.ShouldBe([parent, first, second]);
            tree.SelectedItem.ShouldBeSameAs(parent);
            selectionChanged.ShouldBe(0);
            first.IsSelected.ShouldBeTrue();
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

    /// <summary>Verifies a Shift-select degrades to a plain re-anchoring click, instead of silently
    /// no-opping, once collapsing an ancestor has hidden the current selection anchor.</summary>
    [Fact]
    public async Task ShiftSelect_WhenAncestorOfAnchorCollapses_DoesNotSilentlyNoOpAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
            var parent = new TreeViewItem { Header = "Parent" };
            var child1 = new TreeViewItem { Header = "Child1" };
            var child2 = new TreeViewItem { Header = "Child2" };
            parent.Children.Add(child1);
            parent.Children.Add(child2);
            var sibling = new TreeViewItem { Header = "Sibling" };
            tree.Items.Add(parent);
            tree.Items.Add(sibling);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);

            tree.SelectItem(child1);
            parent.IsExpanded = false;

            tree.NotifyItemInvoked(sibling, ActivationCause.Pointer, Modifiers.Shift);

            tree.SelectedItems.ShouldBe([sibling]);
            tree.SelectedItem.ShouldBeSameAs(sibling);
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
        var change = changes.ShouldHaveSingleItem();
        change.PreviousOffset.ShouldBe(new Point(0, 0));
        change.Offset.ShouldBe(new Point(0, 3));
        change.Cause.ShouldBe(ScrollCause.Programmatic);
    }

    /// <summary>Verifies ScrollBy reports no movement, and raises no ScrollChanged, once the
    /// viewport is already saturated at the requested end.</summary>
    [Fact]
    public void ScrollBy_WhenAlreadyAtSaturatedEndpoint_ReturnsFalseWithoutRaisingScrollChanged()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));
        var changes = 0;
        tree.ScrollChanged += (_, _) => changes++;

        var moved = tree.ScrollBy(0, -1);

        moved.ShouldBeFalse();
        tree.VerticalOffset.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies ScrollBy propagates the composed viewport's own cause validation.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.ScrollBy(0, 1, (ScrollCause) 99));
    }

    /// <summary>Verifies VerticalOffset defaults to zero and round-trips a directly assigned
    /// in-range value, without requiring a caller to go through ScrollBy.</summary>
    [Fact]
    public void VerticalOffset_WhenAssignedDirectly_DefaultsToZeroAndRoundTrips()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));

        tree.VerticalOffset.ShouldBe(0);

        tree.VerticalOffset = 5;

        tree.VerticalOffset.ShouldBe(5);
    }

    /// <summary>Verifies VerticalOffset rejects a value outside the generated scroll container's
    /// current extent, and leaves the previously committed offset unchanged.</summary>
    [Fact]
    public void VerticalOffset_WhenOutsideExtent_ThrowsArgumentOutOfRangeExceptionAndPreservesOffset()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));
        tree.VerticalOffset = 3;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.VerticalOffset = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.VerticalOffset = 9999);

        tree.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies HorizontalOffset defaults to zero and rejects any nonzero value, because
    /// the generated scroll container only ever enables vertical scrolling for a tree.</summary>
    [Fact]
    public void HorizontalOffset_WhenSetToNonZeroValue_ThrowsArgumentOutOfRangeException()
    {
        var tree = new TreeView();

        for (var index = 0; index < 20; index++)
        {
            tree.Items.Add(new TreeViewItem { Header = $"Item {index}" });
        }

        new LayoutEngine().Layout(tree, new Size(10, 4));
        tree.HorizontalOffset.ShouldBe(0);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.HorizontalOffset = 1);

        tree.HorizontalOffset.ShouldBe(0);
        tree.HorizontalOffset = 0;
        tree.HorizontalOffset.ShouldBe(0);
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

    /// <summary>Verifies BringItemIntoView rejects an owned item that is not currently realized as
    /// a visible descendant - here, a child of a collapsed parent - the same way the underlying
    /// container rejects a genuinely foreign control.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsNotRealized_ThrowsArgumentException()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "Parent", IsExpanded = false };
        var child = new TreeViewItem { Header = "Child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);
        new LayoutEngine().Layout(tree, new Size(10, 4));

        _ = Should.Throw<ArgumentException>(() => tree.BringItemIntoView(child));
    }

    /// <summary>Verifies BringItemIntoView leaves the offset untouched and still reports true once
    /// the requested item is already entirely inside the viewport.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsAlreadyFullyVisible_ReturnsTrueWithoutMovingOffset()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "First" };
        tree.Items.Add(first);
        new LayoutEngine().Layout(tree, new Size(10, 4));

        var moved = tree.BringItemIntoView(first);

        moved.ShouldBeTrue();
        tree.VerticalOffset.ShouldBe(0);
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

    /// <summary>Verifies the header constructor and property both reject a null value, and a
    /// fresh item defaults to an empty header.</summary>
    [Fact]
    public void Header_WhenAssignedNullOrUnset_ThrowsOrDefaultsToEmpty()
    {
        _ = Should.Throw<ArgumentNullException>(() => new TreeViewItem(null!));

        var item = new TreeViewItem();
        item.Header.ShouldBe(string.Empty);

        _ = Should.Throw<ArgumentNullException>(() => item.Header = null!);

        item.Header.ShouldBe(string.Empty);
    }

    /// <summary>Verifies a fresh tree carries no local style, resolving to the code-owned default.</summary>
    [Fact]
    public void Style_WhenUnassigned_ResolvesToDefault()
    {
        var tree = new TreeView();

        tree.Style.ShouldBeNull();
        tree.ActualStyle.LoadingGlyph.ShouldBe(TreeViewStyle.Default.LoadingGlyph);
    }

    /// <summary>Verifies a local Style overrides the code-owned status glyphs, and clearing it
    /// returns ownership to the theme-resolved default.</summary>
    [Fact]
    public void Style_WhenAssigned_OverridesStatusGlyphsAndClearingRestoresTheResolvedOne()
    {
        var tree = new TreeView();
        var defaultLoadingGlyph = tree.ActualStyle.LoadingGlyph;

        tree.Style = TreeViewStyle.Default with { LoadingGlyph = new Rune('~') };

        _ = tree.Style.ShouldNotBeNull();
        tree.ActualStyle.LoadingGlyph.ShouldBe(new Rune('~'));

        tree.Style = null;

        tree.Style.ShouldBeNull();
        tree.ActualStyle.LoadingGlyph.ShouldBe(defaultLoadingGlyph);
    }

    /// <summary>
    /// Verifies a tree defaults to the same bracket mark a standalone CheckBox uses, so the two
    /// controls no longer disagree about what an unconfigured check mark looks like.
    /// </summary>
    [Fact]
    public void ActualCheckMark_WhenUnassigned_IsTheBracketFamily()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        tree.Items.Add(item);

        tree.CheckMark.ShouldBeNull();
        item.CheckMark.ShouldBeNull();
        tree.ActualCheckMark.ShouldBe(CheckMark.Brackets);
        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
        item.ActualCheckMark.Width.ShouldBe(3);
        default(CheckMark).ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies precedence runs local item, then owning tree, then library default.</summary>
    [Fact]
    public void ActualCheckMark_WhenTreeAndItemBothAssign_PrefersTheItem()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var inherited = new TreeViewItem { Header = "a", IsCheckable = true };
        var overridden = new TreeViewItem { Header = "b", IsCheckable = true, CheckMark = CheckMark.Brackets };
        tree.Items.Add(inherited);
        tree.Items.Add(overridden);

        inherited.ActualCheckMark.ShouldBe(CheckMark.Tick);
        overridden.ActualCheckMark.ShouldBe(CheckMark.Brackets);

        overridden.CheckMark = null;

        overridden.ActualCheckMark.ShouldBe(CheckMark.Tick);
    }

    /// <summary>Verifies a detached item still resolves the library default.</summary>
    [Fact]
    public void ActualCheckMark_WhenItemIsDetached_ResolvesTheLibraryDefault()
    {
        var item = new TreeViewItem { Header = "a", IsCheckable = true };

        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies a width change invalidates measure while a glyph change does not.</summary>
    [Fact]
    public void CheckMark_WhenWidthChanges_InvalidatesMeasure()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        tree.Items.Add(item);
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        item.CheckMark = CheckMark.Brackets;

        changed.ShouldContain(nameof(TreeViewItem.CheckMark));
        item.Pending.ShouldNotBe(Invalidation.None);
        item.ActualCheckMark.Width.ShouldBe(3);
    }

    /// <summary>Verifies assigning the same mark publishes nothing.</summary>
    [Fact]
    public void CheckMark_WhenUnchanged_RaisesNothing()
    {
        var item = new TreeViewItem { Header = "a", IsCheckable = true, CheckMark = CheckMark.Tick };
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        item.CheckMark = CheckMark.Tick;

        changed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies the glyph projection publishes a notification, which the previous glyph-only
    /// surface never did because it bypassed the standard mutation path.
    /// </summary>
    [Fact]
    public void CheckGlyphs_WhenAssigned_RaisesPropertyChangedAndKeepsTheMarkLayout()
    {
        var item = new TreeViewItem { Header = "a", IsCheckable = true, CheckMark = CheckMark.Brackets };
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        var glyphs = new CheckBoxGlyphs(new Rune('.'), new Rune('x'), new Rune('-'));

        item.CheckGlyphs = glyphs;

        changed.ShouldContain(nameof(TreeViewItem.CheckGlyphs));
        item.CheckGlyphs.ShouldBe(glyphs);

        // The projection replaces glyphs only; the three-cell bracket layout survives.
        item.ActualCheckMark.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
        item.ActualCheckMark.Width.ShouldBe(3);
    }

    /// <summary>Verifies the glyph projection reports the resolved glyphs rather than a local copy.</summary>
    [Fact]
    public void CheckGlyphs_WhenTreeSuppliesTheMark_ReportsTheResolvedGlyphs()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        tree.Items.Add(item);

        item.CheckGlyphs.ShouldBe(CheckMark.Tick.Glyphs);
    }

    /// <summary>Verifies an invalid glyph is rejected before any state changes.</summary>
    [Fact]
    public void CheckGlyphs_WhenGlyphIsNotOneCell_Throws()
    {
        var item = new TreeViewItem { Header = "a", IsCheckable = true };

        _ = Should.Throw<ArgumentException>(
            () => item.CheckGlyphs = new CheckBoxGlyphs(new Rune('\u4f60'), new Rune('x'), new Rune('-')));
        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies an undefined mark family is rejected by the shared value type.</summary>
    [Fact]
    public void CheckMark_WhenMarkStyleIsUndefined_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new CheckMark((CheckBoxMarkStyle) 42, CheckBoxGlyphs.Default));

    /// <summary>
    /// Verifies the measured width matches what the row actually draws for every mark family and
    /// for a non-checkable row, which previously disagreed by one cell.
    /// </summary>
    /// <param name="checkable">Whether the row draws a mark.</param>
    /// <param name="reserved">The expected gap plus mark reservation in cells.</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 4)]
    public void MeasureOverride_WhenRowIsMeasured_MatchesTheRenderedLayout(bool checkable, int reserved)
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "abc", IsCheckable = checkable };
        tree.Items.Add(item);

        item.Measure(new Constraint(80, 1));

        // indent(0) + disclosure(1) + [gap(1) + mark] + leading space(1) + header(3)
        item.DesiredSize.Width.ShouldBe(1 + reserved + 1 + 3);
    }

    /// <summary>Verifies a pointer press lands on the disclosure glyph or the check mark at
    /// exactly the columns OnRenderContent draws them at, matching the hit-zone arithmetic in
    /// OnEvent cell-for-cell against the render arithmetic rather than merely against the total
    /// measured width (see MeasureOverride_WhenRowIsMeasured_MatchesTheRenderedLayout above).
    /// </summary>
    [Fact]
    public void OnEvent_WhenPressedAtRenderedGlyphColumns_TogglesExpansionAndCheckState()
    {
        var tree = new TreeView { Bounds = new Rect(0, 0, 20, 3) };
        var item = new TreeViewItem { Header = "abc", IsCheckable = true };
        item.Children.Add(new TreeViewItem { Header = "child" });
        tree.Items.Add(item);
        new LayoutEngine().Layout(tree, new Size(20, 3));

        // Column layout: [disclosure][gap][check mark (3 cells)][leading space]header
        var glyphX = item.ContentBounds.X;
        var checkX = glyphX + 1 + 1;

        item.IsExpanded.ShouldBeTrue();
        _ = Press(item, glyphX);
        item.IsExpanded.ShouldBeFalse();

        _ = Press(item, checkX + 2);
        item.IsChecked.ShouldBe(true);

        return;

        static RouteResult Press(TreeViewItem target, int x) =>
            Router.Route(
                target,
                Events.Pointer,
                new PointerEventArgs(new Pointer(
                    new Point(x, target.Bounds.Y),
                    pixels: null,
                    Buttons.Primary,
                    PointerAction.Press,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: false,
                    isCellPositionInferred: false)));
    }

    /// <summary>Verifies a one-cell family reserves correspondingly less than the bracket default.</summary>
    [Fact]
    public void MeasureOverride_WhenMarkIsOneCell_ReservesTwoFewerCells()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var item = new TreeViewItem { Header = "abc", IsCheckable = true };
        tree.Items.Add(item);

        item.Measure(new Constraint(80, 1));

        // indent(0) + disclosure(1) + gap(1) + mark(1) + leading space(1) + header(3)
        item.DesiredSize.Width.ShouldBe(1 + 1 + 1 + 1 + 3);
    }

    /// <summary>Verifies a fresh tree defaults to a two-cell indentation per nesting level.</summary>
    [Fact]
    public void Indent_WhenCreated_DefaultsToTwo()
    {
        var tree = new TreeView();

        tree.Indent.ShouldBe(2);
    }

    /// <summary>Verifies a negative indent is rejected before mutation.</summary>
    [Fact]
    public void Indent_WhenSetToNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.Indent = -1);

        tree.Indent.ShouldBe(2);
    }

    /// <summary>Verifies a configured indent widens a nested row's measured width by exactly the
    /// configured amount per nesting level, matching what OnRenderContent draws.</summary>
    [Fact]
    public void Indent_WhenConfigured_WidensNestedRowsByTheConfiguredAmount()
    {
        var tree = new TreeView { Indent = 5 };
        var parent = new TreeViewItem { Header = "a" };
        var child = new TreeViewItem { Header = "a" };
        parent.Children.Add(child);
        tree.Items.Add(parent);
        new LayoutEngine().Layout(tree, new Size(40, 4));

        child.Depth.ShouldBe(parent.Depth + 1);
        child.DesiredSize.Width.ShouldBe(parent.DesiredSize.Width + 5);
    }

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
        first.IsEnabled = false;

        tree.SetSelected(first, true).ShouldBeFalse();
        tree.SelectedItems.ShouldBeEmpty();
    }

    /// <summary>Verifies TreeView proves direct and ancestor-inherited disabled state at the
    /// detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled - the
    /// same disabled contract exercised on a live mounted terminal surface.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenTreeIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        var tree = new TreeView();
        var host = new Stack();
        host.Children.Add(tree);

        tree.IsEnabled = false;
        tree.EffectiveIsEnabled.ShouldBeFalse();

        tree.IsEnabled = true;
        tree.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        tree.IsEnabled.ShouldBeTrue();
        tree.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        tree.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies TreeViewItem proves direct and owning-tree-inherited disabled state at
    /// the detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenItemIsDisabledDirectlyOrByOwningTree_ReportsDisabledAndRecovers()
    {
        var tree = Build(out var first, out _, out _);

        first.IsEnabled = false;
        first.EffectiveIsEnabled.ShouldBeFalse();

        first.IsEnabled = true;
        first.EffectiveIsEnabled.ShouldBeTrue();

        tree.IsEnabled = false;
        first.IsEnabled.ShouldBeTrue();
        first.EffectiveIsEnabled.ShouldBeFalse();

        tree.IsEnabled = true;
        first.EffectiveIsEnabled.ShouldBeTrue();
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

        host.IsEnabled = false;
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

        host.IsEnabled = false;
        first.IsExpanded = false;

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

    /// <summary>Verifies SelectAll refuses to run outside Multiple selection mode, since a single
    /// or none-selection tree has no way to represent the result, and leaves selection state
    /// untouched.</summary>
    [Theory]
    [InlineData(TreeSelectionMode.Single)]
    [InlineData(TreeSelectionMode.None)]
    public void SelectAll_WhenModeIsNotMultiple_ThrowsInvalidOperationExceptionAndPreservesSelection(
        TreeSelectionMode mode)
    {
        var tree = Build(out var first, out _, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        tree.SelectItem(first);
        tree.SelectionMode = mode;

        _ = Should.Throw<InvalidOperationException>(tree.SelectAll);

        tree.SelectedItems.ShouldBe(mode == TreeSelectionMode.Single ? [first] : []);
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
        first = new TreeViewItem { Header = "first", IsExpanded = true };
        child = new TreeViewItem { Header = "child" };
        second = new TreeViewItem { Header = "second" };
        first.Children.Add(child);
        tree.Items.Add(first);
        tree.Items.Add(second);

        return tree;
    }
}
