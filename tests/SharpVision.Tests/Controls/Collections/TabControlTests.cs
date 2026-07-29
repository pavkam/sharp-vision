// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TabControl typed ownership, selection, repair, events, layout, and validation.</summary>
public sealed class TabControlTests
{
    /// <summary>Verifies documented defaults for a new TabControl.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var tabs = new TabControl();

        // Assert
        tabs.Items.ShouldBeEmpty();
        tabs.SelectedIndex.ShouldBe(-1);
        tabs.DividerColor.ShouldBeNull();
        tabs.SelectionIndicatorColor.ShouldBeNull();
        tabs.CanFocus.ShouldBeTrue();
        tabs.IsHitTestVisible.ShouldBeTrue();
        tabs.HeaderWidth.ShouldBe(Length.Auto);
        tabs.HeaderOverflowPolicy.ShouldBe(TabHeaderOverflowPolicy.Clip);
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
        new Engine().Layout(tabs, new Size(10, 4));

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

    /// <summary>Verifies only selected content is arranged below retained headers and separator rows.</summary>
    [Fact]
    public void Layout_WhenSelectionChanges_ExcludesOldContentAndArrangesNewContent()
    {
        var first = Create("General", "General body");
        var second = Create("界", "Wide body");
        var tabs = Create(first, second);
        var engine = new Engine();

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
