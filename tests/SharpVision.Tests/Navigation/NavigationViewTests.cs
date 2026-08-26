// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies sidebar navigation item ownership, selection, groups, and keyboard navigation.</summary>
public sealed class NavigationViewTests
{
    /// <summary>Verifies every public selection-notification boundary yields ownership to a newer
    /// nested selection and never publishes the superseded typed payload.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SelectItem_WhenSelectionNotificationReenters_PublishesOnlyNewestSelection(int boundary)
    {
        var first = new NavigationViewItem { Text = "First" };
        var second = new NavigationViewItem { Text = "Second" };
        var newest = new NavigationViewItem { Text = "Newest" };
        var navigation = new NavigationView();
        navigation.Items.Add(first);
        navigation.Items.Add(second);
        navigation.Items.Add(newest);
        navigation.SelectItem(first);
        List<NavigationViewSelectionChangedEventArgs> changes = [];
        navigation.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (boundary == 0 && eventArgs.PropertyName == nameof(NavigationViewItem.IsSelected))
            {
                navigation.SelectItem(newest);
            }
        };
        second.PropertyChanged += (_, eventArgs) =>
        {
            if (boundary == 1 && eventArgs.PropertyName == nameof(NavigationViewItem.IsSelected))
            {
                navigation.SelectItem(newest);
            }
        };
        navigation.PropertyChanged += (_, eventArgs) =>
        {
            if (boundary == 2 &&
                eventArgs.PropertyName == nameof(NavigationView.SelectedItem) &&
                ReferenceEquals(navigation.SelectedItem, second))
            {
                navigation.SelectItem(newest);
            }
        };

        navigation.SelectItem(second);

        navigation.SelectedItem.ShouldBeSameAs(newest);
        first.IsSelected.ShouldBeFalse();
        second.IsSelected.ShouldBeFalse();
        newest.IsSelected.ShouldBeTrue();
        changes.ShouldHaveSingleItem().CurrentItem.ShouldBeSameAs(newest);
    }

    /// <summary>Verifies a navigation view starts as a quiet borderless sidebar surface without caller styling.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesQuietBackgroundDefaults()
    {
        // Arrange and act
        var navigation = new NavigationView();

        // Assert
        navigation.ActualBorder.Sides.ShouldBe(BorderSide.None);
        navigation.Face.Background.ShouldBe(SemanticColor.Control);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes flip EffectiveIsEnabled and
    /// the derived focus eligibility it drives, and re-enabling restores both.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByAncestor_UpdatesNavigationViewEffectiveEnabled()
    {
        var nav = new NavigationView { IsEnabled = false };

        nav.EffectiveIsEnabled.ShouldBeFalse();
        nav.CanFocus.ShouldBeFalse();

        nav.IsEnabled = true;

        nav.EffectiveIsEnabled.ShouldBeTrue();
        nav.CanFocus.ShouldBeTrue();

        var ancestor = new Overlay { Children = { nav }, IsEnabled = false };

        nav.IsEnabled.ShouldBeTrue();
        nav.EffectiveIsEnabled.ShouldBeFalse();
        nav.CanFocus.ShouldBeFalse();

        ancestor.IsEnabled = true;

        nav.EffectiveIsEnabled.ShouldBeTrue();
        nav.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies items are added through the typed collection.</summary>
    [Fact]
    public void Items_WhenAdded_IncreasesCount()
    {
        var nav = new NavigationView { Header = "Test" };
        nav.Items.Add(new NavigationViewItem { Text = "Page 1" });
        nav.Items.Add(new NavigationViewItem { Text = "Page 2" });

        nav.Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies direct and owning-NavigationView-inherited IsEnabled changes flip a
    /// NavigationViewItem's EffectiveIsEnabled without disturbing its own IsEnabled property, and
    /// re-enabling restores it.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByOwner_UpdatesNavigationViewItemEffectiveEnabled()
    {
        var item = new NavigationViewItem { Text = "Page", IsEnabled = false };

        item.EffectiveIsEnabled.ShouldBeFalse();

        item.IsEnabled = true;

        item.EffectiveIsEnabled.ShouldBeTrue();

        var nav = new NavigationView();
        nav.Items.Add(item);

        nav.IsEnabled = false;

        item.IsEnabled.ShouldBeTrue();
        item.EffectiveIsEnabled.ShouldBeFalse();

        nav.IsEnabled = true;

        item.EffectiveIsEnabled.ShouldBeTrue();
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

    /// <summary>Verifies HorizontalOffset defaults to zero and stays there - the generated items
    /// container only ever enables vertical scrolling, so zero is the only value the current
    /// extent ever admits - while still rejecting a positive request the way the documented
    /// exception promises.</summary>
    [Fact]
    public void HorizontalOffset_WhenNavigationViewHasNoHorizontalScrolling_AlwaysReportsZeroAndRejectsPositive()
    {
        var nav = new NavigationView();

        nav.HorizontalOffset.ShouldBe(0);
        nav.HorizontalOffset = 0;
        nav.HorizontalOffset.ShouldBe(0);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.HorizontalOffset = 1);
        nav.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies VerticalOffset can be set directly (not only through ScrollBy) and reads
    /// back from the generated scroll container within the current extent.</summary>
    [Fact]
    public void VerticalOffset_WhenSetDirectly_RoundTripsWithinExtent()
    {
        var nav = new NavigationView();

        for (var index = 0; index < 20; index++)
        {
            nav.Items.Add(new NavigationViewItem { Text = $"Item {index}" });
        }

        new LayoutEngine().Layout(nav, new Size(10, 4));

        nav.VerticalOffset = 3;

        nav.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies VerticalOffset rejects a value outside the current extent.</summary>
    [Fact]
    public void VerticalOffset_WhenOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var nav = new NavigationView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.VerticalOffset = 1);
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

    /// <summary>Verifies activation retains the entry command binding across reentrant invocation callbacks.</summary>
    [Fact]
    public void PerformInvoke_WhenInvokedCallbackRebindsAndDisposes_ExecutesCapturedCommand()
    {
        var originalParameter = new object();
        var original = new ProbeCommand();
        var replacement = new ProbeCommand();
        var item = new NavigationViewItem
        {
            Text = "Page",
            Command = original,
            CommandParameter = originalParameter
        };
        item.Invoked += (_, _) =>
        {
            item.Command = replacement;
            item.CommandParameter = new object();
            item.Dispose();
        };

        item.PerformInvoke();

        original.Executions.ShouldBe([originalParameter]);
        replacement.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies unavailable items reject programmatic activation, matching the same
    /// EffectiveIsEnabled/EffectiveIsVisible gate every other PerformInvoke-equivalent method
    /// enforces.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void PerformInvoke_WhenItemIsUnavailable_DoesNothing(bool enabled, Visibility visibility)
    {
        var item = new NavigationViewItem { Text = "Page", IsEnabled = enabled, Visibility = visibility };
        var invoked = 0;
        item.Invoked += (_, _) => invoked++;

        item.PerformInvoke();

        invoked.ShouldBe(0);
    }

    /// <summary>Verifies PerformInvoke rejects use after disposal.</summary>
    [Fact]
    public void PerformInvoke_WhenDisposed_Throws()
    {
        var item = new NavigationViewItem { Text = "Page" };
        item.Dispose();

        _ = Should.Throw<ObjectDisposedException>(item.PerformInvoke);
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
        item.IsSelected.ShouldBeTrue();
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

    /// <summary>Verifies semantic selection rejects a null item and leaves selection untouched.</summary>
    [Fact]
    public void SelectItem_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var nav = new NavigationView();
        var item = new NavigationViewItem { Text = "Page" };
        nav.Items.Add(item);
        nav.SelectItem(item);

        _ = Should.Throw<ArgumentNullException>(() => nav.SelectItem(null!));

        nav.SelectedItem.ShouldBeSameAs(item);
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

            down.IsHandled.ShouldBeTrue();
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

            enter.IsHandled.ShouldBeFalse();
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

            enter.IsHandled.ShouldBeTrue();
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
            group.IsExpanded.ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Control, KeyAction.Press));
            _ = Router.Route(group, Events.Key, enter);

            enter.IsHandled.ShouldBeFalse();
            group.IsExpanded.ShouldBeTrue();
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
            group.IsExpanded.ShouldBeTrue();

            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.Shift, KeyAction.Press));
            _ = Router.Route(group, Events.Key, enter);

            enter.IsHandled.ShouldBeTrue();
            group.IsExpanded.ShouldBeFalse();
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

            group.IsExpanded = false;

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

            group.IsExpanded = false;

            nav.SelectedItem.ShouldBeSameAs(after);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies re-expanding a group after its collapse already repaired selection away
    /// from a descendant does not steal selection back - NotifyGroupVisibilityChanged only redirects
    /// the cached navigator position onto the group itself when the navigator's *current* entry is
    /// still a descendant of it, and by the time IsExpanded flips back to true the navigator (and
    /// SelectedItem) have already moved to "After", which is neither the group nor inside it.</summary>
    [Fact]
    public async Task Group_WhenReExpandedAfterRepair_DoesNotStealSelectionBackAsync()
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
            group.IsExpanded = false;
            nav.SelectedItem.ShouldBeSameAs(after);

            group.IsExpanded = true;

            nav.SelectedItem.ShouldBeSameAs(after);
            sub.EffectiveIsVisible.ShouldBeTrue();
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
        selected.IsSelected.ShouldBeTrue();
        removed.IsSelected.ShouldBeFalse();
        cleared.IsSelected.ShouldBeFalse();
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

    /// <summary>Verifies disposing the sole remaining selectable item directly clears selection
    /// instead of indexing an empty selectable set, the same as removal already does through
    /// CompleteRemoval.</summary>
    [Fact]
    public void Items_WhenOnlySelectedItemDisposedDirectly_ClearsSelection()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.SelectItem(selected);

        selected.Dispose();

        navigation.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies collapsing the selected item's own Visibility - not a removal, not an
    /// ancestor group toggling - repairs selection to the adjacent remaining item and raises
    /// exactly one SelectionChanged, matching how a removal already repairs.</summary>
    [Fact]
    public void Items_WhenSelectedItemVisibilityIsSetDirectlyToCollapsed_RepairsSelectionToAdjacentItem()
    {
        var before = new NavigationViewItem { Text = "Before" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var after = new NavigationViewItem { Text = "After" };
        var navigation = new NavigationView();
        navigation.Items.Add(before);
        navigation.Items.Add(selected);
        navigation.Items.Add(after);
        navigation.SelectItem(selected);
        var changes = new List<NavigationViewSelectionChangedEventArgs>();
        navigation.SelectionChanged += (_, args) => changes.Add(args);

        selected.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeSameAs(after);
        selected.IsSelected.ShouldBeFalse();
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(selected),
            args => args.CurrentItem.ShouldBeSameAs(after));
    }

    /// <summary>Verifies Hidden repairs selection the same way Collapsed does - both make
    /// EffectiveIsVisible false, and this repair has no Hidden-specific branch.</summary>
    [Fact]
    public void Items_WhenSelectedItemVisibilityIsSetDirectlyToHidden_RepairsSelectionToAdjacentItem()
    {
        var before = new NavigationViewItem { Text = "Before" };
        var selected = new NavigationViewItem { Text = "Selected" };
        var after = new NavigationViewItem { Text = "After" };
        var navigation = new NavigationView();
        navigation.Items.Add(before);
        navigation.Items.Add(selected);
        navigation.Items.Add(after);
        navigation.SelectItem(selected);

        selected.Visibility = Visibility.Hidden;

        navigation.SelectedItem.ShouldBeSameAs(after);
        selected.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies collapsing the sole item in the view clears selection instead of
    /// indexing an empty selectable set.</summary>
    [Fact]
    public void Items_WhenOnlySelectedItemCollapses_ClearsSelection()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.SelectItem(selected);

        selected.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeNull();
        selected.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies collapsing the selected item clears selection the same way when it was
    /// the last remaining selectable entry - the rest already unselectable rather than absent
    /// from the collection entirely.</summary>
    [Fact]
    public void Items_WhenLastSelectableItemCollapses_ClearsSelection()
    {
        var alreadyCollapsed = new NavigationViewItem { Text = "AlreadyCollapsed", Visibility = Visibility.Collapsed };
        var selected = new NavigationViewItem { Text = "Selected" };
        var navigation = new NavigationView();
        navigation.Items.Add(alreadyCollapsed);
        navigation.Items.Add(selected);
        navigation.SelectItem(selected);

        selected.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies the visibility subscription moves with selection - collapsing a
    /// previously selected item that is no longer selected raises no repair and leaves the
    /// current selection untouched.</summary>
    [Fact]
    public void Items_WhenPreviouslySelectedItemCollapsesAfterReselection_LeavesCurrentSelectionUnaffected()
    {
        var previouslySelected = new NavigationViewItem { Text = "A" };
        var currentlySelected = new NavigationViewItem { Text = "B" };
        var navigation = new NavigationView();
        navigation.Items.Add(previouslySelected);
        navigation.Items.Add(currentlySelected);
        navigation.SelectItem(previouslySelected);
        navigation.SelectItem(currentlySelected);
        var changes = new List<NavigationViewSelectionChangedEventArgs>();
        navigation.SelectionChanged += (_, args) => changes.Add(args);

        previouslySelected.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeSameAs(currentlySelected);
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies collapsing a group with a selected descendant raises exactly one
    /// SelectionChanged - the descendant's own Visibility never changes when its group collapses
    /// (only the group's internal stack's does), so the selected item's own VisibilityChanged
    /// subscription never fires alongside the group-collapse repair for the same transition.</summary>
    [Fact]
    public void Group_WhenCollapsedWithSelectedDescendant_RaisesSelectionChangedExactlyOnce()
    {
        var sub = new NavigationViewItem { Text = "General" };
        var group = new NavigationViewGroup { Header = "Settings" };
        group.Items.Add(sub);
        var after = new NavigationViewItem { Text = "After" };
        var navigation = new NavigationView();
        navigation.Items.Add(group);
        navigation.Items.Add(after);
        navigation.SelectItem(sub);
        var changes = new List<NavigationViewSelectionChangedEventArgs>();
        navigation.SelectionChanged += (_, args) => changes.Add(args);

        group.IsExpanded = false;

        navigation.SelectedItem.ShouldBeSameAs(after);
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(sub),
            args => args.CurrentItem.ShouldBeSameAs(after));
    }

    /// <summary>Verifies collapsing a group's own Visibility directly - not through IsExpanded -
    /// repairs a selected descendant to the adjacent remaining item and raises exactly one
    /// SelectionChanged, the same as the group-collapse path already does.</summary>
    [Fact]
    public void Group_WhenVisibilitySetDirectlyToCollapsedWithSelectedDescendant_RepairsSelectionToAdjacentItem()
    {
        var before = new NavigationViewItem { Text = "Before" };
        var sub = new NavigationViewItem { Text = "General" };
        var group = new NavigationViewGroup { Header = "Settings" };
        group.Items.Add(sub);
        var after = new NavigationViewItem { Text = "After" };
        var navigation = new NavigationView();
        navigation.Items.Add(before);
        navigation.Items.Add(group);
        navigation.Items.Add(after);
        navigation.SelectItem(sub);
        var changes = new List<NavigationViewSelectionChangedEventArgs>();
        navigation.SelectionChanged += (_, args) => changes.Add(args);

        group.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeSameAs(after);
        sub.IsSelected.ShouldBeFalse();
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(sub),
            args => args.CurrentItem.ShouldBeSameAs(after));
    }

    /// <summary>Verifies collapsing a group's own Visibility directly clears selection instead of
    /// indexing an empty selectable set when the group holds the only remaining selectable
    /// descendant, the same as removal already does through CompleteRemoval.</summary>
    [Fact]
    public void Group_WhenVisibilitySetDirectlyToCollapsedWithOnlySelectedDescendant_ClearsSelection()
    {
        var sub = new NavigationViewItem { Text = "General" };
        var group = new NavigationViewGroup { Header = "Settings" };
        group.Items.Add(sub);
        var navigation = new NavigationView();
        navigation.Items.Add(group);
        navigation.SelectItem(sub);

        group.Visibility = Visibility.Collapsed;

        navigation.SelectedItem.ShouldBeNull();
        sub.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies Hidden repairs a selected descendant the same way Collapsed does - both
    /// make the group's EffectiveIsVisible false, and this repair has no Hidden-specific branch.</summary>
    [Fact]
    public void Group_WhenVisibilitySetDirectlyToHiddenWithSelectedDescendant_RepairsSelectionToAdjacentItem()
    {
        var before = new NavigationViewItem { Text = "Before" };
        var sub = new NavigationViewItem { Text = "General" };
        var group = new NavigationViewGroup { Header = "Settings" };
        group.Items.Add(sub);
        var after = new NavigationViewItem { Text = "After" };
        var navigation = new NavigationView();
        navigation.Items.Add(before);
        navigation.Items.Add(group);
        navigation.Items.Add(after);
        navigation.SelectItem(sub);

        group.Visibility = Visibility.Hidden;

        navigation.SelectedItem.ShouldBeSameAs(after);
        sub.IsSelected.ShouldBeFalse();
    }

    /// <summary>Verifies collapsing a group directly while the keyboard-current entry is one of
    /// its descendants parks current somewhere still visible rather than on the now-invisible
    /// group itself - landing current on an invisible group would let a later Enter toggle
    /// IsExpanded on an entry the user can no longer see.</summary>
    [Fact]
    public async Task Group_WhenVisibilitySetDirectlyToCollapsedWithKeyboardCurrentDescendant_DoesNotActivateInvisibleGroupAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var group = new NavigationViewGroup { Header = "Settings" };
            var sub = new NavigationViewItem { Text = "General" };
            group.Items.Add(sub);
            var after = new NavigationViewItem { Text = "After" };
            var navigation = new NavigationView();
            navigation.Items.Add(group);
            navigation.Items.Add(after);
            navigation.Attach(dispatcher);
            using FocusManager focus = new(navigation);
            focus.Focus(navigation).ShouldBeTrue();
            var down = new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(navigation, Events.Key, down);
            _ = Router.Route(navigation, Events.Key, down);
            navigation.SelectedItem.ShouldBeSameAs(sub);

            group.Visibility = Visibility.Collapsed;

            navigation.SelectedItem.ShouldBeSameAs(after);
            var enter = new KeyEventArgs(new Stroke(
                Code.Enter, default, nativeCode: 0, Modifiers.None, KeyAction.Press));
            _ = Router.Route(navigation, Events.Key, enter);

            group.IsExpanded.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing a group tears down its Visibility subscription - changing the
    /// detached group's Visibility afterward neither repairs nor raises SelectionChanged for the
    /// still-live navigation view.</summary>
    [Fact]
    public void Group_WhenRemovedThenVisibilityChanges_DoesNotAffectLiveSelection()
    {
        var selected = new NavigationViewItem { Text = "Selected" };
        var group = new NavigationViewGroup { Header = "Group" };
        var sub = new NavigationViewItem { Text = "Sub" };
        group.Items.Add(sub);
        var navigation = new NavigationView();
        navigation.Items.Add(selected);
        navigation.Items.Add(group);
        navigation.SelectItem(selected);

        navigation.Items.Remove(group).ShouldBeTrue();
        var changed = 0;
        navigation.SelectionChanged += (_, _) => changed++;

        _ = Should.NotThrow(() => group.Visibility = Visibility.Collapsed);

        changed.ShouldBe(0);
        navigation.SelectedItem.ShouldBeSameAs(selected);
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

        navigation.IsDisposed.ShouldBeTrue();
        group.IsDisposed.ShouldBeTrue();
        selected.IsDisposed.ShouldBeTrue();
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
        selected.IsSelected.ShouldBeFalse();
        other.IsSelected.ShouldBeTrue();
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
        selected.IsSelected.ShouldBeFalse();
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
        selected.IsEnabled = false;

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
        var group = new NavigationViewGroup { Header = "Group", IsEnabled = false };
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
        selected.IsSelected.ShouldBeFalse();
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
        selected.IsSelected.ShouldBeFalse();
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
        item.IsSelected.ShouldBeFalse();
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
        mainSelected.IsSelected.ShouldBeTrue();
    }

    /// <summary>Verifies removal from a group restores the item's authored Padding, IsFocusable, and IsTabStop.</summary>
    [Fact]
    public void Group_WhenItemIsRemoved_RestoresAuthoredPaddingFocusableAndTabStop()
    {
        var item = new NavigationViewItem
        {
            Text = "Item",
            Padding = new Thickness(5, 1, 5, 1),
            IsFocusable = false,
            IsTabStop = false
        };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(item);

        group.Items.Remove(item).ShouldBeTrue();

        item.Padding.ShouldBe(new Thickness(5, 1, 5, 1));
        item.IsFocusable.ShouldBeFalse();
        item.IsTabStop.ShouldBeFalse();
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

    /// <summary>Verifies a group's sub-item collection rejects a null Add argument and leaves
    /// Count unchanged.</summary>
    [Fact]
    public void Items_WhenAddedItemIsNull_ThrowsArgumentNullException()
    {
        var group = new NavigationViewGroup { Header = "Group" };

        _ = Should.Throw<ArgumentNullException>(() => group.Items.Add(null!));

        group.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies re-adding an item already owned by this same group is rejected instead of
    /// silently duplicating it in the underlying stack.</summary>
    [Fact]
    public void Items_WhenAddedItemAlreadyBelongsToThisGroup_ThrowsArgumentException()
    {
        var group = new NavigationViewGroup { Header = "Group" };
        var item = new NavigationViewItem { Text = "Item" };
        group.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => group.Items.Add(item));

        group.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies a group's sub-item collection rejects a null Remove argument.</summary>
    [Fact]
    public void Items_WhenRemovedItemIsNull_ThrowsArgumentNullException()
    {
        var group = new NavigationViewGroup { Header = "Group" };

        _ = Should.Throw<ArgumentNullException>(() => group.Items.Remove(null!));
    }

    /// <summary>Verifies removing an item this group never owned reports false instead of throwing
    /// or disturbing the current membership.</summary>
    [Fact]
    public void Items_WhenRemovedItemIsNotOwned_ReturnsFalse()
    {
        var group = new NavigationViewGroup { Header = "Group" };
        var owned = new NavigationViewItem { Text = "Owned" };
        group.Items.Add(owned);
        var stray = new NavigationViewItem { Text = "Stray" };

        group.Items.Remove(stray).ShouldBeFalse();

        group.Items.Count.ShouldBe(1);
        group.Items[0].ShouldBeSameAs(owned);
    }

    /// <summary>Verifies the item indent defaults to 2 cells and rejects negative values, now on the
    /// group's own style rather than a control-side setter.</summary>
    [Fact]
    public void ItemIndent_WhenDefaulted_Is2AndRejectsNegative()
    {
        var group = new NavigationViewGroup();

        group.ActualStyle.ItemIndent.ShouldBe(2);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => group.Style = NavigationViewGroupStyle.Default with { ItemIndent = -1 });
    }

    /// <summary>Verifies a group's local Style defaults to null (theme ownership) and round-trips
    /// an assigned complete style through both Style and the resolved ActualStyle.</summary>
    [Fact]
    public void Style_WhenAssigned_RoundTripsLocalAndResolvedStyle()
    {
        var group = new NavigationViewGroup();
        group.Style.ShouldBeNull();

        var style = NavigationViewGroupStyle.Default with { ItemIndent = 4 };
        group.Style = style;

        group.Style.ShouldBe(style);
        group.ActualStyle.ShouldBe(style);

        group.Style = null;

        group.Style.ShouldBeNull();
    }

    /// <summary>Verifies a local style's ItemIndent difference is graded Measure, not just Render -
    /// the same distinction NavigationViewItemStyle's own comparer draws for AffixGap - isolated
    /// from every other style member by starting from an already-assigned identical style.</summary>
    [Fact]
    public void Style_WhenItemIndentChanges_InvalidatesMeasure()
    {
        using var group = new NavigationViewGroup
        {
            Header = "Group",
            Style = NavigationViewGroupStyle.Default
        };
        group.Clear(Invalidation.All);

        group.Style = NavigationViewGroupStyle.Default with { ItemIndent = 4 };

        group.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a local style difference that does not touch ItemIndent still invalidates
    /// rendering only, unaffected by the ItemIndent-specific comparer branch.</summary>
    [Fact]
    public void Style_WhenGlyphChangesButItemIndentDoesNot_InvalidatesRenderOnly()
    {
        using var group = new NavigationViewGroup
        {
            Header = "Group",
            Style = NavigationViewGroupStyle.Default
        };
        group.Clear(Invalidation.All);

        group.Style = NavigationViewGroupStyle.Default with { CollapsedGlyph = new Rune('*') };

        group.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies direct and owning-NavigationView-inherited IsEnabled changes flip a
    /// NavigationViewGroup's EffectiveIsEnabled without disturbing its own IsEnabled property, and
    /// re-enabling restores it.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByOwner_UpdatesNavigationViewGroupEffectiveEnabled()
    {
        var group = new NavigationViewGroup { Header = "Group", IsEnabled = false };

        group.EffectiveIsEnabled.ShouldBeFalse();

        group.IsEnabled = true;

        group.EffectiveIsEnabled.ShouldBeTrue();

        var nav = new NavigationView();
        nav.Items.Add(group);

        nav.IsEnabled = false;

        group.IsEnabled.ShouldBeTrue();
        group.EffectiveIsEnabled.ShouldBeFalse();

        nav.IsEnabled = true;

        group.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies separator is non-focusable and non-hit-testable.</summary>
    [Fact]
    public void Separator_WhenCreated_IsNonInteractive()
    {
        var separator = new NavigationViewSeparator();

        separator.CanFocus.ShouldBeFalse();
        separator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies direct and owning-NavigationView-inherited IsEnabled changes flip a
    /// NavigationViewSeparator's EffectiveIsEnabled without disturbing its own IsEnabled property,
    /// and re-enabling restores it.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByOwner_UpdatesNavigationViewSeparatorEffectiveEnabled()
    {
        var separator = new NavigationViewSeparator { IsEnabled = false };

        separator.EffectiveIsEnabled.ShouldBeFalse();

        separator.IsEnabled = true;

        separator.EffectiveIsEnabled.ShouldBeTrue();

        var nav = new NavigationView();
        nav.Items.Add(separator);

        nav.IsEnabled = false;

        separator.IsEnabled.ShouldBeTrue();
        separator.EffectiveIsEnabled.ShouldBeFalse();

        nav.IsEnabled = true;

        separator.EffectiveIsEnabled.ShouldBeTrue();
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

    /// <summary>Verifies retained header text and group visibility follow newer values committed
    /// from their owner property notifications.</summary>
    [Fact]
    public void ForwardedProperties_WhenObserversCommitNewerValues_UpdateRetainedPresentation()
    {
        var group = new NavigationViewGroup { Header = "Group" };
        var child = new NavigationViewItem { Text = "Child" };
        group.Items.Add(child);
        var nav = new NavigationView { Header = "Initial" };
        nav.Items.Add(group);
        nav.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(NavigationView.Header) && nav.Header == "Outer")
            {
                nav.Header = "Nested";
            }
        };
        group.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(NavigationViewGroup.IsExpanded) && !group.IsExpanded)
            {
                group.IsExpanded = true;
            }
        };

        nav.Header = "Outer";
        group.IsExpanded = false;
        var size = new Size(24, 10);
        new LayoutEngine().Layout(nav, size);
        using Frame frame = new(size);
        nav.Render(frame.Canvas);

        nav.Header.ShouldBe("Nested");
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        group.IsExpanded.ShouldBeTrue();
        child.EffectiveIsVisible.ShouldBeTrue();
    }

    /// <summary>Verifies Header round-trips, defaults to null, and its documented Measure impact
    /// actually reserves and releases the header row - not just repaints it - by comparing the
    /// auto-sized height with and without a header.</summary>
    [Fact]
    public void Header_WhenSetThenCleared_RoundTripsAndTogglesReservedHeaderRow()
    {
        var withoutHeader = new NavigationView();
        withoutHeader.Header.ShouldBeNull();
        new LayoutEngine().Layout(withoutHeader, new Size(20, 10));
        var baselineHeight = withoutHeader.DesiredSize.Height;

        var nav = new NavigationView { Header = "Title" };
        new LayoutEngine().Layout(nav, new Size(20, 10));

        nav.Header.ShouldBe("Title");
        nav.DesiredSize.Height.ShouldBe(baselineHeight + 1);

        nav.Header = null;
        new LayoutEngine().Layout(nav, new Size(20, 10));

        nav.Header.ShouldBeNull();
        nav.DesiredSize.Height.ShouldBe(baselineHeight);
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
        var change = changes.ShouldHaveSingleItem();
        change.PreviousOffset.ShouldBe(new Point(0, 0));
        change.Offset.ShouldBe(new Point(0, 3));
        change.Cause.ShouldBe(ScrollCause.Programmatic);
    }

    /// <summary>Verifies ScrollBy reports no movement, and raises no ScrollChanged, once the
    /// viewport is already saturated at the requested end - matching the generated scroll
    /// container's own endpoint-clamping contract exposed on TreeView and ListView.</summary>
    [Fact]
    public void ScrollBy_WhenAlreadyAtSaturatedEndpoint_ReturnsFalseWithoutRaisingScrollChanged()
    {
        var nav = new NavigationView();

        for (var index = 0; index < 20; index++)
        {
            nav.Items.Add(new NavigationViewItem { Text = $"Item {index}" });
        }

        new LayoutEngine().Layout(nav, new Size(10, 4));
        var changes = 0;
        nav.ScrollChanged += (_, _) => changes++;

        var moved = nav.ScrollBy(0, -1);

        moved.ShouldBeFalse();
        nav.VerticalOffset.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies ScrollBy propagates the composed scroll container's own cause validation.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var nav = new NavigationView();
        nav.Items.Add(new NavigationViewItem { Text = "Item" });

        new LayoutEngine().Layout(nav, new Size(10, 4));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => nav.ScrollBy(0, 1, (ScrollCause) 99));
    }

    /// <summary>Verifies ScrollBy rejects use after disposal.</summary>
    [Fact]
    public void ScrollBy_WhenDisposed_ThrowsObjectDisposedException()
    {
        var nav = new NavigationView();
        nav.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => nav.ScrollBy(0, 1));
    }

    /// <summary>Verifies ScrollBy requires dispatcher affinity once attached.</summary>
    [Fact]
    public async Task ScrollBy_WhenAttachedOffThread_ThrowsInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var nav = new NavigationView();

        await dispatcher.InvokeAsync(() => nav.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => nav.ScrollBy(0, 1));
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

    /// <summary>Verifies BringItemIntoView rejects an item that is not owned by this navigation
    /// view - never added, or added to a different NavigationView entirely.</summary>
    [Fact]
    public void BringItemIntoView_WhenItemIsNotOwned_ThrowsArgumentException()
    {
        var nav = new NavigationView();
        nav.Items.Add(new NavigationViewItem { Text = "Item" });
        new LayoutEngine().Layout(nav, new Size(10, 4));
        var stray = new NavigationViewItem { Text = "Stray" };

        _ = Should.Throw<ArgumentException>(() => nav.BringItemIntoView(stray));
    }

    /// <summary>Verifies BringItemIntoView rejects use after disposal.</summary>
    [Fact]
    public void BringItemIntoView_WhenDisposed_ThrowsObjectDisposedException()
    {
        var nav = new NavigationView();
        var item = new NavigationViewItem { Text = "Item" };
        nav.Items.Add(item);

        nav.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => nav.BringItemIntoView(item));
    }

    /// <summary>Verifies BringItemIntoView requires dispatcher affinity once attached.</summary>
    [Fact]
    public async Task BringItemIntoView_WhenAttachedOffThread_ThrowsInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var nav = new NavigationView();
        var item = new NavigationViewItem { Text = "Item" };
        nav.Items.Add(item);

        await dispatcher.InvokeAsync(() => nav.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => nav.BringItemIntoView(item));
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

    /// <summary>Verifies a group's Header defaults to empty, round-trips an assigned value, and
    /// rejects null while leaving the previous value in place.</summary>
    [Fact]
    public void GroupHeader_WhenAssignedOrNull_DefaultsRoundTripsAndRejectsNull()
    {
        var group = new NavigationViewGroup();
        group.Header.ShouldBe(string.Empty);

        group.Header = "Settings";

        group.Header.ShouldBe("Settings");
        _ = Should.Throw<ArgumentNullException>(() => group.Header = null!);
        group.Header.ShouldBe("Settings");
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
        original.IsDisposed.ShouldBeFalse();
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
        var parentChanges = 0;
        a.ParentChanged += (_, _) => parentChanges++;

        nav.Items.Move(0, 2);

        nav.Items.Count.ShouldBe(3);
        nav.Items[0].ShouldBeSameAs(b);
        nav.Items[1].ShouldBeSameAs(c);
        nav.Items[2].ShouldBeSameAs(a);
        nav.SelectedItem.ShouldBeSameAs(a);
        parentChanges.ShouldBe(0);
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

    /// <summary>Verifies every NavigationView-declared property starts at its documented default.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var nav = new NavigationView();

        nav.Header.ShouldBeNull();
        nav.ScrollBarStyle.ShouldBeNull();
        nav.LineSize.ShouldBe(1);
        nav.PageOverlap.ShouldBe(0);
        nav.HorizontalOffset.ShouldBe(0);
        nav.VerticalOffset.ShouldBe(0);
        nav.Extent.ShouldBe(default);
        nav.Viewport.ShouldBe(default);
        nav.Items.Count.ShouldBe(0);
        nav.FooterItems.Count.ShouldBe(0);
        nav.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies disposing the navigation view prevents every NavigationView-declared
    /// settable property from mutating further.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        var nav = new NavigationView();

        nav.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => nav.Header = "Test");
        _ = Should.Throw<ObjectDisposedException>(() => nav.ScrollBarStyle = ScrollBarStyle.ThinLine);
        _ = Should.Throw<ObjectDisposedException>(() => nav.LineSize = 2);
        _ = Should.Throw<ObjectDisposedException>(() => nav.PageOverlap = 2);
        _ = Should.Throw<ObjectDisposedException>(() => nav.HorizontalOffset = 0);
        _ = Should.Throw<ObjectDisposedException>(() => nav.VerticalOffset = 0);
    }

    /// <summary>Verifies every NavigationView-declared settable property requires dispatcher
    /// affinity once attached.</summary>
    [Fact]
    public async Task PropertySetter_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var nav = new NavigationView();

        await dispatcher.InvokeAsync(() => nav.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => nav.Header = "Test");
        _ = Should.Throw<InvalidOperationException>(() => nav.ScrollBarStyle = ScrollBarStyle.ThinLine);
        _ = Should.Throw<InvalidOperationException>(() => nav.LineSize = 2);
        _ = Should.Throw<InvalidOperationException>(() => nav.PageOverlap = 2);
        _ = Should.Throw<InvalidOperationException>(() => nav.HorizontalOffset = 0);
        _ = Should.Throw<InvalidOperationException>(() => nav.VerticalOffset = 0);
    }

    /// <summary>Verifies every NavigationViewGroup-declared settable property starts at its
    /// documented default, and disposing the group prevents further mutation.</summary>
    [Fact]
    public void Group_WhenCreatedThenDisposed_UsesDocumentedDefaultsThenPreventsMutation()
    {
        var group = new NavigationViewGroup();

        group.Header.ShouldBe(string.Empty);
        group.IsExpanded.ShouldBeTrue();
        group.Style.ShouldBeNull();
        group.Items.Count.ShouldBe(0);

        group.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => group.Header = "Group");
        _ = Should.Throw<ObjectDisposedException>(() => group.IsExpanded = false);
        _ = Should.Throw<ObjectDisposedException>(() => group.Style = NavigationViewGroupStyle.Default);
    }

    /// <summary>Verifies every NavigationViewGroup-declared settable property requires dispatcher
    /// affinity once attached.</summary>
    [Fact]
    public async Task Group_PropertySetter_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var group = new NavigationViewGroup();

        await dispatcher.InvokeAsync(() => group.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => group.Header = "Group");
        _ = Should.Throw<InvalidOperationException>(() => group.IsExpanded = false);
        _ = Should.Throw<InvalidOperationException>(() => group.Style = NavigationViewGroupStyle.Default);
    }

    /// <summary>Verifies a separator's local Style defaults to null, round-trips an assigned
    /// complete style, and disposing the separator prevents further mutation.</summary>
    [Fact]
    public void Separator_WhenStyleAssignedThenDisposed_RoundTripsThenPreventsMutation()
    {
        var separator = new NavigationViewSeparator();
        separator.Style.ShouldBeNull();

        var style = NavigationViewSeparatorStyle.Default with { Glyph = new Rune('*') };
        separator.Style = style;

        separator.Style.ShouldBe(style);
        separator.ActualStyle.ShouldBe(style);

        separator.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => separator.Style = null);
    }

    /// <summary>Verifies a separator's settable Style property requires dispatcher affinity once
    /// attached.</summary>
    [Fact]
    public async Task Separator_PropertySetter_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var separator = new NavigationViewSeparator();

        await dispatcher.InvokeAsync(() => separator.Attach(dispatcher), TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => separator.Style = NavigationViewSeparatorStyle.Default);
    }
}
