// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TabControl typed ownership, selection, repair, events, layout, and validation.</summary>
public sealed class TabControlTests
{
    /// <summary>Verifies documented defaults for a new TabControl.</summary>
    [ComponentUnitEvidence(typeof(TabControl))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var tabs = new TabControl();

        // Assert
        tabs.Items.ShouldBeEmpty();
        tabs.SelectedIndex.ShouldBe(-1);
        tabs.SelectedItem.ShouldBeNull();
        tabs.DividerColor.ShouldBeNull();
        tabs.SelectionIndicatorColor.ShouldBeNull();
        tabs.CanFocus.ShouldBeTrue();
        tabs.IsHitTestVisible.ShouldBeTrue();
        tabs.HeaderWidth.ShouldBe(Length.Auto);
        tabs.HeaderOverflowPolicy.ShouldBe(TabHeaderOverflowPolicy.Clip);
    }

    /// <summary>Verifies SelectedItem mirrors SelectedIndex and reports the selected page identity.</summary>
    [Fact]
    public void SelectedItem_WhenSet_UpdatesSelectedIndexAndReportsIdentity()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);

        tabs.SelectedItem = second;

        tabs.SelectedIndex.ShouldBe(1);
        tabs.SelectedItem.ShouldBeSameAs(second);

        tabs.SelectedIndex = 0;

        tabs.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>Verifies setting SelectedItem to null clears selection, matching SelectedIndex = -1.</summary>
    [Fact]
    public void SelectedItem_WhenSetToNull_ClearsSelection()
    {
        var tabs = Create(Create("First", "One"));
        tabs.SelectedIndex = 0;

        tabs.SelectedItem = null;

        tabs.SelectedIndex.ShouldBe(-1);
        tabs.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies setting SelectedItem to a page this control does not own clears selection.</summary>
    [Fact]
    public void SelectedItem_WhenItemIsNotOwned_ClearsSelection()
    {
        var owned = Create("First", "One");
        var tabs = Create(owned);
        tabs.SelectedIndex = 0;
        var foreign = Create("Foreign", "Elsewhere");

        tabs.SelectedItem = foreign;

        tabs.SelectedIndex.ShouldBe(-1);
        tabs.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies the selection-changed event carries the previous and current page identities.</summary>
    [Fact]
    public void SelectionChanged_WhenSelectionChanges_CarriesItemIdentities()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 0;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.SelectedIndex = 1;

        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(first),
            args => args.CurrentItem.ShouldBeSameAs(second));
    }

    /// <summary>Verifies removing the selected page reports its identity as PreviousItem, not a shifted successor.</summary>
    [Fact]
    public void SelectionChanged_WhenSelectedPageIsRemoved_ReportsRemovedItemAsPrevious()
    {
        var selected = Create("Selected", "One");
        var next = Create("Next", "Two");
        var tabs = Create(selected, next);
        tabs.SelectedIndex = 0;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Remove(selected).ShouldBeTrue();

        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(selected),
            args => args.CurrentItem.ShouldBeSameAs(next));
    }

    /// <summary>Verifies clearing every page while one is selected reports its identity as PreviousItem.</summary>
    [Fact]
    public void SelectionChanged_WhenCleared_ReportsPreviousItemAndNoCurrentItem()
    {
        var selected = Create("Selected", "One");
        var tabs = Create(selected);
        tabs.SelectedIndex = 0;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Clear();

        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(selected),
            args => args.CurrentItem.ShouldBeNull());
        tabs.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies a close request can be cancelled or accepted before removal.</summary>
    [Fact]
    public void Close_WhenRequested_RaisesCancellableEventBeforeRemoval()
    {
        var first = Create("First", "One");
        first.IsClosable = true;
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        var requests = 0;
        tabs.CloseRequested += (_, args) =>
        {
            requests++;
            args.Cancel = requests == 1;
        };

        tabs.RequestClose(first).ShouldBeFalse();
        tabs.Items.ShouldContain(first);

        tabs.RequestClose(first).ShouldBeTrue();
        tabs.Items.ShouldNotContain(first);
        tabs.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies non-closeable pages reject close requests without raising the event.</summary>
    [Fact]
    public void Close_WhenPageIsNotClosable_DoesNothing()
    {
        var item = Create("First", "One");
        var tabs = Create(item);
        var raised = false;
        tabs.CloseRequested += (_, _) => raised = true;

        tabs.RequestClose(item).ShouldBeFalse();

        raised.ShouldBeFalse();
        tabs.Items.ShouldContain(item);
    }

    /// <summary>Verifies configured header lengths and scrolling policy reach retained headers.</summary>
    [Fact]
    public void HeaderLayout_WhenConfigured_UsesLengthAndOverflowPolicy()
    {
        var tabs = Create(Create("A", "One"), Create("B", "Two"), Create("C", "Three"));

        tabs.HeaderWidth = Length.Cells(5);
        tabs.HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll;
        tabs.SelectedIndex = 2;
        new LayoutEngine().Layout(tabs, new Size(10, 4));

        tabs.HeaderAt(0).Bounds.Width.ShouldBe(5);
        tabs.HeaderAt(2).Bounds.Width.ShouldBe(5);
        tabs.HeaderOverflowPolicy.ShouldBe(TabHeaderOverflowPolicy.Scroll);
    }

    /// <summary>Verifies custom glyphs override defaults and ResetGlyphs restores them.</summary>
    [Fact]
    public void Glyphs_WhenCustomized_OverrideDefaultsAndResetRestores()
    {
        // Arrange
        var tabs = new TabControl();
        var defaultDivider = tabs.DividerGlyph;
        var defaultUnderline = tabs.UnderlineGlyph;

        // Act
        tabs.DividerGlyph = new Rune('|');
        tabs.UnderlineGlyph = new Rune('=');

        // Assert custom
        tabs.DividerGlyph.ShouldBe(new Rune('|'));
        tabs.UnderlineGlyph.ShouldBe(new Rune('='));

        // Act reset
        tabs.ResetGlyphs();

        // Assert restored
        tabs.DividerGlyph.ShouldBe(defaultDivider);
        tabs.UnderlineGlyph.ShouldBe(defaultUnderline);
    }

    /// <summary>Verifies the divider glyph between headers survives on top of the header
    /// Stack's own opaque background instead of being overwritten every frame.</summary>
    [Fact]
    public void Render_WhenTwoHeadersArePresent_DrawsDividerOnTopOfHeaderBackground()
    {
        var tabs = Create(Create("A", "One"), Create("B", "Two"));
        tabs.HeaderWidth = Length.Cells(5);
        tabs.DividerGlyph = new Rune('|');
        var size = new Size(10, 4);
        new LayoutEngine().Layout(tabs, size);
        using Frame frame = new(size);

        tabs.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("|");
    }

    /// <summary>Verifies color properties reject transparent values before mutation.</summary>
    [Fact]
    public void ColorProperties_WhenTransparentIsAssigned_ThrowBeforeMutation()
    {
        // Arrange
        var tabs = new TabControl();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => tabs.DividerColor = Color.Transparent);
        _ = Should.Throw<ArgumentException>(() => tabs.SelectionIndicatorColor = Color.Transparent);
        tabs.DividerColor.ShouldBeNull();
        tabs.SelectionIndicatorColor.ShouldBeNull();
    }

    /// <summary>Verifies disposing the TabControl prevents direct property mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var tabs = new TabControl();

        // Act
        tabs.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() =>
            tabs.DividerColor = Color.Rgb(0xff, 0, 0));
    }

    /// <summary>Verifies disposed collection mutations fail before changing the tab set.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsRemoveAndClear()
    {
        var item = Create("First", "One");
        var tabs = Create(item);
        tabs.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.Remove(item));
        _ = Should.Throw<ObjectDisposedException>(tabs.Items.Clear);
    }

    /// <summary>Verifies keyboard Left/Right/Home/End navigate between eligible tabs.</summary>
    [Fact]
    public void Keyboard_WhenArrowsArePressed_NavigatesEligibleTabs()
    {
        // Arrange
        var first = Create("First", "One");
        var disabled = Create("Disabled", "Two");
        disabled.IsEnabled = false;
        var third = Create("Third", "Three");
        var tabs = Create(first, disabled, third);

        // Act Right skips disabled
        Key(tabs, Code.Right);

        // Assert
        tabs.SelectedIndex.ShouldBe(2);

        // Act Right wraps
        Key(tabs, Code.Right);
        tabs.SelectedIndex.ShouldBe(0);

        // Act Home and End
        Key(tabs, Code.End);
        tabs.SelectedIndex.ShouldBe(2);
        Key(tabs, Code.Home);
        tabs.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies dynamic header text changes propagate to the rendered header.</summary>
    [Fact]
    public void Header_WhenChanged_UpdatesDisplayedHeader()
    {
        // Arrange
        var item = Create("Original", "Content");
        var tabs = Create(item);
        tabs.HeaderAt(0).Header.ShouldBe("Original");

        // Act
        item.Header = "Updated";

        // Assert
        tabs.HeaderAt(0).Header.ShouldBe("Updated");
    }

    /// <summary>Verifies the first eligible page auto-selects and every semantic item enters the private host.</summary>
    [Fact]
    public void Items_WhenPagesAreAdded_AutoSelectsFirstEligibleOwnedPage()
    {
        var disabled = Create("Disabled", "No");
        disabled.IsEnabled = false;
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = new TabControl();

        tabs.Items.Add(disabled);
        tabs.Items.Add(first);
        tabs.Items.Add(second);

        tabs.Items.ShouldBe([disabled, first, second]);
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
        IsHeaderSelected(tabs, 0).ShouldBeFalse();
        IsHeaderSelected(tabs, 1).ShouldBeTrue();
        IsHeaderSelected(tabs, 2).ShouldBeFalse();
        first.Parent.ShouldNotBeNull().Parent.ShouldBeSameAs(tabs);
    }

    /// <summary>Verifies changed selection publishes once after page and retained-header state commit.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_PublishesCommittedIdentityOnce()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        var observations = new List<string>();
        tabs.SelectionChanged += (_, _) => observations.Add(
            $"{tabs.SelectedIndex}:{IsHeaderSelected(tabs, 0)}:{IsHeaderSelected(tabs, 1)}");

        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = -1;

        observations.ShouldBe(["1:False:True", "-1:False:False"]);
        tabs.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies selected removal chooses successor then predecessor.</summary>
    [Fact]
    public void Items_WhenSelectedPagesAreRemoved_RepairsToNearestEligibility()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var successor = Create("Successor", "Three");
        var tabs = Create(first, selected, successor);
        tabs.SelectedIndex = 1;

        tabs.Items.Remove(selected).ShouldBeTrue();

        selected.Parent.ShouldBeNull();
        selected.IsDisposed.ShouldBeFalse();
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(successor);

        tabs.Items.Remove(successor).ShouldBeTrue();

        tabs.SelectedIndex.ShouldBe(0);
        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
    }

    /// <summary>Verifies selected removal publishes the removed index and the no-selection transition.</summary>
    [Fact]
    public void Items_WhenSelectedPageRemovalLeavesNoEligiblePage_PublishesOriginalIndexAndNoSelection()
    {
        var selected = Create("Selected", "Two");
        var tabs = Create(selected);
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Remove(selected).ShouldBeTrue();

        tabs.SelectedIndex.ShouldBe(-1);
        changes.Count.ShouldBe(1);
        changes[0].PreviousIndex.ShouldBe(0);
        changes[0].CurrentIndex.ShouldBe(-1);
    }

    /// <summary>Verifies removal before the selected page notifies the shifted index and preserves presentation.</summary>
    [Fact]
    public void Items_WhenPageBeforeSelectionIsRemoved_NotifiesShiftedSelectionAndKeepsPresentationConsistent()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var tabs = Create(first, selected);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Remove(first).ShouldBeTrue();

        tabs.SelectedIndex.ShouldBe(0);
        tabs.Items[0].ShouldBeSameAs(selected);
        IsHeaderSelected(tabs, 0).ShouldBeTrue();
        changes.Count.ShouldBe(1);
        changes[0].PreviousIndex.ShouldBe(1);
        changes[0].CurrentIndex.ShouldBe(0);
    }

    /// <summary>Verifies disabling or collapsing the selected page chooses the nearest eligible page.</summary>
    [Fact]
    public void Availability_WhenSelectedPageBecomesUnavailable_RepairsSelection()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        tabs.SelectedIndex = 1;

        second.IsEnabled = false;

        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(third);

        third.Visibility = Visibility.Collapsed;

        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
        IsHeaderSelected(tabs, 0).ShouldBeTrue();
    }

    /// <summary>Verifies a consumer's Visibility write, made from a PropertyChanged handler while
    /// ApplyPresentation's own write for that item is still in flight, is honored instead of being
    /// silently discarded and re-issued on every following pass - the reentrancy previously
    /// misattributed to the control itself by the _updatingPresentation guard (see #199).</summary>
    [Fact]
    public void ApplyPresentation_WhenConsumerVetoesVisibilityFromPropertyChanged_HonorsTheVetoAndRepairsSelection()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 1;

        first.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ControlBase.Visibility) && first.Visibility == Visibility.Visible)
            {
                first.Visibility = Visibility.Collapsed;
            }
        };

        tabs.SelectedIndex = 0;

        first.Visibility.ShouldBe(Visibility.Collapsed);
        tabs.SelectedIndex.ShouldBe(1);
        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(second);
        second.Visibility.ShouldBe(Visibility.Visible);

        // A further layout pass must not re-issue the discarded request: the veto stands.
        var laterVisibilityChanges = 0;
        first.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ControlBase.Visibility))
            {
                laterVisibilityChanges++;
            }
        };
        new LayoutEngine().Layout(tabs, new Size(20, 5));

        laterVisibilityChanges.ShouldBe(0);
        first.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies Clear detaches every page while clearing selection.</summary>
    [Fact]
    public void Items_WhenCleared_DetachesWithoutDisposalAndClearsSelection()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var tabs = Create(first, selected);
        tabs.SelectedIndex = 1;

        tabs.Items.Clear();

        tabs.Items.ShouldBeEmpty();
        tabs.SelectedIndex.ShouldBe(-1);
        first.Parent.ShouldBeNull();
        selected.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        selected.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies removal restores the item's authored Width, Height, and Visibility.</summary>
    [Fact]
    public void Items_WhenTabIsRemoved_RestoresAuthoredWidthHeightAndVisibility()
    {
        var item = Create("First", "One");
        item.Width = Length.Cells(12);
        item.Height = Length.Cells(4);
        item.Visibility = Visibility.Hidden;
        var tabs = Create(item);

        _ = tabs.Items.Remove(item);

        item.Width.ShouldBe(Length.Cells(12));
        item.Height.ShouldBe(Length.Cells(4));
        item.Visibility.ShouldBe(Visibility.Hidden);
    }

    /// <summary>Verifies clearing restores the authored visibility of every detached item.</summary>
    [Fact]
    public void Items_WhenTabsAreCleared_RestoresAuthoredVisibilityOnEveryDetachedItem()
    {
        var visible = Create("Visible", "One");
        var hidden = Create("Hidden", "Two");
        hidden.Visibility = Visibility.Hidden;
        var tabs = Create(visible, hidden);

        tabs.Items.Clear();

        visible.Visibility.ShouldBe(Visibility.Visible);
        hidden.Visibility.ShouldBe(Visibility.Hidden);
    }

    /// <summary>
    /// Verifies a tab removed while unselected (and therefore Collapsed by this
    /// control's private presentation policy) is selectable again after moving
    /// to a different TabControl. Before the restore-on-detach fix, AddItem
    /// captured the item's leftover Collapsed visibility as its next owner's
    /// requested visibility, making the item permanently unselectable anywhere.
    /// </summary>
    [Fact]
    public void Items_WhenRemovedTabIsAddedToAnotherTabControl_IsSelectableAndRendersItsContent()
    {
        var selected = Create("Selected", "One");
        var moved = Create("Moved", "Two");
        var first = Create(selected, moved);
        first.SelectedIndex = 0;

        _ = first.Items.Remove(moved);

        var second = Create(moved);

        second.SelectedIndex.ShouldBe(0);
        second.Items[second.SelectedIndex].ShouldBeSameAs(moved);
        IsHeaderSelected(second, 0).ShouldBeTrue();
    }

    /// <summary>Verifies invalid selected indexes and unavailable targets preserve the committed page.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsInvalid_PreservesSelectionBeforeThrowing()
    {
        var first = Create("First", "One");
        var disabled = Create("Disabled", "Two");
        disabled.IsEnabled = false;
        var tabs = Create(first, disabled);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = -2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = 2);
        _ = Should.Throw<InvalidOperationException>(() => tabs.SelectedIndex = 1);

        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
        IsHeaderSelected(tabs, 0).ShouldBeTrue();
    }

    /// <summary>Verifies collection validation rejects invalid candidates before ownership or selection changes.</summary>
    [Fact]
    public void Items_WhenCandidateIsInvalid_PreservesCollectionOwnershipAndSelection()
    {
        var first = Create("First", "One");
        var tabs = Create(first);
        var attached = Create("Attached", "Elsewhere");
        var host = new Stack { Children = { attached } };
        var disposed = Create("Disposed", "Gone");
        disposed.Dispose();

        _ = Should.Throw<ArgumentNullException>(() => tabs.Items.Add(null!));
        _ = Should.Throw<ArgumentException>(() => tabs.Items.Add(first));
        _ = Should.Throw<ArgumentException>(() => tabs.Items.Add(attached));
        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.Add(disposed));

        tabs.Items.ShouldBe([first]);
        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
        _ = first.Parent.ShouldNotBeNull();
        attached.Parent.ShouldBeSameAs(host);
    }

    /// <summary>Verifies inserting before the selected page shifts SelectedIndex without a selection-changed event.</summary>
    [Fact]
    public void Insert_WhenIndexPrecedesSelection_ShiftsSelectedIndexWithoutRaisingSelectionChanged()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);
        var inserted = Create("Inserted", "Zero");

        tabs.Items.Insert(0, inserted);

        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(second);
        changes.ShouldBeEmpty();
        tabs.Items.ShouldBe([inserted, first, second]);
    }

    /// <summary>Verifies inserting into an empty tab control auto-selects the inserted page.</summary>
    [Fact]
    public void Insert_WhenListIsEmpty_AutoSelectsInsertedPage()
    {
        var tabs = new TabControl();
        var item = Create("Only", "Content");

        tabs.Items.Insert(0, item);

        tabs.SelectedIndex.ShouldBe(0);
        tabs.SelectedItem.ShouldBeSameAs(item);
    }

    /// <summary>Verifies an inserted page renders its header at the requested strip position, not appended.</summary>
    [Fact]
    public void Insert_WhenPageIsInsertedBeforeSelection_RendersHeaderStripInNewOrder()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        var inserted = Create("Inserted", "Zero");

        tabs.Items.Insert(0, inserted);

        tabs.HeaderAt(0).Header.ShouldBe("Inserted");
        tabs.HeaderAt(1).Header.ShouldBe("First");
        tabs.HeaderAt(2).Header.ShouldBe("Second");
    }

    /// <summary>Verifies an out-of-range insertion index throws before mutating the collection.</summary>
    [Fact]
    public void Insert_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var tabs = Create(Create("First", "One"));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.Items.Insert(2, Create("New", "Content")));

        tabs.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies inserting an already-owned candidate throws and leaves both collections unchanged.</summary>
    [Fact]
    public void Insert_WhenCandidateIsAlreadyOwnedElsewhere_ThrowsAndLeavesCollectionsUnchanged()
    {
        var owned = Create("Owned", "Content");
        var other = Create(owned);
        var tabs = Create(Create("First", "One"));

        _ = Should.Throw<ArgumentException>(() => tabs.Items.Insert(0, owned));

        tabs.Items.Count.ShouldBe(1);
        other.Items.ShouldBe([owned]);
        owned.Parent.ShouldNotBeNull().Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies removing the selected page by position repairs selection to the nearest eligible page once.</summary>
    [Fact]
    public void RemoveAt_WhenSelectedPageIsRemoved_RepairsSelectionToNearestEligibleOnce()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, selected, third);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.RemoveAt(1);

        tabs.Items.ShouldBe([first, third]);
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(selected),
            args => args.CurrentItem.ShouldBeSameAs(third));
    }

    /// <summary>Verifies an out-of-range removal index throws without mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var tabs = Create(Create("First", "One"));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.Items.RemoveAt(1));

        tabs.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies replacing the selected page detaches the old page without disposing it and selects the new one.</summary>
    [Fact]
    public void Indexer_WhenSelectedPageIsReplaced_DetachesOldWithoutDisposalAndSelectsReplacementOnce()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 0;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);
        var replacement = Create("Replacement", "New");

        tabs.Items[0] = replacement;

        tabs.Items.ShouldBe([replacement, second]);
        tabs.SelectedIndex.ShouldBe(0);
        tabs.SelectedItem.ShouldBeSameAs(replacement);
        first.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeNull();
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            args => args.PreviousItem.ShouldBeSameAs(first),
            args => args.CurrentItem.ShouldBeSameAs(replacement));
    }

    /// <summary>Verifies replacing a non-selected page does not disturb the current selection.</summary>
    [Fact]
    public void Indexer_WhenNonSelectedPageIsReplaced_PreservesSelection()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 0;
        var replacement = Create("Replacement", "New");

        tabs.Items[1] = replacement;

        tabs.SelectedIndex.ShouldBe(0);
        tabs.SelectedItem.ShouldBeSameAs(first);
        tabs.Items.ShouldBe([first, replacement]);
    }

    /// <summary>Verifies assigning a null value through the indexer throws.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_Throws()
    {
        var tabs = Create(Create("First", "One"));

        _ = Should.Throw<ArgumentNullException>(() => tabs.Items[0] = null!);
    }

    /// <summary>Verifies moving the selected page preserves its identity, updates SelectedIndex, and reorders headers.</summary>
    [Fact]
    public void Move_WhenSelectedPageMoves_PreservesIdentityAndReordersHeaders()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, selected, third);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Move(1, 2);

        tabs.Items.ShouldBe([first, third, selected]);
        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(selected);
        changes.ShouldBeEmpty();
        tabs.HeaderAt(2).Header.ShouldBe("Selected");
    }

    /// <summary>Verifies an out-of-range move index throws without mutating the collection.</summary>
    [Fact]
    public void Move_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.Items.Move(0, 2));

        tabs.Items.ShouldBe([first, second]);
    }

    /// <summary>Verifies IndexOf reports the current position of an owned page and -1 for a foreign page.</summary>
    [Fact]
    public void IndexOf_WhenItemIsOwnedOrForeign_ReportsPositionOrNegativeOne()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        var foreign = Create("Foreign", "Elsewhere");

        tabs.Items.IndexOf(second).ShouldBe(1);
        tabs.Items.IndexOf(foreign).ShouldBe(-1);
    }

    /// <summary>Verifies disposed collection mutations reject Insert, RemoveAt, indexer assignment, and Move.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsInsertRemoveAtIndexerAndMove()
    {
        var item = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(item, second);
        tabs.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.Insert(0, Create("New", "Content")));
        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items[0] = Create("New", "Content"));
        _ = Should.Throw<ObjectDisposedException>(() => tabs.Items.Move(0, 1));
    }

    /// <summary>Verifies only selected content is arranged below retained headers and separator rows.</summary>
    [Fact]
    public void Layout_WhenSelectionChanges_ExcludesOldContentAndArrangesNewContent()
    {
        var first = Create("General", "General body");
        var second = Create("界", "Wide body");
        var tabs = Create(first, second);
        var engine = new LayoutEngine();

        engine.Layout(tabs, new Size(20, 5));

        first.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
        second.Content.ShouldNotBeNull().Bounds.ShouldBe(default);

        tabs.SelectedIndex = 1;
        engine.Layout(tabs, new Size(20, 5));

        first.Content.ShouldNotBeNull().Bounds.ShouldBe(default);
        second.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
    }

    private static TabControl Create(params TabItem[] items)
    {
        var result = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var item in items)
        {
            result.Items.Add(item);
        }

        return result;
    }

    private static TabItem Create(string header, string content) => new()
    {
        Header = header,
        Content = new ControlText(content)
    };

    private static bool IsHeaderSelected(TabControl control, int index) =>
        (control.HeaderAt(index).GetAppearanceState() & VisualState.Selected) != 0;

    private static void Key(TabControl control, Code code) => Router.Route(
        control,
        Events.Key,
        new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
}
