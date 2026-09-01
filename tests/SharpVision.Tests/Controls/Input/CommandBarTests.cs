// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CommandBar defaults, typed ownership, selection, and canonical activation.</summary>
public sealed class CommandBarTests
{
    /// <summary>Verifies a new bar is one empty, focusable Tab stop with no overflow session.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        using var bar = new CommandBar();

        bar.Items.ShouldBeEmpty();
        bar.Spacing.ShouldBe(1);
        bar.SelectedIndex.ShouldBe(-1);
        bar.SelectedItem.ShouldBeNull();
        bar.IsOverflowOpen.ShouldBeFalse();
        bar.Style.ShouldBeNull();
        bar.ActualStyle.ShouldBe(CommandBarStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        bar.IsFocusable.ShouldBeTrue();
        bar.IsTabStop.ShouldBeTrue();
        bar.TabNavigation.ShouldBe(TabNavigation.None);
    }

    /// <summary>Verifies an item enables the shared caption, command, and press-facing defaults.</summary>
    [Fact]
    public void ItemConstructor_WhenCreated_UsesDocumentedDefaults()
    {
        using var item = new CommandBarItem();

        item.Text.ShouldBeEmpty();
        item.Command.ShouldBeNull();
        item.CommandParameter.ShouldBeNull();
        item.StartAffix.ShouldBeNull();
        item.EndAffix.ShouldBeNull();
        item.IsOverflowed.ShouldBeFalse();
        item.Style.ShouldBeNull();
        item.ActualStyle.ShouldBe(CommandBarItemStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        item.IsFocusable.ShouldBeTrue();
        item.IsTabStop.ShouldBeTrue();
    }

    /// <summary>Verifies a separator is passive and starts with its resolved default presentation.</summary>
    [Fact]
    public void SeparatorConstructor_WhenCreated_UsesDocumentedDefaults()
    {
        using var separator = new CommandBarSeparator();

        separator.Style.ShouldBeNull();
        separator.ActualStyle.ShouldBe(CommandBarSeparatorStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        separator.IsFocusable.ShouldBeFalse();
        separator.IsTabStop.ShouldBeFalse();
        separator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies spacing validation happens before the current value can change.</summary>
    [Fact]
    public void Spacing_WhenNegative_RejectsBeforeMutation()
    {
        using var bar = new CommandBar { Spacing = 3 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Spacing = -1);

        bar.Spacing.ShouldBe(3);
    }

    /// <summary>Verifies both typed entry kinds retain source order and semantic ancestry.</summary>
    [Fact]
    public void Items_WhenItemAndSeparatorAreAdded_OwnsThemInSourceOrder()
    {
        using var bar = new CommandBar();
        var item = new CommandBarItem { Text = "Run" };
        var separator = new CommandBarSeparator();

        bar.Items.Add(item);
        bar.Items.Add(separator);

        bar.Items.ShouldBe([item, separator]);
        var host = item.Parent.ShouldNotBeNull();
        host.Parent.ShouldBeSameAs(bar);
        separator.Parent.ShouldBeSameAs(item.Parent);
    }

    /// <summary>Verifies bar-private face overrides do not erase caller-authored values on detach.</summary>
    [Fact]
    public void Items_WhenItemIsRemoved_RestoresAuthoredFacePropertiesWithoutDisposal()
    {
        using var bar = new CommandBar();
        using var item = new CommandBarItem
        {
            IsFocusable = true,
            IsTabStop = true,
            Height = Length.Cells(4)
        };
        bar.Items.Add(item);

        item.IsFocusable.ShouldBeFalse();
        item.IsTabStop.ShouldBeFalse();
        item.Height.ShouldBe(Length.Cells(1));

        bar.Items.Remove(item).ShouldBeTrue();

        item.Parent.ShouldBeNull();
        item.IsDisposed.ShouldBeFalse();
        item.IsFocusable.ShouldBeTrue();
        item.IsTabStop.ShouldBeTrue();
        item.Height.ShouldBe(Length.Cells(4));
    }

    /// <summary>Verifies the collection indexer refuses arbitrary controls without replacing the entry.</summary>
    [Fact]
    public void Items_WhenArbitraryControlIsAssigned_RejectsBeforeCollectionChanges()
    {
        using var bar = new CommandBar();
        var original = new CommandBarItem { Text = "Run" };
        using var arbitrary = new Button();
        bar.Items.Add(original);

        _ = Should.Throw<InvalidOperationException>(() => bar.Items[0] = arbitrary);

        bar.Items.ShouldBe([original]);
        _ = original.Parent.ShouldNotBeNull();
        arbitrary.Parent.ShouldBeNull();
    }

    /// <summary>Verifies duplicate and cross-owner insertion are rejected before either collection changes.</summary>
    [Fact]
    public void Items_WhenEntryAlreadyBelongsToATree_RejectsBeforeMutation()
    {
        using var first = new CommandBar();
        using var second = new CommandBar();
        var item = new CommandBarItem();
        first.Items.Add(item);

        _ = Should.Throw<ArgumentException>(() => first.Items.Add(item));
        _ = Should.Throw<ArgumentException>(() => second.Items.Add(item));

        first.Items.ShouldBe([item]);
        second.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies replacement, movement, and removal preserve entry identity and order.</summary>
    [Fact]
    public void Items_WhenMutated_PreservesIdentityAndDetachesReplacedEntries()
    {
        using var bar = new CommandBar();
        using var first = new CommandBarItem { Text = "First" };
        var middle = new CommandBarSeparator();
        var last = new CommandBarItem { Text = "Last" };
        var replacement = new CommandBarItem { Text = "Replacement" };
        bar.Items.Add(first);
        bar.Items.Add(middle);
        bar.Items.Add(last);

        bar.Items.Move(2, 0);
        bar.Items[1] = replacement;
        bar.Items.RemoveAt(2);

        bar.Items.ShouldBe([last, replacement]);
        first.Parent.ShouldBeNull();
        middle.Parent.ShouldBeNull();
        _ = replacement.Parent.ShouldNotBeNull();
        bar.Items.IndexOf(last).ShouldBe(0);
        bar.Items.IndexOf(first).ShouldBe(-1);
    }

    /// <summary>Verifies clearing detaches every entry and leaves caller-owned instances alive.</summary>
    [Fact]
    public void Items_WhenCleared_DetachesWithoutDisposal()
    {
        using var bar = new CommandBar();
        using var item = new CommandBarItem();
        using var separator = new CommandBarSeparator();
        bar.Items.Add(item);
        bar.Items.Add(separator);

        bar.Items.Clear();

        bar.Items.ShouldBeEmpty();
        item.Parent.ShouldBeNull();
        separator.Parent.ShouldBeNull();
        item.IsDisposed.ShouldBeFalse();
        separator.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies a bar disposes entries whose ownership it still retains.</summary>
    [Fact]
    public void Dispose_WhenEntriesRemainOwned_DisposesEntries()
    {
        var bar = new CommandBar();
        var item = new CommandBarItem();
        var separator = new CommandBarSeparator();
        bar.Items.Add(item);
        bar.Items.Add(separator);

        bar.Dispose();

        item.IsDisposed.ShouldBeTrue();
        separator.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies child-initiated disposal removes ownership and repairs selected identity.</summary>
    [Fact]
    public void Dispose_WhenSelectedEntryDisposesDirectly_RepairsToNearestAvailableSibling()
    {
        using var bar = new CommandBar();
        var first = new CommandBarItem { Text = "First" };
        var selected = new CommandBarItem { Text = "Selected" };
        var last = new CommandBarItem { Text = "Last" };
        bar.Items.Add(first);
        bar.Items.Add(selected);
        bar.Items.Add(last);
        bar.SelectedItem = selected;

        selected.Dispose();

        bar.Items.ShouldBe([first, last]);
        bar.SelectedItem.ShouldBeSameAs(last);
        bar.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies selection accepts only owned visible enabled command items.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsInvalid_RejectsBeforeMutation()
    {
        using var bar = new CommandBar();
        var first = new CommandBarItem();
        var separator = new CommandBarSeparator();
        var disabled = new CommandBarItem { IsEnabled = false };
        var hidden = new CommandBarItem { Visibility = Visibility.Hidden };
        bar.Items.Add(first);
        bar.Items.Add(separator);
        bar.Items.Add(disabled);
        bar.Items.Add(hidden);
        bar.SelectedIndex = 0;

        _ = Should.Throw<ArgumentException>(() => bar.SelectedIndex = 1);
        _ = Should.Throw<InvalidOperationException>(() => bar.SelectedIndex = 2);
        _ = Should.Throw<InvalidOperationException>(() => bar.SelectedIndex = 3);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.SelectedIndex = 4);

        bar.SelectedItem.ShouldBeSameAs(first);
        bar.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies a foreign SelectedItem is the documented clear-selection action.</summary>
    [Fact]
    public void SelectedItem_WhenForeign_ClearsSelection()
    {
        using var bar = new CommandBar();
        var owned = new CommandBarItem();
        using var foreign = new CommandBarItem();
        bar.Items.Add(owned);
        bar.SelectedItem = owned;

        bar.SelectedItem = foreign;

        bar.SelectedIndex.ShouldBe(-1);
        bar.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies moving an unrelated slot adjusts only the numeric selection position.</summary>
    [Fact]
    public void Items_WhenSelectedIdentityMoves_PreservesSelectedItem()
    {
        using var bar = new CommandBar();
        var first = new CommandBarItem { Text = "First" };
        var second = new CommandBarItem { Text = "Second" };
        var third = new CommandBarItem { Text = "Third" };
        bar.Items.Add(first);
        bar.Items.Add(second);
        bar.Items.Add(third);
        bar.SelectedItem = second;

        bar.Items.Move(0, 2);

        bar.SelectedItem.ShouldBeSameAs(second);
        bar.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies removing selection chooses the nearest available command sibling.</summary>
    [Fact]
    public void Items_WhenSelectedItemIsRemoved_SelectsNearestAvailableSibling()
    {
        using var bar = new CommandBar();
        var first = new CommandBarItem { Text = "First" };
        var separator = new CommandBarSeparator();
        var last = new CommandBarItem { Text = "Last" };
        bar.Items.Add(first);
        bar.Items.Add(separator);
        bar.Items.Add(last);
        bar.SelectedItem = first;

        _ = bar.Items.Remove(first);

        bar.SelectedItem.ShouldBeSameAs(last);
        bar.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies a live selected item becoming unavailable repairs forward and then backward.</summary>
    [Fact]
    public void Availability_WhenSelectedItemBecomesUnavailable_SelectsNearestAvailableSibling()
    {
        using var bar = new CommandBar();
        var first = new CommandBarItem { Text = "First" };
        var middle = new CommandBarItem { Text = "Middle" };
        var last = new CommandBarItem { Text = "Last" };
        bar.Items.Add(first);
        bar.Items.Add(middle);
        bar.Items.Add(last);
        bar.SelectedItem = middle;

        middle.IsEnabled = false;
        bar.SelectedItem.ShouldBeSameAs(last);

        last.Visibility = Visibility.Hidden;
        bar.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>Verifies command denial suppresses both item and bar events as well as execution.</summary>
    [Fact]
    public void PerformInvoke_WhenCommandCannotExecute_RaisesNoEvents()
    {
        using var bar = new CommandBar();
        var command = new ProbeCommand { CanExecuteValue = false };
        var item = new CommandBarItem { Command = command };
        var itemEvents = 0;
        var barEvents = 0;
        item.Invoked += (_, _) => itemEvents++;
        bar.ItemInvoked += (_, _) => barEvents++;
        bar.Items.Add(item);

        item.PerformInvoke();

        command.Queries.ShouldBe([null]);
        command.Executions.ShouldBeEmpty();
        itemEvents.ShouldBe(0);
        barEvents.ShouldBe(0);
    }

    /// <summary>Verifies the accepted canonical order is item event, bar event, then command.</summary>
    [Fact]
    public void PerformInvoke_WhenAllowed_RaisesItemThenBarThenExecutesCapturedCommand()
    {
        using var bar = new CommandBar();
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var item = new CommandBarItem { Command = command, CommandParameter = parameter };
        item.Invoked += (_, eventArgs) =>
        {
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("item");
        };
        bar.ItemInvoked += (_, eventArgs) =>
        {
            eventArgs.Item.ShouldBeSameAs(item);
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("bar");
        };
        bar.Items.Add(item);

        item.PerformInvoke();

        order.ShouldBe(["item", "bar", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies callback rebinding cannot redirect one already accepted action.</summary>
    [Fact]
    public void PerformInvoke_WhenCallbacksReplaceCommandAndSelection_ExecutesCapturedBindingOnce()
    {
        using var bar = new CommandBar();
        var originalParameter = new object();
        var replacementParameter = new object();
        var original = new ProbeCommand();
        var replacement = new ProbeCommand();
        var first = new CommandBarItem { Command = original, CommandParameter = originalParameter };
        var second = new CommandBarItem();
        bar.Items.Add(first);
        bar.Items.Add(second);
        bar.SelectedItem = first;
        first.Invoked += (_, _) =>
        {
            first.Command = replacement;
            first.CommandParameter = replacementParameter;
            bar.SelectedItem = second;
            bar.Style = CommandBarStyle.Default with { Padding = new Thickness(1) };
        };

        first.PerformInvoke();

        original.Executions.ShouldBe([originalParameter]);
        replacement.Executions.ShouldBeEmpty();
        bar.SelectedItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies source removal from the item callback suppresses stale bar and command stages.</summary>
    [Fact]
    public void PerformInvoke_WhenItemHandlerRemovesSource_SuppressesBarEventAndCommand()
    {
        using var bar = new CommandBar();
        var command = new ProbeCommand();
        var item = new CommandBarItem { Command = command };
        var barEvents = 0;
        bar.Items.Add(item);
        item.Invoked += (_, _) => _ = bar.Items.Remove(item);
        bar.ItemInvoked += (_, _) => barEvents++;

        item.PerformInvoke();

        barEvents.ShouldBe(0);
        command.Executions.ShouldBeEmpty();
        item.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a nested newer activation suppresses the older command continuation.</summary>
    [Fact]
    public void PerformInvoke_WhenBarHandlerStartsNestedActivation_SuppressesOuterCommand()
    {
        using var bar = new CommandBar();
        var firstCommand = new ProbeCommand();
        var secondCommand = new ProbeCommand();
        var first = new CommandBarItem { Command = firstCommand };
        var second = new CommandBarItem { Command = secondCommand };
        bar.Items.Add(first);
        bar.Items.Add(second);
        bar.ItemInvoked += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Item, first))
            {
                second.PerformInvoke();
            }
        };

        first.PerformInvoke();

        firstCommand.Executions.ShouldBeEmpty();
        secondCommand.Executions.ShouldBe([null]);
    }

    /// <summary>Verifies callback failure is rethrown only after later accepted stages complete.</summary>
    [Fact]
    public void PerformInvoke_WhenItemEventThrows_CompletesBarAndCommandThenRethrowsEarliestFailure()
    {
        using var bar = new CommandBar();
        var failure = new FormatException("item callback");
        var command = new ProbeCommand();
        var barEvents = 0;
        var item = new CommandBarItem { Command = command };
        item.Invoked += (_, _) => throw failure;
        bar.ItemInvoked += (_, _) => barEvents++;
        bar.Items.Add(item);

        var thrown = Should.Throw<FormatException>(item.PerformInvoke);

        thrown.ShouldBeSameAs(failure);
        barEvents.ShouldBe(1);
        command.Executions.ShouldBe([null]);
    }

    /// <summary>Verifies unavailability committed by the bar callback suppresses the captured command.</summary>
    [Fact]
    public void PerformInvoke_WhenBarHandlerHidesSource_SuppressesCommandStage()
    {
        using var bar = new CommandBar();
        var command = new ProbeCommand();
        var item = new CommandBarItem { Command = command };
        bar.Items.Add(item);
        bar.ItemInvoked += (_, _) => item.Visibility = Visibility.Hidden;

        item.PerformInvoke();

        command.Executions.ShouldBeEmpty();
        bar.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies detached hidden and disabled entries cannot activate.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    [InlineData(true, Visibility.Collapsed)]
    public void PerformInvoke_WhenUnavailable_IsNoOp(bool enabled, Visibility visibility)
    {
        using var item = new CommandBarItem { IsEnabled = enabled, Visibility = visibility };
        var command = new ProbeCommand();
        var invoked = 0;
        item.Command = command;
        item.Invoked += (_, _) => invoked++;

        item.PerformInvoke();

        invoked.ShouldBe(0);
        command.Queries.ShouldBeEmpty();
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies disposed programmatic activation reports the lifetime error.</summary>
    [Fact]
    public void PerformInvoke_WhenDisposed_Throws()
    {
        var item = new CommandBarItem();
        item.Dispose();

        _ = Should.Throw<ObjectDisposedException>(item.PerformInvoke);
    }

    /// <summary>Verifies event payload validation rejects invalid values and preserves valid identity.</summary>
    [Fact]
    public void CommandBarItemInvokedEventArgs_WhenValuesVary_ValidatesBeforeAssignment()
    {
        using var item = new CommandBarItem();

        _ = Should.Throw<ArgumentNullException>(() => new CommandBarItemInvokedEventArgs(null!, ActivationCause.Pointer));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new CommandBarItemInvokedEventArgs(item, (ActivationCause) 99));

        var eventArgs = new CommandBarItemInvokedEventArgs(item, ActivationCause.Keyboard);
        eventArgs.Item.ShouldBeSameAs(item);
        eventArgs.Cause.ShouldBe(ActivationCause.Keyboard);
    }
}
