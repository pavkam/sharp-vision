// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies hierarchical tree view ownership, selection, expand/collapse, and keyboard navigation.</summary>
public sealed class TreeViewTests
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

    /// <summary>Verifies assigning either current SelectedItem value from outside an attached
    /// tree's dispatcher rejects the mutation before the equality no-op.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedItem_WhenAttachedAndAssignedCurrentValueOffDispatcher_ThrowsAsync(bool selected)
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var item = new TreeViewItem("Item");
        var tree = new TreeView { Items = { item } };

        if (selected)
        {
            tree.SelectedItem = item;
        }

        await dispatcher.InvokeAsync(
            () => tree.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        var current = tree.SelectedItem;

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => tree.SelectedItem = current);
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

    /// <summary>Verifies direct item disposal removes semantic ownership and releases descendants.</summary>
    [Fact]
    public void Item_WhenDisposedDirectly_RemovesSemanticEntryAndReleasesChildren()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "parent" };
        var child = new TreeViewItem { Header = "child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);
        tree.SelectedItem = parent;

        parent.Dispose();

        tree.Items.ShouldBeEmpty();
        tree.SelectedItem.ShouldBeNull();
        child.Parent.ShouldBeNull();
        child.ParentCollection.ShouldBeNull();
    }

    /// <summary>Verifies direct nested-item disposal removes the exact child and its selection identity.</summary>
    [Fact]
    public void NestedItem_WhenDisposedDirectly_RemovesChildAndSelection()
    {
        var tree = new TreeView();
        var parent = new TreeViewItem { Header = "parent" };
        var child = new TreeViewItem { Header = "child" };
        parent.Children.Add(child);
        tree.Items.Add(parent);
        tree.SelectedItem = child;

        child.Dispose();

        parent.Children.ShouldBeEmpty();
        tree.SelectedItem.ShouldBeNull();
        child.ParentCollection.ShouldBeNull();
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

    /// <summary>Verifies selection property reentry suppresses the superseded tree delta.</summary>
    [Theory]
    [InlineData(nameof(TreeView.SelectedItem))]
    [InlineData(nameof(TreeView.SelectedItems))]
    public void SelectItem_WhenSelectionPropertyObserverReenters_PublishesOnlyCurrentTypedEvent(
        string propertyName)
    {
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        var third = new TreeViewItem { Header = "Third" };
        var tree = new TreeView { Items = { first, second, third } };
        var events = new List<TreeViewSelectionChangedEventArgs>();
        tree.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == propertyName && ReferenceEquals(tree.SelectedItem, second))
            {
                tree.SelectItem(third);
            }
        };
        tree.SelectionChanged += (_, eventArgs) => events.Add(eventArgs);

        tree.SelectItem(second);

        tree.SelectedItem.ShouldBeSameAs(third);
        events.Count.ShouldBe(1);
        events[0].CurrentItem.ShouldBeSameAs(third);
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

    /// <summary>Verifies an ExpandedChanged subscriber that disposes the owning tree cannot reach
    /// a structural rebuild against the now-disposed tree - the item itself is left untouched by
    /// the tree's dispose, so it still finds and calls back into the disposed tree.</summary>
    [Fact]
    public void IsExpanded_WhenExpandedChangedDisposesTree_DoesNotThrow()
    {
        var item = new TreeViewItem { Header = "Parent", IsExpanded = false };
        item.Children.Add(new TreeViewItem { Header = "Child" });
        var tree = new TreeView { Items = { item } };
        item.ExpandedChanged += (_, _) => tree.Dispose();

        _ = Should.NotThrow(() => item.IsExpanded = true);

        tree.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies a newer expansion committed by the public callback owns all structural
    /// and loading work, so the superseded outer transition cannot start a hidden request.</summary>
    [Fact]
    public async Task IsExpanded_WhenExpandedChangedCollapsesAgain_DoesNotStartSupersededLoadAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);
            item.ExpandedChanged += (_, eventArgs) =>
            {
                if (eventArgs.IsExpanded)
                {
                    item.IsExpanded = false;
                }
            };

            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        item.IsExpanded.ShouldBeFalse();
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        source.Requests.ShouldBeEmpty();
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

    /// <summary>Verifies adding a child publishes the effective aggregate check transition once.</summary>
    [Fact]
    public void Children_WhenAddedChildChangesAggregateCheckState_NotifiesParent()
    {
        // Arrange
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        parent.Children.Add(new TreeViewItem { Header = "First", IsCheckable = true, IsChecked = true });
        var changes = new List<CheckChangedEventArgs>();
        var propertyChanges = 0;
        parent.CheckStateChanged += (_, eventArgs) => changes.Add(eventArgs);
        parent.PropertyChanged += (_, eventArgs) =>
            propertyChanges += eventArgs.PropertyName == nameof(TreeViewItem.IsChecked) ? 1 : 0;

        // Act
        parent.Children.Add(new TreeViewItem { Header = "Second", IsCheckable = true, IsChecked = false });

        // Assert
        parent.IsChecked.ShouldBeNull();
        propertyChanges.ShouldBe(1);
        var change = changes.ShouldHaveSingleItem();
        change.Previous.ShouldBe(true);
        change.Current.ShouldBeNull();
    }

    /// <summary>Verifies removing a child publishes the effective aggregate check transition once.</summary>
    [Fact]
    public void Children_WhenRemovedChildChangesAggregateCheckState_NotifiesParent()
    {
        // Arrange
        var first = new TreeViewItem { Header = "First", IsCheckable = true, IsChecked = true };
        var second = new TreeViewItem { Header = "Second", IsCheckable = true, IsChecked = false };
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true, Children = { first, second } };
        var changes = new List<CheckChangedEventArgs>();
        parent.CheckStateChanged += (_, eventArgs) => changes.Add(eventArgs);

        // Act
        _ = parent.Children.Remove(second);

        // Assert
        parent.IsChecked.ShouldBe(true);
        var change = changes.ShouldHaveSingleItem();
        change.Previous.ShouldBeNull();
        change.Current.ShouldBe(true);
    }

    /// <summary>Verifies replacing a child uses the same aggregate check-state transaction as
    /// insertion and removal.</summary>
    [Fact]
    public void Children_WhenReplacementChangesAggregateCheckState_NotifiesParent()
    {
        // Arrange
        var first = new TreeViewItem { Header = "First", IsCheckable = true, IsChecked = true };
        var second = new TreeViewItem { Header = "Second", IsCheckable = true, IsChecked = false };
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true, Children = { first, second } };
        var changes = new List<CheckChangedEventArgs>();
        parent.CheckStateChanged += (_, eventArgs) => changes.Add(eventArgs);

        // Act
        parent.Children[1] = new TreeViewItem { Header = "Replacement", IsCheckable = true, IsChecked = true };

        // Assert
        parent.IsChecked.ShouldBe(true);
        var change = changes.ShouldHaveSingleItem();
        change.Previous.ShouldBeNull();
        change.Current.ShouldBe(true);
    }

    /// <summary>Verifies the first-child state callback observes the child in the flattened tree.</summary>
    [Fact]
    public void Children_WhenFirstChildMakesParentLoaded_RealizesBeforeStateCallback()
    {
        // Arrange
        var tree = new TreeView();
        var parent = new TreeViewItem("Parent");
        var child = new TreeViewItem("Child");
        tree.Items.Add(parent);
        parent.ChildStateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Current == TreeViewChildState.Loaded)
            {
                tree.BringItemIntoView(child).ShouldBeTrue();
            }
        };

        // Act and assert
        Should.NotThrow(() => parent.Children.Add(child));
    }

    /// <summary>Verifies a child-state transition and its structural realization share one rebuild.</summary>
    [Fact]
    public void Children_WhenFirstChildChangesParentState_RebuildsOwnedTreeOnce()
    {
        // Arrange
        var tree = new TreeView();
        var parent = new TreeViewItem("Parent");
        tree.Items.Add(parent);
        tree.OwnedItemsWalkCount = 0;

        // Act
        parent.Children.Add(new TreeViewItem("Child"));

        // Assert
        tree.OwnedItemsWalkCount.ShouldBe(1);
    }

    /// <summary>Verifies selection repair after removing the last child observes the parent's final leaf state.</summary>
    [Fact]
    public void Children_WhenSelectedLastChildIsRemoved_SelectionCallbackObservesLeafState()
    {
        // Arrange
        var child = new TreeViewItem("Child");
        var parent = new TreeViewItem("Parent") { Children = { child } };
        var tree = new TreeView { Items = { parent } };
        tree.SelectItem(child);
        TreeViewChildState? observed = null;
        tree.SelectionChanged += (_, _) => observed = parent.ChildState;

        // Act
        _ = parent.Children.Remove(child);

        // Assert
        observed.ShouldBe(TreeViewChildState.Leaf);
    }

    /// <summary>Verifies a nested checkability transition suppresses the older ancestor snapshot.</summary>
    [Fact]
    public void IsCheckable_WhenPropertyCallbackReenters_SuppressesSupersededAncestorEvent()
    {
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var child = new TreeViewItem { Header = "Child", IsCheckable = true, IsChecked = true };
        parent.Children.Add(child);
        var parentEvents = new List<CheckChangedEventArgs>();
        var reentered = false;
        child.PropertyChanged += (_, eventArgs) =>
        {
            if (!reentered && eventArgs.PropertyName == nameof(TreeViewItem.IsCheckable))
            {
                reentered = true;
                child.IsCheckable = true;
            }
        };
        parent.CheckStateChanged += (_, eventArgs) => parentEvents.Add(eventArgs);

        child.IsCheckable = false;

        child.IsCheckable.ShouldBeTrue();
        parent.IsChecked.ShouldBe(true);
        parentEvents.Select(static eventArgs => eventArgs.Current).ShouldBe([true]);
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

    /// <summary>Verifies a nested check transaction suppresses the older descendant snapshot.</summary>
    [Fact]
    public void IsChecked_WhenCheckCallbackReenters_SuppressesSupersededDescendantEvent()
    {
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var child = new TreeViewItem { Header = "Child", IsCheckable = true };
        parent.Children.Add(child);
        var childEvents = new List<CheckChangedEventArgs>();
        parent.CheckStateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Current == true)
            {
                child.IsChecked = false;
            }
        };
        child.CheckStateChanged += (_, eventArgs) => childEvents.Add(eventArgs);

        parent.IsChecked = true;

        child.IsChecked.ShouldBe(false);
        childEvents.Select(static eventArgs => eventArgs.Current).ShouldBe([false]);
    }

    /// <summary>Verifies removing any captured descendant from the first callback prevents an
    /// obsolete notification from reaching that detached item.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IsChecked_WhenPropertyCallbackRemovesCapturedChild_DoesNotNotifyDetachedItem(int index)
    {
        var parent = new TreeViewItem { Header = "Parent", IsCheckable = true };
        var children = Enumerable.Range(0, 3)
            .Select(value => new TreeViewItem { Header = value.ToString(CultureInfo.InvariantCulture), IsCheckable = true })
            .ToArray();

        foreach (var child in children)
        {
            parent.Children.Add(child);
        }

        var detachedEvents = 0;
        children[index].CheckStateChanged += (_, _) => detachedEvents++;
        parent.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TreeViewItem.IsChecked))
            {
                _ = parent.Children.Remove(children[index]);
            }
        };

        parent.IsChecked = true;

        children[index].Parent.ShouldBeNull();
        detachedEvents.ShouldBe(0);
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

    /// <summary>Verifies a disposed checkable item rejects check-state mutation before changing
    /// its effective state.</summary>
    [Fact]
    public void IsChecked_WhenItemIsDisposed_ThrowsBeforeMutation()
    {
        // Arrange
        var item = new TreeViewItem { Header = "Leaf", IsCheckable = true };
        item.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => item.IsChecked = true);
        item.IsChecked.ShouldBe(false);
    }

    /// <summary>Verifies a check-state mutation from outside the owning dispatcher is rejected
    /// before the attached item or its hierarchy can change.</summary>
    [Fact]
    public async Task IsChecked_WhenAttachedAndSetOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var item = new TreeViewItem { Header = "Leaf", IsCheckable = true };
        var tree = new TreeView { Items = { item } };
        await dispatcher.InvokeAsync(
            () => tree.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => item.IsChecked = true);
        item.IsChecked.ShouldBe(false);
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

    /// <summary>Verifies item and selection callbacks can invalidate the activated item without a
    /// later selection or tree-level invocation publishing that obsolete ownership.</summary>
    [Theory]
    [InlineData("item", false)]
    [InlineData("item", true)]
    [InlineData("changing", false)]
    [InlineData("changing", true)]
    [InlineData("changed", false)]
    [InlineData("changed", true)]
    public async Task Invocation_WhenCallbackInvalidatesItem_StopsBeforeTreeEventAsync(
        string callback,
        bool dispose)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var tree = new TreeView();
            var item = new TreeViewItem { Header = "A" };
            tree.Items.Add(item);
            tree.Attach(dispatcher);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();
            var treeInvocations = 0;
            tree.ItemInvoked += (_, _) => treeInvocations++;
            Action invalidate = dispose
                ? item.Dispose
                : () =>
                {
                    if (ReferenceEquals(item.FindTreeView(), tree))
                    {
                        tree.Items.Remove(item).ShouldBeTrue();
                    }
                };

            if (callback == "item")
            {
                item.Invoked += (_, _) => invalidate();
                var enter = new KeyEventArgs(new Stroke(
                    Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
                _ = Router.Route(tree, Events.Key, enter);
            }
            else
            {
                if (callback == "changing")
                {
                    tree.SelectionChanging += (_, _) => invalidate();
                }
                else
                {
                    tree.SelectionChanged += (_, _) => invalidate();
                }

                tree.NotifyItemInvoked(item, ActivationCause.Pointer);
            }

            treeInvocations.ShouldBe(0);
            tree.Dispose();
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

    /// <summary>Verifies the same width-graded rule fires for the theme-driven fallback, not only
    /// the local setter above: a bundled-theme swap that moves the check-box glyph family across
    /// the Brackets/Square width boundary must reach an attached item's own measure, even though
    /// neither <see cref="TreeView"/> nor <see cref="TreeViewItem"/> owns a style slot for the
    /// mark.</summary>
    [Fact]
    public void ActualCheckMark_WhenThemeChangesWidth_InvalidatesItemMeasure()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        tree.Items.Add(item);
        var previousTheme = ThemeCatalog.Parse(ThemeJson.Create());
        var currentTheme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "blocks"));
        tree.PropagateTheme(previousTheme);
        item.ActualCheckMark.Width.ShouldBe(3);
        tree.Clear(Invalidation.All);
        item.Clear(Invalidation.All);

        tree.PropagateTheme(currentTheme);

        (item.Pending & Invalidation.Measure).ShouldNotBe(Invalidation.None);
        item.ActualCheckMark.Width.ShouldBe(1);
    }

    /// <summary>Verifies the theme-driven fallback also fires for a value-only change - a swap that
    /// moves the checked/unchecked glyphs without crossing the Brackets/Square width boundary needs
    /// only a repaint, mirroring the local setter's own Render-only grading.</summary>
    [Fact]
    public void ActualCheckMark_WhenThemeChangesGlyphsOnly_InvalidatesItemRenderNotMeasure()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        tree.Items.Add(item);
        var previousTheme = ThemeCatalog.Parse(ThemeJson.Create());
        var currentTheme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "dots"));
        tree.PropagateTheme(previousTheme);
        var before = item.ActualCheckMark;
        tree.Clear(Invalidation.All);
        item.Clear(Invalidation.All);

        tree.PropagateTheme(currentTheme);

        item.ActualCheckMark.Width.ShouldBe(before.Width);
        item.ActualCheckMark.ShouldNotBe(before);
        (item.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
        (item.Pending & Invalidation.Render).ShouldNotBe(Invalidation.None);
    }

    /// <summary>Verifies a detached item - no owning tree - still notices a theme swap through its
    /// own registered dependency, since it resolves the library default directly rather than
    /// through an owner that would otherwise fan the change out.</summary>
    [Fact]
    public void ActualCheckMark_WhenDetachedItemThemeChangesWidth_InvalidatesMeasure()
    {
        var item = new TreeViewItem { Header = "a", IsCheckable = true };
        var previousTheme = ThemeCatalog.Parse(ThemeJson.Create());
        var currentTheme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "blocks"));
        item.SetTheme(previousTheme);
        item.ActualCheckMark.Width.ShouldBe(3);
        item.Clear(Invalidation.All);

        item.SetTheme(currentTheme);

        (item.Pending & Invalidation.Measure).ShouldNotBe(Invalidation.None);
        item.ActualCheckMark.Width.ShouldBe(1);
    }

    /// <summary>Verifies a throwing owner observer cannot skip invalidating retained rows for the
    /// already-committed shared check-mark presentation.</summary>
    [Fact]
    public void CheckMark_WhenPropertyObserverThrows_StillInvalidatesRetainedRows()
    {
        var item = new TreeViewItem { Header = "Item", IsCheckable = true };
        var tree = new TreeView();
        tree.Items.Add(item);
        tree.Clear(Invalidation.All);
        item.Clear(Invalidation.All);
        tree.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TreeView.CheckMark))
            {
                throw new InvalidOperationException("observer failure");
            }
        };
        var mark = new CheckMark(CheckBoxMarkStyle.Square, CheckBoxGlyphs.Default);

        _ = Should.Throw<InvalidOperationException>(() => tree.CheckMark = mark);

        tree.CheckMark.ShouldBe(mark);
        tree.ActualCheckMark.ShouldBe(mark);
        (item.Pending & Invalidation.Measure).ShouldNotBe(Invalidation.None);
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
    /// Verifies a SelectionChanging subscriber that disposes the tree synchronously mid-commit
    /// does not leave the rest of CommitSelection running against disposed-guarded members.
    /// </summary>
    [Fact]
    public void SetSelected_WhenChangingSubscriberDisposesTree_DoesNotThrow()
    {
        var tree = Build(out var first, out _, out _);
        tree.SelectionChanging += (_, _) => tree.Dispose();

        var changed = Should.NotThrow(() => tree.SetSelected(first, true));

        changed.ShouldBeTrue();
        tree.IsDisposed.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a PropertyChanged subscriber that disposes the tree between the SelectedItem and
    /// SelectedItems notifications does not leave the rest of CommitSelection running against
    /// disposed-guarded members.
    /// </summary>
    [Fact]
    public void SetSelected_WhenPropertyChangedSubscriberDisposesTree_DoesNotThrow()
    {
        var tree = Build(out var first, out _, out _);
        tree.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TreeView.SelectedItem))
            {
                tree.Dispose();
            }
        };

        var changed = Should.NotThrow(() => tree.SetSelected(first, true));

        changed.ShouldBeTrue();
        tree.IsDisposed.ShouldBeTrue();
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

    /// <summary>
    /// Verifies a SelectionChanging subscriber that reentrantly commits another selection change
    /// from inside its own handler does not let the now-superseded outer proposal reach a second
    /// subscriber registered after it on the same event - the second subscriber must only ever
    /// observe the reentrant transition that actually won.
    /// </summary>
    [Fact]
    public void SetSelected_WhenChangingSubscriberReentrantlyChangesSelection_LaterSubscriberNeverSeesSupersededProposal()
    {
        var tree = Build(out var first, out var second, out _);
        tree.SelectionMode = TreeSelectionMode.Multiple;
        List<string> secondSubscriberObservations = [];

        tree.SelectionChanging += (_, eventArgs) =>
        {
            if (eventArgs.AddedItems.Any(item => ReferenceEquals(item, second)))
            {
                _ = tree.SetSelected(first, true);
            }
        };
        tree.SelectionChanging += (_, eventArgs) =>
            secondSubscriberObservations.Add(Names(eventArgs.AddedItems));

        _ = tree.SetSelected(second, true);

        tree.SelectedItems.ShouldBe([first]);
        secondSubscriberObservations.ShouldBe(["first"]);
    }

    /// <summary>Verifies a first subscriber that reenters through item removal - rather than through
    /// another selection assignment - still stops a later subscriber from observing the now-obsolete
    /// outer proposal, proving the version-checked delivery in <c>SelectionCommit&lt;TKey&gt;</c>
    /// generalizes to any reentrant selection-version bump, not only a reentrant selection call.</summary>
    [Fact]
    public void SetSelected_WhenChangingSubscriberReentrantlyRemovesAnItem_LaterSubscriberNeverSeesSupersededProposal()
    {
        var tree = Build(out var first, out var second, out _);
        List<string> secondSubscriberObservations = [];

        tree.SelectionChanging += (_, eventArgs) =>
        {
            if (eventArgs.AddedItems.Any(item => ReferenceEquals(item, second)))
            {
                _ = tree.Items.Remove(second);
            }
        };
        tree.SelectionChanging += (_, eventArgs) =>
            secondSubscriberObservations.Add(Names(eventArgs.AddedItems));

        _ = tree.SetSelected(second, true);

        secondSubscriberObservations.ShouldBeEmpty();
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

    #region Selection caching

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

    #endregion

    #region Asynchronous child loading

    /// <summary>Verifies a loaded-state callback observes the newly committed child in the final
    /// flattened presentation rather than an intermediate loading tree.</summary>
    [Fact]
    public async Task ChildLoad_WhenLoadedCallbackBringsChildIntoView_ObservesRealizedChildAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child"));
        var root = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { root } };
        var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        root.ChildStateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Current == TreeViewChildState.Loaded)
            {
                tree.BringItemIntoView(root.Children[0]).ShouldBeTrue();
                callbackCompleted.SetResult();
            }
        };

        // Act
        await dispatcher.InvokeAsync(() => tree.Attach(dispatcher), TestContext.Current.CancellationToken);
        await callbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        root.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies a loaded child snapshot publishes the resulting aggregate check-state
    /// transition on its checkable parent.</summary>
    [Fact]
    public async Task ChildLoad_WhenMembershipChangesAggregateCheckState_NotifiesParentAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child")
        {
            IsCheckable = true,
            InitialCheckState = false
        });
        var root = new TreeViewItem("Root") { ChildSource = source, IsCheckable = true, IsChecked = true };
        var tree = new TreeView { Items = { root } };
        var changed = new TaskCompletionSource<CheckChangedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        root.CheckStateChanged += (_, eventArgs) => _ = changed.TrySetResult(eventArgs);

        // Act
        await dispatcher.InvokeAsync(() => tree.Attach(dispatcher), TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        var change = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        root.IsChecked.ShouldBe(false);
        change.Previous.ShouldBe(true);
        change.Current.ShouldBe(false);
    }

    /// <summary>Verifies the deferred attach callback from one dispatcher cannot start a request
    /// after the item migrates, while the new attachment's callback still starts exactly once.</summary>
    [Fact]
    public async Task AttachedLoad_WhenItemMigratesBeforeDeferredCallback_IgnoresPreviousDispatcherAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null);
        var item = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { item } };
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releasePrevious = new();
        using ManualResetEventSlim releaseCurrent = new();
        previousDispatcher.Post(() =>
        {
            tree.Attach(previousDispatcher);
            tree.Detach();
            detached.SetResult();
            releasePrevious.Wait();
        });
        await detached.Task.WaitAsync(TestContext.Current.CancellationToken);
        currentDispatcher.Post(() =>
        {
            tree.Attach(currentDispatcher);
            attached.SetResult();
            releaseCurrent.Wait();
        });
        await attached.Task.WaitAsync(TestContext.Current.CancellationToken);

        releasePrevious.Set();
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.ShouldBeEmpty();

        releaseCurrent.Set();
        await currentDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.ShouldBe([null]);
        await currentDispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detachment cancels an in-flight generation, reattachment starts a current
    /// request, and the ignored old completion cannot replace the new tree state.</summary>
    [Fact]
    public async Task ChildLoad_WhenItemMigratesBeforeCompletion_IgnoresPreviousGenerationAsync()
    {
        await using var previousDispatcher = Dispatcher.Start();
        await using var currentDispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var stale = source.DeferNext(null);
        source.AddChildren(null, new TreeViewChildDescription("fresh", "Fresh"));
        var item = new TreeViewItem("Root") { ChildSource = source };
        var tree = new TreeView { Items = { item } };
        await previousDispatcher.InvokeAsync(() => tree.Attach(previousDispatcher), TestContext.Current.CancellationToken);
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(1);
        await previousDispatcher.InvokeAsync(tree.Detach, TestContext.Current.CancellationToken);
        await currentDispatcher.InvokeAsync(() => tree.Attach(currentDispatcher), TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        _ = stale.TrySetResult([new TreeViewChildDescription("stale", "Stale")]);
        await previousDispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.Children.ShouldHaveSingleItem().Header.ShouldBe("Fresh");
        source.Requests.Count.ShouldBe(2);
        await currentDispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies expansion invariant work survives either public expansion observer
    /// throwing after the state commits.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsExpanded_WhenExpansionObserverThrows_StillStartsCommittedLoadAsync(
        bool throwFromPropertyObserver)
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child")
        {
            Presence = TreeViewChildPresence.Leaf
        });
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);

            if (throwFromPropertyObserver)
            {
                item.PropertyChanged += (_, eventArgs) =>
                {
                    if (eventArgs.PropertyName == nameof(TreeViewItem.IsExpanded))
                    {
                        throw new InvalidOperationException("The property observer failed.");
                    }
                };
            }
            else
            {
                item.ExpandedChanged += (_, _) =>
                    throw new InvalidOperationException("The expanded observer failed.");
            }

            _ = Should.Throw<InvalidOperationException>(() => item.IsExpanded = true);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        source.Requests.ShouldBe([null]);
        item.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies cancelling from the Loading callback cannot expose a token owned by the
    /// cancelled and disposed request to the superseded outer start path.</summary>
    [Fact]
    public async Task BeginLoad_WhenLoadingObserverClearsSource_DoesNotStartCancelledRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loading)
                {
                    item.ChildSource = null;
                }
            };

            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.ChildSource.ShouldBeNull();
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies a failing Loading observer cannot strand a committed loading state with
    /// no request behind it.</summary>
    [Fact]
    public async Task BeginLoad_WhenLoadingObserverThrows_StillStartsRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        var item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
        var tree = new TreeView { Items = { item } };

        await dispatcher.InvokeAsync(() =>
        {
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loading)
                {
                    throw new InvalidOperationException("The loading observer failed.");
                }
            };

            _ = Should.Throw<InvalidOperationException>(() => item.IsExpanded = true);
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.ShouldBe([null]);

        completion.SetResult([]);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies an item with no <see cref="TreeViewItem.ChildSource"/> and no children is
    /// a leaf, distinct from an item whose source has not yet been consulted.</summary>
    [Fact]
    public void ChildState_WhenNoChildSourceAndNoChildren_IsLeaf()
    {
        var item = new TreeViewItem("Leaf");

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies an item whose children were authored directly (never through
    /// <see cref="TreeViewItem.ChildSource"/>) reports Loaded, and HasChildren tracks whether that
    /// committed snapshot is non-empty rather than reporting true unconditionally.</summary>
    [Fact]
    public void ChildState_WhenChildrenAreAuthoredDirectly_IsLoadedAndTracksEmptiness()
    {
        var item = new TreeViewItem("Node");
        item.Children.Add(new TreeViewItem("Child"));

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.HasChildren.ShouldBeTrue();

        item.Children.Clear();

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies assigning a source moves an item to Unloaded, offering a disclosure
    /// affordance before any request has ever been made.</summary>
    [Fact]
    public void ChildSource_WhenAssigned_MovesToUnloadedAndOffersDisclosure()
    {
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Node") { ChildSource = source };

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.HasChildren.ShouldBeTrue();
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies attaching an item that started life IsExpanded (the default) with a
    /// ChildSource already assigned triggers the deferred load the moment a dispatcher becomes
    /// available, instead of leaving it Unloaded-but-IsExpanded forever.</summary>
    [Fact]
    public async Task OnAttached_WhenItemIsExpandedAndUnloaded_TriggersTheDeferredLoadAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            // ChildSource is assigned - and IsExpanded is already true, the constructor default -
            // entirely before this item ever reaches a dispatcher.
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.ShouldBe([null]);
    }

    /// <summary>Verifies re-expanding an item that is already Loading - IsExpanded already true, set
    /// to true again - is a no-op that never starts a second concurrent request.</summary>
    [Fact]
    public async Task Expanded_WhenAlreadyLoadingAndSetAgain_DoesNotStartASecondRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        _ = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = true; }, TestContext.Current.CancellationToken);

        source.Requests.Count.ShouldBe(1);
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies a load committing many children applies them as one atomic update: the
    /// Loaded transition and the full child set become observable together, never as a partial
    /// intermediate set, and the transition fires exactly once.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenManyChildrenAreReturned_AppliesAsOneAtomicUpdateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var descriptions = Enumerable.Range(0, 25)
            .Select(static index => new TreeViewChildDescription($"child-{index}", $"Child {index}")
            {
                Presence = TreeViewChildPresence.Leaf
            })
            .ToArray();
        source.AddChildren(null, descriptions);
        TreeView tree = null!;
        TreeViewItem item = null!;
        var loadedTransitions = 0;
        var observedCountAtLoaded = -1;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.ChildStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Current == TreeViewChildState.Loaded)
                {
                    loadedTransitions++;
                    observedCountAtLoaded = item.Children.Count;
                }
            };
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        // The state field commits before SetChildState finishes publishing its callbacks. Queueing
        // the observation behind that dispatcher transaction prevents an off-thread fast-path
        // predicate from asserting while the Loaded callback is still pending in the same turn.
        var (transitions, countAtTransition, finalCount) = await dispatcher.InvokeAsync(
            () => (Transitions: loadedTransitions,
                CountAtTransition: observedCountAtLoaded,
                FinalCount: item.Children.Count),
            TestContext.Current.CancellationToken);

        transitions.ShouldBe(1);
        countAtTransition.ShouldBe(
            25,
            "the full committed set must already be visible at the moment the transition fires");
        finalCount.ShouldBe(25);
    }

    /// <summary>Verifies a description that never sets Presence defaults to MayHaveChildren: the
    /// materialized child inherits the parent's ChildSource instead of becoming a Leaf, so it stays
    /// independently expandable.</summary>
    [Fact]
    public async Task Presence_WhenUnspecified_DefaultsToMayHaveChildrenAndInheritsTheSourceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("child", "Child"));
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.ChildSource.ShouldBeSameAs(source);
        child.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        child.HasChildren.ShouldBeTrue();
    }

    /// <summary>Verifies an empty successful result commits Loaded, not Leaf - only never having had
    /// a source, or an explicit <see cref="TreeViewChildPresence.Leaf"/> answer, means leaf.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultIsEmpty_BecomesLoadedNotLeafAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState != TreeViewChildState.Loading,
            TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.ShouldBeEmpty();
        item.HasChildren.ShouldBeFalse();
    }

    /// <summary>Verifies collapsing an item whose very first load is still in flight cancels the
    /// request and restores the state it had before the load started - Unloaded - and that a late
    /// completion of the cancelled request is dropped rather than committed.</summary>
    [Fact]
    public async Task Expanded_WhenSetFalseDuringFirstLoad_CancelsRestoresUnloadedAndDropsStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = false; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();

        // Deliver the stale completion, then flush the dispatcher queue once more: the fake source
        // ignores cancellation on purpose, so the request's own continuation still posts a commit
        // attempt - this proves it lands and is dropped by the generation guard, not merely that we
        // never gave it a chance to run.
        _ = deferred.TrySetResult([]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies collapsing an item mid-reload - one that already had committed children -
    /// cancels the reload and restores the prior Loaded state and its children untouched, and that
    /// the cancelled reload's late completion is dropped.</summary>
    [Fact]
    public async Task Expanded_WhenSetFalseDuringReloadInFlight_CancelsRestoresPriorLoadedStateAndDropsStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.Count.ShouldBe(1);

        var deferred = source.DeferNext(null);
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            _ = item.ReloadChildrenAsync();
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(() => { item.IsExpanded = false; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.Count.ShouldBe(1);
        item.Children[0].Header.ShouldBe("A");

        _ = deferred.TrySetResult([new TreeViewChildDescription("b", "B") { Presence = TreeViewChildPresence.Leaf }]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loaded);
        item.Children.Count.ShouldBe(1);
        item.Children[0].Header.ShouldBe("A");
    }

    /// <summary>Verifies starting a second reload while the first is still in flight supersedes it:
    /// the first request's late completion is dropped by the generation guard once the second has
    /// committed.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenAnOlderRequestCompletesAfterANewerOne_DropsTheStaleCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var stale = source.DeferNext(null);
        Task staleObservation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            _ = item.ReloadChildrenAsync();
            staleObservation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("fresh", "Fresh") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { _ = item.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.Children.Any(child => child.Header == "Fresh"),
            TestContext.Current.CancellationToken);

        _ = stale.TrySetResult([new TreeViewChildDescription("stale", "Stale") { Presence = TreeViewChildPresence.Leaf }]);
        await staleObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        _ = item.Children.ShouldHaveSingleItem();
        item.Children[0].Header.ShouldBe("Fresh");
    }

    /// <summary>Verifies reassigning a different non-null source over loader-owned children cancels
    /// any pending load, evicts and disposes the old children, and returns to Unloaded.</summary>
    [Fact]
    public async Task ChildSource_WhenReassignedToADifferentSource_EvictsDisposesAndReturnsToUnloadedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var firstSource = new FakeTreeViewChildSource();
        firstSource.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        var secondSource = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = firstSource };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        var previousChild = item.Children.ShouldHaveSingleItem();

        await dispatcher.InvokeAsync(() => { item.ChildSource = secondSource; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        item.Children.ShouldBeEmpty();
        previousChild.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies clearing a loader-owned source to null evicts and disposes the loaded
    /// children and lands on Leaf - not Loaded - because it no longer has a source to answer for it.</summary>
    [Fact]
    public async Task ChildSource_WhenClearedToNull_EvictsDisposesAndBecomesLeafAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        var previousChild = item.Children.ShouldHaveSingleItem();

        await dispatcher.InvokeAsync(() => { item.ChildSource = null; }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Leaf);
        item.Children.ShouldBeEmpty();
        previousChild.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies source eviction publishes the aggregate check-state transition caused by
    /// removing the loader-owned child snapshot.</summary>
    [Fact]
    public async Task ChildSource_WhenLoaderChildrenAreEvicted_NotifiesAggregateCheckStateAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("a", "A")
        {
            IsCheckable = true,
            InitialCheckState = false,
            Presence = TreeViewChildPresence.Leaf
        });
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root")
            {
                ChildSource = source,
                IsCheckable = true,
                IsChecked = true
            };
            var tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        CheckChangedEventArgs? change = null;
        item.CheckStateChanged += (_, eventArgs) => change = eventArgs;

        // Act
        await dispatcher.InvokeAsync(() => { item.ChildSource = null; }, TestContext.Current.CancellationToken);

        // Assert
        item.IsChecked.ShouldBe(true);
        _ = change.ShouldNotBeNull();
        change.Previous.ShouldBe(false);
        change.Current.ShouldBe(true);
    }

    /// <summary>Verifies a failed reload retains the children an earlier successful load already
    /// committed, and publishes the failure through <see cref="TreeViewItem.LastChildLoadError"/>.</summary>
    [Fact]
    public async Task CommitChildLoadFailure_WhenPriorChildrenExist_RetainsThemAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(
            null,
            new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("b", "B") { Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        item.Children.Count.ShouldBe(2);

        var failure = new InvalidOperationException("simulated enumeration failure");
        source.FailNext(null, failure);

        await dispatcher.InvokeAsync(() => { _ = item.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(2);
        item.LastChildLoadError.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies a null result list is rejected as a failure without mutating the previously
    /// committed - or absent - children.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultIsNull_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult(null!);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a null element inside an otherwise valid result list is rejected without
    /// mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultContainsANullElement_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([null!]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies duplicate keys within one result are rejected without mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultHasDuplicateKeys_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([
            new TreeViewChildDescription("dup", "First") { Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("dup", "Second") { Presence = TreeViewChildPresence.Leaf }
        ]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a key that collides with an ancestor's stable key - which would materialize
    /// a cycle - is rejected without mutating state.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenResultKeyCollidesWithAnAncestor_RejectsAsACycleAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(null, new TreeViewChildDescription("root-key", "Whoops"));
        TreeView tree = null!;
        TreeViewItem root = null!;
        TreeViewItem child = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { RemoteKey = "root-key" };
            child = new TreeViewItem("Child") { ChildSource = source };
            root.Children.Add(child);
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            child,
            () => child.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        child.Children.ShouldBeEmpty();
        _ = child.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a header containing a terminal control character is rejected without
    /// mutating state, matching the same validation a caller-authored <see cref="TreeViewItem.Header"/>
    /// enforces synchronously.</summary>
    [Fact]
    public async Task CommitChildLoad_WhenAHeaderContainsAControlCharacter_RejectsWithoutMutatingStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([
            new TreeViewChildDescription("bad", "ContainsBell") { Presence = TreeViewChildPresence.Leaf }
        ]);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        item.Children.ShouldBeEmpty();
        _ = item.LastChildLoadError.ShouldNotBeNull();
    }

    /// <summary>Verifies a stable key reused across a reload keeps the same materialized instance,
    /// preserving its IsExpanded, checked, and selected state instead of rebuilding it from scratch.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenKeysAreStable_PreservesExpandedCheckedAndSelectedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("k2", "Two") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var one = root.Children.Single(child => Equals(child.RemoteKey, "k1"));
        await dispatcher.InvokeAsync(() =>
        {
            one.IsExpanded = false;
            one.IsChecked = true;
            tree.SelectItem(one);
        }, TestContext.Current.CancellationToken);

        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One Renamed") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf },
            new TreeViewChildDescription("k2", "Two") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { _ = root.ReloadChildrenAsync(); }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.Children.Any(child => child.Header == "One Renamed"),
            TestContext.Current.CancellationToken);

        var reloadedOne = root.Children.Single(child => Equals(child.RemoteKey, "k1"));
        reloadedOne.ShouldBeSameAs(one, "the stable key must reuse the same materialized instance");
        reloadedOne.Header.ShouldBe("One Renamed");
        reloadedOne.IsExpanded.ShouldBeFalse();
        reloadedOne.IsChecked.ShouldBe(true);
        tree.SelectedItem.ShouldBeSameAs(one);
    }

    /// <summary>Verifies a checkable parent's own check state, set before its children ever load,
    /// applies as the initial state to later-loaded checkable descendants that do not specify their
    /// own initial state.</summary>
    [Fact]
    public async Task IsChecked_WhenSetBeforeChildrenLoad_AppliesToLaterLoadedCheckableDescendantsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source, IsCheckable = true, IsExpanded = false };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
            root.IsChecked = true;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("k1", "One") { IsCheckable = true, Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() => { root.IsExpanded = true; }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.IsChecked.ShouldBe(true);
    }

    /// <summary>Verifies a description's explicit InitialCheckState overrides the checkable
    /// parent's own check state, rather than always inheriting it.</summary>
    [Fact]
    public async Task InitialCheckState_WhenSpecified_OverridesTheCheckableParentsOwnStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem root = null!;

        await dispatcher.InvokeAsync(() =>
        {
            root = new TreeViewItem("Root") { ChildSource = source, IsCheckable = true, IsExpanded = false };
            tree = new TreeView { Items = { root } };
            tree.Attach(dispatcher);
            root.IsChecked = true;
        }, TestContext.Current.CancellationToken);

        source.AddChildren(
            null,
            new TreeViewChildDescription("k1", "One")
            {
                IsCheckable = true,
                InitialCheckState = false,
                Presence = TreeViewChildPresence.Leaf
            });

        await dispatcher.InvokeAsync(() => { root.IsExpanded = true; }, TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            root,
            () => root.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        var child = root.Children.ShouldHaveSingleItem();
        child.IsChecked.ShouldBe(false, "the description's own InitialCheckState must win over the inherited parent state");
    }

    /// <summary>Verifies disposing the owning tree while a descendant's load is still in flight
    /// cancels the subtree's pending loads through the item's own disposal hook, and a late
    /// completion afterward does not fault the fire-and-forget loop.</summary>
    [Fact]
    public async Task Dispose_WhenLoadIsInFlight_CancelsTheSubtreeLoadWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(tree.Dispose, TestContext.Current.CancellationToken);

        _ = deferred.TrySetResult([new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }]);

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsFaulted.ShouldBeFalse();
        item.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies directly disposing a loading item removes its semantic entry and drops a late completion.</summary>
    [Fact]
    public async Task ItemDispose_WhenLoadIsInFlight_RemovesEntryAndDropsLateCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var deferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;
        Task observation = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
            observation = item.LastChildLoadObservation!;
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await dispatcher.InvokeAsync(item.Dispose, TestContext.Current.CancellationToken);

        tree.Items.ShouldBeEmpty();
        item.ParentCollection.ShouldBeNull();
        _ = deferred.TrySetResult([new TreeViewChildDescription("late", "Late") { Presence = TreeViewChildPresence.Leaf }]);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsFaulted.ShouldBeFalse();
        item.Children.ShouldBeEmpty();
    }

    /// <summary>Verifies <see cref="TreeViewItem.ReloadChildrenAsync"/> rejects a null ChildSource.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenChildSourceIsNull_ThrowsInvalidOperationExceptionAsync()
    {
        var item = new TreeViewItem("Leaf");

        _ = await Should.ThrowAsync<InvalidOperationException>(() => item.ReloadChildrenAsync());
    }

    /// <summary>Verifies <see cref="TreeViewItem.ReloadChildrenAsync"/> requires an item attached to
    /// a running dispatcher.</summary>
    [Fact]
    public async Task ReloadChildrenAsync_WhenItemIsUnattached_ThrowsInvalidOperationExceptionAsync()
    {
        var source = new FakeTreeViewChildSource();
        var item = new TreeViewItem("Node") { ChildSource = source };

        _ = await Should.ThrowAsync<InvalidOperationException>(() => item.ReloadChildrenAsync());
    }

    /// <summary>Verifies an unloaded branch is skipped by <see cref="TreeView.ExpandAll"/> rather
    /// than being forced to start a remote load it never promised to trigger.</summary>
    [Fact]
    public async Task ExpandAll_WhenBranchIsUnloaded_SkipsItWithoutStartingALoadAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Node") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            tree.ExpandAll();
        }, TestContext.Current.CancellationToken);

        item.IsExpanded.ShouldBeFalse();
        item.ChildState.ShouldBe(TreeViewChildState.Unloaded);
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies a fresh tree defaults to four concurrent child-load admissions.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenCreated_DefaultsToFour()
    {
        var tree = new TreeView();

        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a non-positive concurrency limit is rejected before mutation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxConcurrentChildLoads_WhenSetToNonPositiveValue_ThrowsArgumentOutOfRangeException(int value)
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tree.MaxConcurrentChildLoads = value);

        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a changed concurrency limit publishes once, while an equivalent
    /// assignment remains notification-free.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenChanged_RaisesPropertyChangedOnce()
    {
        // Arrange
        var tree = new TreeView();
        List<string?> changed = [];
        tree.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        // Act
        tree.MaxConcurrentChildLoads = 2;
        tree.MaxConcurrentChildLoads = 2;

        // Assert
        changed.ShouldBe([nameof(TreeView.MaxConcurrentChildLoads)]);
    }

    /// <summary>Verifies even an equivalent concurrency-limit assignment enforces dispatcher
    /// affinity before returning as a no-op.</summary>
    [Fact]
    public async Task MaxConcurrentChildLoads_WhenAttachedAndAssignedCurrentValueOffDispatcher_ThrowsAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var tree = new TreeView();
        await dispatcher.InvokeAsync(() => tree.Attach(dispatcher), TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => tree.MaxConcurrentChildLoads = 4);
        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies a disposed tree rejects a concurrency-limit mutation before changing the
    /// retained admission policy.</summary>
    [Fact]
    public void MaxConcurrentChildLoads_WhenOwnerIsDisposed_ThrowsBeforeMutation()
    {
        // Arrange
        var tree = new TreeView();
        tree.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => tree.MaxConcurrentChildLoads = 2);
        tree.MaxConcurrentChildLoads.ShouldBe(4);
    }

    /// <summary>Verifies increasing the live concurrency limit immediately grants available slots
    /// to already queued requests instead of leaving capacity idle until another load finishes.</summary>
    [Fact]
    public async Task MaxConcurrentChildLoads_WhenIncreased_AdmitsQueuedRequestImmediatelyAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;
        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(1);
        second.IsAwaitingLoadSlot.ShouldBeTrue();

        // Act
        _ = await dispatcher.InvokeAsync(
            () => tree.MaxConcurrentChildLoads = 2,
            TestContext.Current.CancellationToken);
        _ = secondDeferred.TrySetResult([]);

        // Assert
        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        source.Requests.Count.ShouldBe(2);
        second.IsAwaitingLoadSlot.ShouldBeFalse();

        _ = firstDeferred.TrySetResult([]);
        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a second concurrent load request beyond <see cref="TreeView.MaxConcurrentChildLoads"/>
    /// is admission-queued rather than issued immediately, and is granted its own slot once an
    /// earlier request releases one.</summary>
    [Fact]
    public async Task RequestLoadSlot_WhenConcurrencyLimitIsReached_QueuesTheAdditionalRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;

        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        first.ChildState.ShouldBe(TreeViewChildState.Loading);
        second.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1, "only the admitted request should have reached the source");
        second.IsAwaitingLoadSlot.ShouldBeTrue();

        var firstObservation = first.LastChildLoadObservation!;
        var secondObservation = second.LastChildLoadObservation!;

        // Resolved from this test thread, not the dispatcher - RunLoadAsync's slot-release path
        // (ConfigureAwait(false) throughout, so its continuation and finally block run wherever
        // the source's task completed) must marshal back to the dispatcher before touching the
        // tree's admission bookkeeping or the queued item's own state, exactly as its commit and
        // failure branches already do. Awaiting both observations and asserting they never
        // faulted is what would have caught that admission handoff corrupting state or throwing
        // an unobserved exception off-dispatcher.
        _ = firstDeferred.TrySetResult([]);
        _ = secondDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        await firstObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await secondObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstObservation.IsFaulted.ShouldBeFalse();
        secondObservation.IsFaulted.ShouldBeFalse();

        second.IsAwaitingLoadSlot.ShouldBeFalse();
        source.Requests.Count.ShouldBe(2, "releasing the first slot must admit the queued second request");
    }

    /// <summary>Verifies collapsing an admission-queued item and immediately re-expanding it - all
    /// within the same dispatcher turn, before the cancelled request's posted slot-cleanup callback
    /// has had a chance to run - does not strand the re-expanded request. The cancelled request's
    /// deferred cleanup must not blindly clear whatever wait handle is currently installed: by the
    /// time it runs, the re-expand has already installed its own live one, and clearing it would
    /// leave nobody to ever grant that request its slot.</summary>
    [Fact]
    public async Task Expanded_WhenCollapsedAndReExpandedWhileAdmissionQueued_StillReachesLoadedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var firstDeferred = source.DeferNext(null);
        var secondDeferred = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem first = null!;
        TreeViewItem second = null!;

        await dispatcher.InvokeAsync(() =>
        {
            tree = new TreeView { MaxConcurrentChildLoads = 1 };
            first = new TreeViewItem("First") { ChildSource = source, IsExpanded = false };
            second = new TreeViewItem("Second") { ChildSource = source, IsExpanded = false };
            tree.Items.Add(first);
            tree.Items.Add(second);
            tree.Attach(dispatcher);
            first.IsExpanded = true;
            second.IsExpanded = true;

            // Collapse the still-queued second item and immediately re-expand it, both within this
            // same synchronous block. The first expand's cancellation posts its slot-cleanup callback
            // back through the dispatcher rather than running it inline, so it cannot possibly run
            // before this block finishes - the re-expand below is guaranteed to have already installed
            // its own live wait handle by the time that stale cleanup eventually executes.
            second.IsExpanded = false;
            second.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        first.ChildState.ShouldBe(TreeViewChildState.Loading);
        second.ChildState.ShouldBe(TreeViewChildState.Loading);
        source.Requests.Count.ShouldBe(1, "only the admitted first request should have reached the source so far");

        var firstObservation = first.LastChildLoadObservation!;
        _ = firstDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            first,
            () => first.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await firstObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Releasing the first slot must admit the re-expanded second request. Before the fix, the
        // first expand's stale posted cleanup unconditionally nulled the field the re-expand had
        // already repointed at its own wait handle, so nothing was ever left to grant a slot to and
        // this second item stayed Loading forever - this call would time out.
        var secondObservation = second.LastChildLoadObservation!;
        _ = secondDeferred.TrySetResult([]);

        await TreeViewChildLoadWait.UntilAsync(
            second,
            () => second.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);
        await secondObservation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        second.ChildState.ShouldBe(TreeViewChildState.Loaded);
        second.IsAwaitingLoadSlot.ShouldBeFalse();
        source.Requests.Count.ShouldBe(2, "the re-expanded second request must eventually reach the source");
    }

    /// <summary>Verifies a Control-held Enter over a LoadFailed item does not retry the load and
    /// leaves the stroke unhandled - matching the activation-eligible modifier gate
    /// <c>TreeView.OnKeyRouted</c> already applies to the ordinary activation path just a
    /// few lines below.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifierOnLoadFailedItem_DoesNotRetryAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        source.Requests.Count.ShouldBe(1, "the gated stroke must not have started a second request");
    }

    /// <summary>Verifies an Alt-held Enter over a LoadFailed item is gated the same way a
    /// Control-held one is.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasAltModifierOnLoadFailedItem_DoesNotRetryAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Alt, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);

        item.ChildState.ShouldBe(TreeViewChildState.LoadFailed);
        source.Requests.Count.ShouldBe(1, "the gated stroke must not have started a second request");
    }

    /// <summary>Verifies a plain Enter over a LoadFailed item still retries the load and handles
    /// the stroke.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterIsPlainOnLoadFailedItem_RetriesAndHandlesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            item.ChildState.ShouldBe(TreeViewChildState.Loading);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.Count.ShouldBe(2, "the retry must have reached the source as a second request");
    }

    /// <summary>Verifies a Shift-held Enter (a common terminal chord) over a LoadFailed item still
    /// retries the load and handles the stroke.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifierOnLoadFailedItem_RetriesAndHandlesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        source.FailNext(null, new InvalidOperationException("simulated"));
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.LoadFailed,
            TestContext.Current.CancellationToken);

        source.AddChildren(null, new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf });

        await dispatcher.InvokeAsync(() =>
        {
            tree.SelectItem(item);
            using FocusManager focus = new(tree);
            focus.Focus(tree).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(tree, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            item.ChildState.ShouldBe(TreeViewChildState.Loading);
        }, TestContext.Current.CancellationToken);

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        source.Requests.Count.ShouldBe(2, "the retry must have reached the source as a second request");
    }

    /// <summary>Verifies a fresh tree carries the documented default status row text.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenCreated_UseDocumentedDefaults()
    {
        var tree = new TreeView();

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText round-trip a caller-assigned value.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenAssigned_RoundTrip()
    {
        var tree = new TreeView
        {
            LoadingText = "Please wait…",
            LoadFailedText = "Could not load.",
        };

        tree.LoadingText.ShouldBe("Please wait…");
        tree.LoadFailedText.ShouldBe("Could not load.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText reject a null value.</summary>
    [Fact]
    public void LoadingAndLoadFailedText_WhenAssignedNull_ThrowArgumentNullException()
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentNullException>(() => tree.LoadingText = null!);
        _ = Should.Throw<ArgumentNullException>(() => tree.LoadFailedText = null!);

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }

    /// <summary>Verifies LoadingText and LoadFailedText reject a value containing a terminal
    /// control character instead of silently corrupting the rendered status row.</summary>
    [Theory]
    [InlineData("Loading\nnow")]
    [InlineData("Loading\tnow")]
    public void LoadingAndLoadFailedText_WhenContainingControlCharacter_ThrowArgumentException(string value)
    {
        var tree = new TreeView();

        _ = Should.Throw<ArgumentException>(() => tree.LoadingText = value);
        _ = Should.Throw<ArgumentException>(() => tree.LoadFailedText = value);

        tree.LoadingText.ShouldBe("Loading…");
        tree.LoadFailedText.ShouldBe("Failed to load. Press Enter to retry.");
    }

    #endregion

    #region Child-loading dispatcher fullness

    private static TreeViewChildDescription[] OneLeafChild() =>
        [new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }];

    /// <summary>Blocks the dispatcher thread inside one posted callback (which becomes the
    /// currently-running work item, so it no longer counts against the bounded queue itself), then
    /// queues one more filler behind it, saturating a capacity-1 dispatcher for as long as the
    /// returned handle is held.</summary>
    /// <param name="dispatcher">The capacity-1 dispatcher to saturate.</param>
    /// <param name="cancellationToken">Cancels waiting for the hostage to start running.</param>
    /// <param name="filler">The queued filler; defaults to a no-op when null.</param>
    private static async Task<ManualResetEventSlim> SaturateSingleSlotQueueAsync(
        Dispatcher dispatcher,
        CancellationToken cancellationToken,
        Action? filler = null)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(cancellationToken);
        dispatcher.Post(filler ?? (static () => { }));
        return release;
    }

    /// <summary>Verifies the success-commit post's bridging retry - given a genuine chance to
    /// succeed once the saturated slot frees, exactly as a live dispatcher queue drains in
    /// practice - reaches <see cref="Dispatcher.UnhandledException"/> and
    /// <see cref="Dispatcher.FatalException"/> with the original "queue is full" failure, the same
    /// outcome a synchronous dispatcher-callback failure already produces. The commit itself never
    /// runs (only the rethrow was ever queued), so the item stays stuck Loading - loading state is
    /// deliberately not reset as a substitute for this contract.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSuccessCommitPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostageRelease = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        // Frees the one saturated slot deterministically, in the otherwise nanosecond-wide window
        // between the first (failed) attempt and the bridging retry, instead of racing a genuine
        // drain: releasing the hostage lets the dispatcher thread dequeue and run the filler above,
        // which signals fillerDrained the moment it does, before the retry ever attempts to post.
        dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        completion.SetResult(OneLeafChild());

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await Should.NotThrowAsync(async () =>
            await dispatcher.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        dispatcher.FatalException.ShouldBeSameAs(reported);
    }

    /// <summary>Verifies the success-commit post's bridging retry, when it is also rejected for a
    /// full queue - the queue never drains at all in this scenario - drops the fault as the
    /// documented, accepted edge instead of retrying indefinitely: the observation completes
    /// successfully, nothing reaches <see cref="Dispatcher.UnhandledException"/>, and the item
    /// stays stuck Loading with no observable signal beyond that stall.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSuccessCommitPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;
        var release = await SaturateSingleSlotQueueAsync(dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        completion.SetResult(OneLeafChild());

        // Nothing ever frees the saturated slot, so both the original attempt and the bridging
        // retry observe the same full queue; give the off-thread continuation a moment to reach and
        // exhaust both before asserting the drop.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        release.Set();

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        unhandledObserved.ShouldBeFalse();
    }

    /// <summary>Verifies the success-commit and slot-release posts still no-op silently once the
    /// dispatcher is disposed, exactly as before.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenDispatcherIsDisposedAfterSuccess_CompletesWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        await dispatcher.DisposeAsync();

        completion.SetResult(OneLeafChild());

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies the failure-commit post's bridging retry - given the same genuine chance
    /// to succeed as the success-commit site above - reaches
    /// <see cref="Dispatcher.UnhandledException"/> with the original "queue is full" failure.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenFailureCommitPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostageRelease = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();

        // The failure commit never ran either (same bridge, same only-the-rethrow-was-queued
        // shape), so LastChildLoadError is never published and the item stays stuck Loading rather
        // than advancing to LoadFailed.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        item.LastChildLoadError.ShouldBeNull();
    }

    /// <summary>Verifies the failure-commit post's bridging retry, when it is also rejected for a
    /// full queue, drops the fault instead of retrying indefinitely - the sibling edge case to the
    /// success-commit site's own double-failure drop above.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenFailureCommitPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;
        var release = await SaturateSingleSlotQueueAsync(dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        release.Set();

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        item.LastChildLoadError.ShouldBeNull();
        unhandledObserved.ShouldBeFalse();
    }

    /// <summary>Verifies the failure-commit and slot-release posts still no-op silently once the
    /// dispatcher is disposed, exactly as before.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenDispatcherIsDisposedAfterFailure_CompletesWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        await dispatcher.DisposeAsync();

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies the <c>finally</c> block's own admission-slot release post, when its own
    /// bridging retry is also rejected for a full queue - isolated from the other two sites by
    /// letting the success-commit post through first - drops the fault as the documented, accepted
    /// edge instead of retrying indefinitely, and that the already-queued commit still runs once
    /// the dispatcher is unblocked even though the slot itself was never released.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSlotReleasePostFindsQueueFullOnBothAttempts_DropsTheFaultButStillCommitsAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        using ManualResetEventSlim release = new();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // No filler this time: with capacity 1 and the hostage already dequeued, the queue is
        // empty, so the success-commit post below succeeds and consumes the one slot the queue
        // allows - isolating the finally block's own slot-release post (and its own bridging
        // retry) as the one that finds it full on both attempts.
        completion.SetResult(OneLeafChild());

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        // The success commit's own post succeeded immediately (queue was empty), so give the
        // off-thread continuation a moment to reach the finally block and exhaust both of its own
        // post attempts against the now-saturated queue before asserting the drop.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // The success commit was already queued behind the still-blocked dispatcher thread before
        // the finally's own posts failed, so it has not run yet.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        unhandledObserved.ShouldBeFalse();

        release.Set();

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
    }

    #endregion
}
