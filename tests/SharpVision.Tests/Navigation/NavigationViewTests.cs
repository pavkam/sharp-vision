// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies sidebar navigation item ownership, selection, groups, and keyboard navigation.</summary>
public sealed class NavigationViewTests
{
    /// <summary>Verifies a navigation view starts as a quiet borderless sidebar surface without caller styling.</summary>
    [ComponentUnitEvidence(typeof(NavigationView))]
    [Fact]
    public void Constructor_WhenCreated_UsesQuietBackgroundDefaults()
    {
        // Arrange and act
        var navigation = new NavigationView();

        // Assert
        navigation.ActualBorder.Sides.ShouldBe(BorderSide.None);
        navigation.Face.Background.ShouldBe(SemanticColor.Control);
    }

    /// <summary>Verifies items are added through the typed collection.</summary>
    [ComponentUnitEvidence(typeof(NavigationViewItem))]
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var nav = new NavigationView { Header = "Test" };
        nav.Items.Add(new NavigationViewItem { Text = "Page 1" });
        nav.Items.Add(new NavigationViewItem { Text = "Page 2" });

        nav.Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies unchanged LineSize assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void LineSize_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var nav = new NavigationView();
        var notifications = 0;
        nav.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(NavigationView.LineSize))
            {
                notifications++;
            }
        };

        nav.LineSize = 3;
        nav.LineSize = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies unchanged PageOverlap assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void PageOverlap_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var nav = new NavigationView();
        var notifications = 0;
        nav.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(NavigationView.PageOverlap))
            {
                notifications++;
            }
        };

        nav.PageOverlap = 3;
        nav.PageOverlap = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies LineSize rejects a negative value.</summary>
    [Fact]
    public void LineSize_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var nav = new NavigationView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.LineSize = -1);
    }

    /// <summary>Verifies PageOverlap rejects a negative value.</summary>
    [Fact]
    public void PageOverlap_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var nav = new NavigationView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.PageOverlap = -1);
    }

    /// <summary>Verifies LineSize forwards to, and reads back from, the generated scroll container.</summary>
    [Fact]
    public void LineSize_WhenSet_ForwardsToScrollContainer()
    {
        var nav = new NavigationView { LineSize = 3 };

        nav.LineSize.ShouldBe(3);
    }

    /// <summary>Verifies PageOverlap forwards to, and reads back from, the generated scroll container.</summary>
    [Fact]
    public void PageOverlap_WhenSet_ForwardsToScrollContainer()
    {
        var nav = new NavigationView { PageOverlap = 3 };

        nav.PageOverlap.ShouldBe(3);
    }

    /// <summary>Verifies PerformInvoke activates a standalone item through the same path as
    /// owner-driven activation, raising Invoked and then executing the bound command.</summary>
    [Fact]
    public void PerformInvoke_WhenCommandCanExecute_RaisesInvokedThenExecutesExactlyOnce()
    {
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var item = new NavigationViewItem
        {
            Text = "Page",
            Command = command,
            CommandParameter = parameter
        };
        item.Invoked += (_, _) => order.Add("invoked");

        item.PerformInvoke();

        order.ShouldBe(["invoked", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies a false CanExecute suppresses execution but never the Invoked event.</summary>
    [Fact]
    public void PerformInvoke_WhenCommandCannotExecute_StillRaisesInvoked()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var item = new NavigationViewItem { Text = "Page", Command = command };
        var invoked = 0;
        item.Invoked += (_, _) => invoked++;

        item.PerformInvoke();

        invoked.ShouldBe(1);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies callers select an owned semantic entry without moving focus to its private face.</summary>
    [Fact]
    public void SelectItem_WhenOwned_UpdatesSelectionWithoutChangingFocusOwnership()
    {
        var nav = new NavigationView();
        var item = new NavigationViewItem { Text = "Page" };
        nav.Items.Add(item);

        nav.SelectItem(item);

        nav.SelectedItem.ShouldBeSameAs(item);
        item.Selected.ShouldBeTrue();
        item.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies selecting an owned entry publishes a PropertyChanged notification for
    /// SelectedItem, so a data-bound consumer observes selection changes made through any of
    /// SelectItem, focus, keyboard navigation, or structural repair - not just a hypothetical
    /// direct setter.</summary>
    [Fact]
    public void SelectItem_WhenOwned_RaisesSelectedItemPropertyChanged()
    {
        var nav = new NavigationView();
        var item = new NavigationViewItem { Text = "Page" };
        nav.Items.Add(item);
        var raised = new List<string?>();
        nav.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        nav.SelectItem(item);

        raised.ShouldContain(nameof(NavigationView.SelectedItem));
    }

    /// <summary>Verifies semantic selection rejects an entry from another navigation owner.</summary>
    [Fact]
    public void SelectItem_WhenForeign_ThrowsArgumentException()
    {
        var nav = new NavigationView();
        var other = new NavigationView();
        var item = new NavigationViewItem { Text = "Elsewhere" };
        other.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => nav.SelectItem(item));
    }

    /// <summary>Verifies private items reject focus while selection remains owner-managed.</summary>
    [Fact]
    public async Task Focus_WhenItemReceivesFocus_SelectsItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var item1 = new NavigationViewItem { Text = "A" };
            var item2 = new NavigationViewItem { Text = "B" };
            nav.Items.Add(item1);
            nav.Items.Add(item2);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            var raised = 0;
            nav.SelectionChanged += (_, _) => raised++;

            focus.Focus(item1).ShouldBeFalse();
            nav.SelectedItem.ShouldBeNull();
            raised.ShouldBe(0);
            focus.Focus(nav).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Down arrow navigates between items via the bubble handler.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowKeyPressed_NavigatesBetweenItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Text = "A" };
            var b = new NavigationViewItem { Text = "B" };
            var c = new NavigationViewItem { Text = "C" };
            nav.Items.Add(a);
            nav.Items.Add(b);
            nav.Items.Add(c);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(nav).ShouldBeTrue();

            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(nav, Events.Key, down);

            down.Handled.ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(a);
            focus.Focused.ShouldBeSameAs(nav);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies groups contain sub-items that participate in selection.</summary>
    [Fact]
    public async Task Group_WhenSubItemActivated_SelectsSubItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Text = "General" };
            group.Items.Add(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(nav).ShouldBeTrue();

            _ = Router.Route(nav, Events.Key, new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            nav.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not select the current item,
    /// and leaves the stroke unhandled so a shortcut bound to the modified combination still sees it.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifier_DoesNotSelectAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Text = "A" };
            nav.Items.Add(a);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(nav).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(nav, Events.Key, enter);

            enter.Handled.ShouldBeFalse();
            nav.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still selects the current item.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifier_StillSelectsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Text = "A" };
            nav.Items.Add(a);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            focus.Focus(nav).ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(nav, Events.Key, enter);

            enter.Handled.ShouldBeTrue();
            nav.SelectedItem.ShouldBeSameAs(a);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not toggle a group's expanded
    /// state through the group's own key handler.</summary>
    [Fact]
    public async Task Group_WhenEnterHasControlModifier_DoesNotToggleExpandedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Text = "General" };
            group.Items.Add(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);
            group.Expanded.ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(group, Events.Key, enter);

            enter.Handled.ShouldBeFalse();
            group.Expanded.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still toggles a group's expanded
    /// state through the group's own key handler.</summary>
    [Fact]
    public async Task Group_WhenEnterHasShiftModifier_StillTogglesExpandedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Text = "General" };
            group.Items.Add(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);
            group.Expanded.ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(group, Events.Key, enter);

            enter.Handled.ShouldBeTrue();
            group.Expanded.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies collapsing a group hides its sub-items.</summary>
    [Fact]
    public async Task Group_WhenCollapsed_HidesSubItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Text = "General" };
            group.Items.Add(sub);
            nav.Items.Add(group);
            nav.Attach(dispatcher);

            group.Expanded = false;

            sub.EffectiveIsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies collapsing the selected item's group repairs selection to the adjacent
    /// remaining item at the same position, matching removal's repair rather than always jumping
    /// to the first selectable item in the view.</summary>
    [Fact]
    public async Task Group_WhenCollapsedWithSelectedDescendant_RepairsToAdjacentItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var before = new NavigationViewItem { Text = "Before" };
            var after = new NavigationViewItem { Text = "After" };
            var sub = new NavigationViewItem { Text = "General" };
            var group = new NavigationViewGroup { Header = "Settings" };
            group.Items.Add(sub);
            var nav = new NavigationView();
            nav.Items.Add(before);
            nav.Items.Add(group);
            nav.Items.Add(after);
            nav.Attach(dispatcher);
            nav.SelectItem(sub);

            group.Expanded = false;

            nav.SelectedItem.ShouldBeSameAs(after);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removed and cleared grouped items cannot select through their former owner.</summary>
    [Fact]
    public void Group_WhenItemsAreRemovedOrCleared_DetachesOwnerActivation()
    {
        // Arrange
        var selected = new NavigationViewItem { Text = "Selected" };
        var removed = new NavigationViewItem { Text = "Removed" };
        var cleared = new NavigationViewItem { Text = "Cleared" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(removed);
        group.Items.Add(cleared);
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        // Act
        group.Items.Remove(removed).ShouldBeTrue();
        group.Items.Clear();
        removed.ActivateFromOwner(ActivationCause.Programmatic);
        cleared.ActivateFromOwner(ActivationCause.Programmatic);

        // Assert
        navigation.SelectedItem.ShouldBeSameAs(selected);
        selected.Selected.ShouldBeTrue();
        removed.Selected.ShouldBeFalse();
        cleared.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies disposing the selected item directly (bypassing Items.Remove) repairs
    /// selection so subsequent navigation does not crash with ObjectDisposedException.</summary>
    [Fact]
    public void Items_WhenSelectedItemDisposedDirectly_RepairsSelectionWithoutThrowing()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var other = new NavigationViewItem { Text = "Other" };
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(other);
        navigation.SelectItem(selected);

        selected.Dispose();

        navigation.SelectedItem.ShouldBeSameAs(other);
        Should.NotThrow(() => navigation.SelectItem(other));
    }

    /// <summary>Verifies disposing the selected item directly repairs to the adjacent remaining
    /// item at the same position, matching how removing it through Items.Remove already repairs
    /// - not always to the first item in the entire view, which throws the cursor away from
    /// where the user's selection conceptually was.</summary>
    [Fact]
    public void Items_WhenSelectedItemDisposedDirectly_RepairsToAdjacentItemNotTheFirst()
    {
        var first = new NavigationViewItem { Text = "First" };
        var second = new NavigationViewItem { Text = "Second" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var navigation = new NavigationView();
        navigation.Items.Add(first);
        navigation.Items.Add(second);
        navigation.Items.Add(selected);
        navigation.SelectItem(selected);

        selected.Dispose();

        navigation.SelectedItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies owner disposal can retire a grouped current item after the group has
    /// already disposed that descendant, matching application shutdown of grouped navigation.</summary>
    [Fact]
    public void Dispose_WhenGroupedItemIsCurrent_DoesNotMutateDisposedDescendant()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(new NavigationViewGroup { Header = "Earlier group" });
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        Should.NotThrow(navigation.Dispose);

        navigation.Disposed.ShouldBeTrue();
        group.Disposed.ShouldBeTrue();
        selected.Disposed.ShouldBeTrue();
    }

    /// <summary>Verifies disposal retires the selected identity even when its group is the final
    /// top-level entry and no later host change can repair retained navigation state.</summary>
    [Fact]
    public void Dispose_WhenSelectedGroupIsFinalEntry_ClearsSelectedItem()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        navigation.Dispose();

        navigation.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies removing the selected item directly from its group repairs
    /// selection through the owning view and raises exactly one SelectionChanged.</summary>
    [Fact]
    public void Group_WhenSelectedChildIsRemoved_RepairsSelectionAndRaisesEventOnce()
    {
        var other = new NavigationViewItem { Text = "Other" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(other);
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(group);
        navigation.SelectItem(selected);
        var changes = new List<NavigationViewSelectionChangedEventArgs>();
        navigation.SelectionChanged += (_, args) => changes.Add(args);

        group.Items.Remove(selected).ShouldBeTrue();

        navigation.SelectedItem.ShouldBeSameAs(other);
        selected.Selected.ShouldBeFalse();
        other.Selected.ShouldBeTrue();
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(selected),
            args => args.CurrentItem.ShouldBeSameAs(other));
    }

    /// <summary>Verifies clearing a group containing the selected item repairs
    /// selection to the nearest remaining item outside the group.</summary>
    [Fact]
    public void Group_WhenClearedWithSelectedChild_RepairsSelectionToNearestRemainingItem()
    {
        var before = new NavigationViewItem { Text = "Before" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(before);
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        group.Items.Clear();

        navigation.SelectedItem.ShouldBeSameAs(before);
        selected.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies removing the selected item after it was disabled — leaving it outside
    /// CollectSelectableItems while still SelectedItem, since neither SelectItem nor disabling
    /// require the target to stay selectable — repairs selection instead of indexing the
    /// remaining selectable set with PrepareRemoval's not-found sentinel.</summary>
    [Fact]
    public void Items_WhenDisabledSelectedItemIsRemoved_RepairsSelectionToRemainingItem()
    {
        var remaining = new NavigationViewItem { Text = "Remaining" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(remaining);
        navigation.SelectItem(selected);
        selected.Enabled = false;

        _ = Should.NotThrow(() => navigation.Items.Remove(selected));

        navigation.SelectedItem.ShouldBeSameAs(remaining);
    }

    /// <summary>Verifies a disabled group's sub-item is never treated as a fallback selection
    /// target, matching the same disabled-group guard already applied to keyboard navigation
    /// (see the sibling EffectiveIsVisible/EffectiveIsEnabled check on CollectNavigableFrom).</summary>
    [Fact]
    public void Items_WhenSelectedItemIsRemovedFromDisabledGroup_DoesNotFallBackIntoThatGroup()
    {
        var remaining = new NavigationViewItem { Text = "Remaining" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var groupOnlyItem = new NavigationViewItem { Text = "GroupOnly" };
        var group = new NavigationViewGroup { Header = "Group", Enabled = false };
        group.Items.Add(groupOnlyItem);
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(group);
        navigation.Items.Add(remaining);
        navigation.SelectItem(selected);

        _ = navigation.Items.Remove(selected);

        navigation.SelectedItem.ShouldBeSameAs(remaining);
    }

    /// <summary>Verifies removing an entire group repairs selection when the
    /// selected item is a descendant rather than the group entry itself.</summary>
    [Fact]
    public void Items_WhenGroupContainingSelectedItemIsRemoved_RepairsSelection()
    {
        var remaining = new NavigationViewItem { Text = "Remaining" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(remaining);
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        navigation.Items.Remove(group).ShouldBeTrue();

        navigation.SelectedItem.ShouldBeSameAs(remaining);
        selected.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies removing every remaining descendant of the selected item's
    /// group clears selection entirely rather than leaving a stale reference.</summary>
    [Fact]
    public void Items_WhenGroupContainingSelectedItemIsRemovedAndNoneRemain_ClearsSelection()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(selected);
        var navigation = new NavigationView();
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        navigation.Items.Remove(group).ShouldBeTrue();

        navigation.SelectedItem.ShouldBeNull();
        selected.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies a group's selected item survives moving into another
    /// navigation view without carrying ghost selection state from its former owner.</summary>
    [Fact]
    public void Group_WhenSelectedItemReparentsIntoAnotherView_DoesNotLeakOwnerSelectionState()
    {
        var group = new NavigationViewGroup { Header = "Group" };
        var item = new NavigationViewItem { Text = "Item" };
        group.Items.Add(item);
        var first = new NavigationView();
        first.Items.Add(group);
        first.SelectItem(item);

        group.Items.Remove(item).ShouldBeTrue();
        var second = new NavigationView();
        second.Items.Add(item);

        first.SelectedItem.ShouldBeNull();
        second.SelectedItem.ShouldBeNull();
        item.Selected.ShouldBeFalse();
    }

    /// <summary>Verifies clearing a footer group's selected item does not clear a
    /// main-section selection, and vice versa.</summary>
    [Fact]
    public void ClearEntries_WhenSectionDoesNotOwnSelection_LeavesOtherSectionSelectionIntact()
    {
        var mainSelected = new NavigationViewItem { Text = "Main" };
        var footerGroup = new NavigationViewGroup { Header = "Footer group" };
        footerGroup.Items.Add(new NavigationViewItem { Text = "Footer item" });
        var navigation = new NavigationView();
        navigation.Items.Add(mainSelected);
        navigation.FooterItems.Add(footerGroup);
        navigation.SelectItem(mainSelected);

        navigation.FooterItems.Clear();

        navigation.SelectedItem.ShouldBeSameAs(mainSelected);
        mainSelected.Selected.ShouldBeTrue();
    }

    /// <summary>Verifies removal from a group restores the item's authored Padding, Focusable, and TabStop.</summary>
    [Fact]
    public void Group_WhenItemIsRemoved_RestoresAuthoredPaddingFocusableAndTabStop()
    {
        var item = new NavigationViewItem
        {
            Text = "Item",
            Padding = new Thickness(5, 1, 5, 1),
            Focusable = false,
            TabStop = false
        };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(item);

        group.Items.Remove(item).ShouldBeTrue();

        item.Padding.ShouldBe(new Thickness(5, 1, 5, 1));
        item.Focusable.ShouldBeFalse();
        item.TabStop.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an item moved from a group to the top level renders without the
    /// group's indentation, rather than retaining a leaked private padding
    /// value that has no explanation at the top level.
    /// </summary>
    [Fact]
    public void Items_WhenGroupedItemMovesToTopLevel_DoesNotRetainGroupPadding()
    {
        var item = new NavigationViewItem { Text = "Item" };
        var authoredPadding = item.Padding;
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(item);

        group.Items.Remove(item).ShouldBeTrue();
        var navigation = new NavigationView();
        navigation.Items.Add(item);

        item.Padding.ShouldBe(authoredPadding);
    }

    /// <summary>Verifies group membership never touches an item's Padding, regardless of
    /// whether it is authored before or after the item joins the group.</summary>
    [Fact]
    public void Group_WhenItemJoinsOrLeaves_NeverMutatesItemPadding()
    {
        var before = new NavigationViewItem { Text = "Before", Padding = new Thickness(3, 0, 0, 0) };
        var after = new NavigationViewItem { Text = "After" };
        var group = new NavigationViewGroup { Header = "Group" };

        group.Items.Add(before);
        group.Items.Add(after);
        after.Padding = new Thickness(1, 1, 1, 1);

        before.Padding.ShouldBe(new Thickness(3, 0, 0, 0));
        after.Padding.ShouldBe(new Thickness(1, 1, 1, 1));
    }

    /// <summary>Verifies a group's Items collection enumerates in insertion order and reports the
    /// correct Count and indexer, matching NavigationViewEntryCollection's constrained collection
    /// shape.</summary>
    [Fact]
    public void Items_WhenSubItemsAreAdded_EnumeratesInInsertionOrderWithCountAndIndexer()
    {
        var group = new NavigationViewGroup { Header = "Group" };
        var first = new NavigationViewItem { Text = "First" };
        var second = new NavigationViewItem { Text = "Second" };

        group.Items.Add(first);
        group.Items.Add(second);

        group.Items.Count.ShouldBe(2);
        group.Items[0].ShouldBeSameAs(first);
        group.Items[1].ShouldBeSameAs(second);
        group.Items.ShouldBe([first, second]);
    }

    /// <summary>Verifies a sub-item added to a second group throws and leaves the first group's
    /// count unchanged.</summary>
    [Fact]
    public void Items_WhenSubItemIsAddedToASecondGroup_ThrowsAndLeavesFirstGroupCountUnchanged()
    {
        var item = new NavigationViewItem { Text = "Item" };
        var first = new NavigationViewGroup { Header = "First" };
        var second = new NavigationViewGroup { Header = "Second" };
        first.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => second.Items.Add(item));

        first.Items.Count.ShouldBe(1);
        second.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies the item indent defaults to 2 cells and rejects negative values, now on the
    /// group's own style rather than a control-side setter.</summary>
    [ComponentUnitEvidence(typeof(NavigationViewGroup))]
    [Fact]
    public void ItemIndent_WhenDefaulted_Is2AndRejectsNegative()
    {
        var group = new NavigationViewGroup();

        group.ActualStyle.ItemIndent.ShouldBe(2);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => group.Style = NavigationViewGroupStyle.Default with { ItemIndent = -1 });
    }

    /// <summary>Verifies separator is non-focusable and non-hit-testable.</summary>
    [ComponentUnitEvidence(typeof(NavigationViewSeparator))]
    [Fact]
    public void Separator_WhenCreated_IsNonInteractive()
    {
        var separator = new NavigationViewSeparator();

        separator.CanFocus.ShouldBeFalse();
        separator.HitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies a separator stretches to fill its row on its own, so the owning view
    /// never needs to pin its Width.</summary>
    [Fact]
    public void Separator_WhenCreated_StretchesHorizontally()
    {
        var separator = new NavigationViewSeparator();

        separator.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
    }

    /// <summary>Verifies adding and removing a separator never touches its authored Width.</summary>
    [Fact]
    public void Items_WhenSeparatorIsAddedAndRemoved_NeverMutatesWidth()
    {
        var separator = new NavigationViewSeparator { Width = Length.Cells(6) };
        var nav = new NavigationView();

        nav.Items.Add(separator);
        separator.Width.ShouldBe(Length.Cells(6));

        _ = nav.Items.Remove(separator);
        separator.Width.ShouldBe(Length.Cells(6));
    }

    /// <summary>Verifies footer items are accessible through the FooterItems collection.</summary>
    [Fact]
    public void FooterItems_WhenAdded_IncreasesFooterCount()
    {
        var nav = new NavigationView();
        nav.FooterItems.Add(new NavigationViewItem { Text = "Quit" });

        nav.FooterItems.Count.ShouldBe(1);
        nav.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies header renders when set.</summary>
    [Fact]
    public void Header_WhenSet_IsRendered()
    {
        var nav = new NavigationView
        {
            Header = "My App",
            Width = Length.Cells(24),
            Height = Length.Cells(10)
        };
        var size = new Size(24, 10);
        new LayoutEngine().Layout(nav, size);
        using Frame frame = new(size);

        nav.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("M");
    }

    /// <summary>Verifies removing an item clears selection if it was selected.</summary>
    [Fact]
    public async Task Items_WhenSelectedItemRemoved_ClearsSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var item = new NavigationViewItem { Text = "A" };
            nav.Items.Add(item);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);
            nav.SelectedItem.ShouldBeNull();

            _ = nav.Items.Remove(item);

            nav.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies NavigationView is the only tab stop; private item faces are never focusable.</summary>
    [Fact]
    public async Task Selection_WhenChanged_OnlySelectedItemIsTabStopAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var nav = new NavigationView();
            var a = new NavigationViewItem { Text = "A" };
            var b = new NavigationViewItem { Text = "B" };
            var c = new NavigationViewItem { Text = "C" };
            nav.Items.Add(a);
            nav.Items.Add(b);
            nav.Items.Add(c);
            nav.Attach(dispatcher);
            using FocusManager focus = new(nav);

            a.CanTabStop.ShouldBeFalse();
            b.CanTabStop.ShouldBeFalse();
            c.CanTabStop.ShouldBeFalse();

            a.CanFocus.ShouldBeFalse();
            b.CanFocus.ShouldBeFalse();
            c.CanFocus.ShouldBeFalse();
            focus.Focus(nav).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the generated scroll container's contract is reachable directly on
    /// NavigationView, without a caller needing to know about the private items Stack.</summary>
    [Fact]
    public void ScrollBy_WhenContentExceedsViewport_MovesVerticalOffsetAndRaisesScrollChanged()
    {
        var nav = new NavigationView();

        for (var index = 0; index < 20; index++)
        {
            nav.Items.Add(new NavigationViewItem { Text = $"Item {index}" });
        }

        new LayoutEngine().Layout(nav, new Size(10, 4));
        List<ScrollChangedEventArgs> changes = [];
        nav.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        nav.Extent.Height.ShouldBeGreaterThan(nav.Viewport.Height);
        var moved = nav.ScrollBy(0, 3);

        moved.ShouldBeTrue();
        nav.VerticalOffset.ShouldBe(3);
        _ = changes.ShouldHaveSingleItem();
    }

    /// <summary>Verifies BringItemIntoView scrolls minimally to reveal an item below the viewport.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsBelowViewport_ScrollsToRevealIt()
    {
        var nav = new NavigationView();
        NavigationViewItem? last = null;

        for (var index = 0; index < 20; index++)
        {
            last = new NavigationViewItem { Text = $"Item {index}" };
            nav.Items.Add(last);
        }

        new LayoutEngine().Layout(nav, new Size(10, 4));

        var moved = nav.BringItemIntoView(last!);

        moved.ShouldBeTrue();
        nav.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies BringItemIntoView validates its argument like the underlying container does.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var nav = new NavigationView();

        _ = Should.Throw<ArgumentNullException>(() => nav.BringItemIntoView(null!));
    }

    /// <summary>Verifies an item header carrying a terminal control character is rejected instead of
    /// silently dropping post-newline text at render time.</summary>
    [Theory]
    [InlineData("Save\nAs")]
    [InlineData("Save\rAs")]
    [InlineData("Save\tAs")]
    public void ItemHeader_WhenContainingControlCharacter_Throws(string header)
    {
        var item = new NavigationViewItem();

        _ = Should.Throw<ArgumentException>(() => item.Text = header);
    }

    /// <summary>Verifies a group header carrying a terminal control character is rejected the same
    /// way an item header is.</summary>
    [Theory]
    [InlineData("Save\nAs")]
    [InlineData("Save\rAs")]
    [InlineData("Save\tAs")]
    public void GroupHeader_WhenContainingControlCharacter_Throws(string header)
    {
        var group = new NavigationViewGroup();

        _ = Should.Throw<ArgumentException>(() => group.Header = header);
    }

    /// <summary>Verifies Insert places an entry at the requested position instead of appending.</summary>
    [Fact]
    public void Insert_WhenGivenIndex_PlacesItAtRequestedPosition()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        var middle = new NavigationViewItem { Text = "Middle" };
        nav.Items.Add(a);
        nav.Items.Add(b);

        nav.Items.Insert(1, middle);

        nav.Items.Count.ShouldBe(3);
        nav.Items[0].ShouldBeSameAs(a);
        nav.Items[1].ShouldBeSameAs(middle);
        nav.Items[2].ShouldBeSameAs(b);
    }

    /// <summary>Verifies Insert also accepts a separator or a group at a position.</summary>
    [Fact]
    public void Insert_WhenGivenSeparatorOrGroup_PlacesItAtRequestedPosition()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        var separator = new NavigationViewSeparator();
        var group = new NavigationViewGroup { Header = "Group" };
        nav.Items.Add(a);
        nav.Items.Add(b);

        nav.Items.Insert(1, separator);
        nav.Items.Insert(2, group);

        nav.Items.Count.ShouldBe(4);
        nav.Items[0].ShouldBeSameAs(a);
        nav.Items[1].ShouldBeSameAs(separator);
        nav.Items[2].ShouldBeSameAs(group);
        nav.Items[3].ShouldBeSameAs(b);
    }

    /// <summary>Verifies an out-of-range Insert throws before mutating the collection.</summary>
    [Fact]
    public void Insert_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var nav = new NavigationView();
        nav.Items.Add(new NavigationViewItem { Text = "A" });

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => nav.Items.Insert(5, new NavigationViewItem { Text = "B" }));

        nav.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies removing the selected entry by position repairs selection to the nearest
    /// remaining selectable item, matching Remove's own repair.</summary>
    [Fact]
    public void RemoveAt_WhenSelectedEntryIsRemoved_RepairsSelectionToNearestAvailable()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        var c = new NavigationViewItem { Text = "C" };
        nav.Items.Add(a);
        nav.Items.Add(b);
        nav.Items.Add(c);
        nav.SelectItem(b);

        nav.Items.RemoveAt(1);

        nav.Items.Count.ShouldBe(2);
        nav.SelectedItem.ShouldBeSameAs(c);
    }

    /// <summary>Verifies removing an entry that is not the selected one leaves SelectedItem's
    /// identity untouched, since selection here is tracked by reference, not index.</summary>
    [Fact]
    public void RemoveAt_WhenEntryOtherThanSelectionIsRemoved_PreservesSelectedItemIdentity()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        nav.Items.Add(a);
        nav.Items.Add(b);
        nav.SelectItem(b);

        nav.Items.RemoveAt(0);

        nav.Items.Count.ShouldBe(1);
        nav.SelectedItem.ShouldBeSameAs(b);
    }

    /// <summary>Verifies an out-of-range RemoveAt throws before mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var nav = new NavigationView();
        nav.Items.Add(new NavigationViewItem { Text = "A" });

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.Items.RemoveAt(5));

        nav.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies replacing the selected entry through the indexer detaches the old entry
    /// without disposing it and selects the replacement.</summary>
    [Fact]
    public void Indexer_WhenSelectedEntryIsReplaced_DetachesOldWithoutDisposalAndSelectsReplacement()
    {
        var nav = new NavigationView();
        var original = new NavigationViewItem { Text = "Original" };
        var replacement = new NavigationViewItem { Text = "Replacement" };
        nav.Items.Add(original);
        nav.SelectItem(original);

        nav.Items[0] = replacement;

        nav.Items.Count.ShouldBe(1);
        nav.Items[0].ShouldBeSameAs(replacement);
        original.Disposed.ShouldBeFalse();
        original.Parent.ShouldBeNull();
        nav.SelectedItem.ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies an out-of-range indexer assignment throws before mutating the collection.</summary>
    [Fact]
    public void Indexer_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var nav = new NavigationView();
        nav.Items.Add(new NavigationViewItem { Text = "A" });

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => nav.Items[5] = new NavigationViewItem { Text = "B" });

        nav.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies Move repositions an entry while preserving its identity and, since
    /// selection here is reference-tracked, the selected item's identity.</summary>
    [Fact]
    public void Move_WhenEntryMoves_PreservesIdentityAndSelection()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        var c = new NavigationViewItem { Text = "C" };
        nav.Items.Add(a);
        nav.Items.Add(b);
        nav.Items.Add(c);
        nav.SelectItem(a);

        nav.Items.Move(0, 2);

        nav.Items.Count.ShouldBe(3);
        nav.Items[0].ShouldBeSameAs(b);
        nav.Items[1].ShouldBeSameAs(c);
        nav.Items[2].ShouldBeSameAs(a);
        nav.SelectedItem.ShouldBeSameAs(a);
    }

    /// <summary>Verifies an out-of-range Move throws before mutating the collection.</summary>
    [Fact]
    public void Move_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        nav.Items.Add(a);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.Items.Move(0, 5));

        nav.Items.Count.ShouldBe(1);
        nav.Items[0].ShouldBeSameAs(a);
    }

    /// <summary>Verifies IndexOf reports an owned entry's position and -1 for a foreign one, and that
    /// the header and footer sections are independently indexed.</summary>
    [Fact]
    public void IndexOf_WhenItemIsOwnedOrForeign_ReportsPositionOrNegativeOne()
    {
        var nav = new NavigationView();
        var a = new NavigationViewItem { Text = "A" };
        var b = new NavigationViewItem { Text = "B" };
        var footer = new NavigationViewItem { Text = "Footer" };
        var foreign = new NavigationViewItem { Text = "Elsewhere" };
        nav.Items.Add(a);
        nav.Items.Add(b);
        nav.FooterItems.Add(footer);

        nav.Items.IndexOf(b).ShouldBe(1);
        nav.Items.IndexOf(footer).ShouldBe(-1);
        nav.FooterItems.IndexOf(footer).ShouldBe(0);
        nav.Items.IndexOf(foreign).ShouldBe(-1);
    }
}
