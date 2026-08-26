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
        tabs.SelectedItem.ShouldBeNull();
        tabs.Style.ShouldBeNull();
        tabs.ActualStyle.DividerColor.ShouldBe((ControlColor) SemanticColor.ControlBorder);
        tabs.ActualStyle.SelectionIndicatorColor.ShouldBe((ControlColor) SemanticColor.Accent);
        tabs.CanFocus.ShouldBeTrue();
        tabs.IsHitTestVisible.ShouldBeTrue();
        tabs.HeaderWidth.ShouldBe(Length.Auto);
        tabs.HeaderOverflowPolicy.ShouldBe(TabHeaderOverflowPolicy.Clip);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes flip EffectiveIsEnabled and
    /// the derived focus eligibility it drives, and re-enabling restores both.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByAncestor_UpdatesEffectiveEnabledAndFocusEligibility()
    {
        var tabs = new TabControl { IsEnabled = false };

        tabs.EffectiveIsEnabled.ShouldBeFalse();
        tabs.CanFocus.ShouldBeFalse();

        tabs.IsEnabled = true;

        tabs.EffectiveIsEnabled.ShouldBeTrue();
        tabs.CanFocus.ShouldBeTrue();

        var ancestor = new Overlay { Children = { tabs }, IsEnabled = false };

        tabs.IsEnabled.ShouldBeTrue();
        tabs.EffectiveIsEnabled.ShouldBeFalse();
        tabs.CanFocus.ShouldBeFalse();

        ancestor.IsEnabled = true;

        tabs.EffectiveIsEnabled.ShouldBeTrue();
        tabs.CanFocus.ShouldBeTrue();
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

    /// <summary>Verifies a newer selection committed from the SelectedIndex notification
    /// supersedes every remaining notification from the older selection transaction.</summary>
    [Fact]
    public void SelectedIndex_WhenPropertyObserverSelectsNewerPage_SuppressesStaleSelectionEvent()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TabControl.SelectedIndex) && tabs.SelectedIndex == 1)
            {
                tabs.SelectedIndex = 2;
            }
        };
        tabs.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        tabs.SelectedIndex = 1;

        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(third);
        second.Visibility.ShouldBe(Visibility.Collapsed);
        third.Visibility.ShouldBe(Visibility.Visible);
        changes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            eventArgs => eventArgs.CurrentIndex.ShouldBe(2),
            eventArgs => eventArgs.CurrentItem.ShouldBeSameAs(third));
    }

    /// <summary>Verifies the SelectedItem notification is also a transaction boundary: a newer
    /// selection committed there prevents the older typed event from describing stale identity.</summary>
    [Fact]
    public void SelectedItem_WhenPropertyObserverSelectsNewerPage_SuppressesStaleSelectionEvent()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(TabControl.SelectedItem) && ReferenceEquals(tabs.SelectedItem, second))
            {
                tabs.SelectedItem = third;
            }
        };
        tabs.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        tabs.SelectedItem = second;

        tabs.SelectedItem.ShouldBeSameAs(third);
        changes.ShouldHaveSingleItem().CurrentItem.ShouldBeSameAs(third);
    }

    /// <summary>Verifies presentation callbacks may select another page immediately; the nested
    /// transaction presents its page before publishing and suppresses the interrupted event.</summary>
    [Fact]
    public void Visibility_WhenObserverSelectsNewerPage_PresentsAndPublishesOnlyNewerSelection()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        var observations = new List<(int Index, Visibility Second, Visibility Third)>();
        second.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility) && second.Visibility == Visibility.Visible)
            {
                tabs.SelectedIndex = 2;
            }
        };
        tabs.SelectionChanged += (_, eventArgs) =>
            observations.Add((eventArgs.CurrentIndex, second.Visibility, third.Visibility));

        tabs.SelectedIndex = 1;

        tabs.SelectedIndex.ShouldBe(2);
        observations.ShouldBe([(2, Visibility.Collapsed, Visibility.Visible)]);
    }

    /// <summary>Verifies structural mutation from a page-presentation callback cannot publish the
    /// obsolete numeric index captured before the selected page shifted in the collection.</summary>
    [Fact]
    public void Visibility_WhenObserverRemovesEarlierPage_SuppressesObsoleteSelectionEvent()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, second, third);
        var changes = new List<TabSelectionChangedEventArgs>();
        second.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility) && second.Visibility == Visibility.Visible)
            {
                _ = tabs.Items.Remove(first);
            }
        };
        tabs.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        tabs.SelectedIndex = 1;

        tabs.SelectedIndex.ShouldBe(0);
        tabs.SelectedItem.ShouldBeSameAs(second);
        second.Visibility.ShouldBe(Visibility.Visible);
        changes.ShouldBeEmpty();
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

    /// <summary>Verifies RequestClose rejects a null tab.</summary>
    [Fact]
    public void RequestClose_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var tabs = Create(Create("First", "One"));

        _ = Should.Throw<ArgumentNullException>(() => tabs.RequestClose(null!));
    }

    /// <summary>Verifies RequestClose rejects a tab owned by a different control, without raising
    /// CloseRequested or mutating either control's items.</summary>
    [Fact]
    public void RequestClose_WhenItemIsForeign_ThrowsArgumentException()
    {
        var tabs = Create(Create("First", "One"));
        var foreign = Create("Foreign", "Two");
        var other = Create(foreign);
        var raised = false;
        tabs.CloseRequested += (_, _) => raised = true;

        _ = Should.Throw<ArgumentException>(() => tabs.RequestClose(foreign));

        raised.ShouldBeFalse();
        other.Items.ShouldContain(foreign);
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

    /// <summary>Verifies HeaderOverflowPolicy rejects an undefined value before mutating state.</summary>
    [Fact]
    public void HeaderOverflowPolicy_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var tabs = new TabControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => tabs.HeaderOverflowPolicy = (TabHeaderOverflowPolicy) 99);

        tabs.HeaderOverflowPolicy.ShouldBe(TabHeaderOverflowPolicy.Clip);
    }

    /// <summary>Verifies a local Style overrides the code-owned strip glyphs, and clearing it
    /// returns ownership to the theme rather than to a second set of code-owned values.</summary>
    [Fact]
    public void Style_WhenAssigned_OverridesTheStripGlyphsAndClearingRestoresTheResolvedOnes()
    {
        // Arrange
        using var tabs = new TabControl();
        var defaultDivider = tabs.ActualStyle.DividerGlyph;
        var defaultUnderline = tabs.ActualStyle.UnderlineGlyph;

        // Act
        tabs.Style = TabControlStyle.Default with
        {
            DividerGlyph = new Rune('|'),
            UnderlineGlyph = new Rune('=')
        };

        // Assert custom
        tabs.ActualStyle.DividerGlyph.ShouldBe(new Rune('|'));
        tabs.ActualStyle.UnderlineGlyph.ShouldBe(new Rune('='));

        // Act reset - null returns to the resolved style, not to a separate code-owned copy, which
        // is the behavior the old ResetGlyphs() could not offer.
        tabs.Style = null;

        // Assert restored
        tabs.ActualStyle.DividerGlyph.ShouldBe(defaultDivider);
        tabs.ActualStyle.UnderlineGlyph.ShouldBe(defaultUnderline);
    }

    /// <summary>Verifies the divider glyph between headers survives on top of the header
    /// Stack's own opaque background instead of being overwritten every frame.</summary>
    [Fact]
    public void Render_WhenTwoHeadersArePresent_DrawsDividerOnTopOfHeaderBackground()
    {
        var tabs = Create(Create("A", "One"), Create("B", "Two"));
        tabs.HeaderWidth = Length.Cells(5);
        tabs.Style = TabControlStyle.Default with { DividerGlyph = new Rune('|') };
        var size = new Size(10, 4);
        new LayoutEngine().Layout(tabs, size);
        using Frame frame = new(size);

        tabs.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("|");
    }

    /// <summary>Verifies the divider still draws on the header row when the control is squeezed
    /// to exactly height 1, the graceful-degradation case ArrangeOverride explicitly clamps for.</summary>
    [Fact]
    public void Render_WhenHeightIsOne_StillDrawsDivider()
    {
        var tabs = Create(Create("A", "One"), Create("B", "Two"));
        tabs.HeaderWidth = Length.Cells(5);
        tabs.Style = TabControlStyle.Default with { DividerGlyph = new Rune('|') };
        var size = new Size(10, 1);
        new LayoutEngine().Layout(tabs, size);
        using Frame frame = new(size);

        tabs.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("|");
    }

    /// <summary>Verifies the selection underline, which needs a second row the height-1 case
    /// does not have, is not drawn beyond the header row.</summary>
    [Fact]
    public void Render_WhenHeightIsOne_DoesNotDrawUnderline()
    {
        var tabs = Create(Create("A", "One"), Create("B", "Two"));
        tabs.HeaderWidth = Length.Cells(5);
        tabs.Style = TabControlStyle.Default with { UnderlineGlyph = new Rune('=') };
        new LayoutEngine().Layout(tabs, new Size(10, 1));
        using Frame frame = new(new Size(10, 2));

        tabs.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies the style's colors reject transparent values, which the control's own
    /// properties used to guard.</summary>
    [Fact]
    public void StyleColors_WhenTransparentIsAssigned_Throw()
    {
        _ = Should.Throw<ArgumentException>(
            () => TabControlStyle.Default with { DividerColor = Color.Transparent });
        _ = Should.Throw<ArgumentException>(
            () => TabControlStyle.Default with { SelectionIndicatorColor = Color.Transparent });
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
            tabs.Style = TabControlStyle.Default with { DividerColor = Color.Rgb(0xff, 0, 0) });
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

    /// <summary>Verifies keys outside tab navigation remain available to inherited routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var tabs = new TabControl();
        var raised = 0;
        tabs.KeyDown += (_, _) => raised++;
        var eventArgs = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        _ = Router.Route(tabs, Events.Key, eventArgs);

        // Assert
        eventArgs.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies a routed handler can suppress the built-in tab-navigation default.</summary>
    [Fact]
    public void Dispatch_WhenKeyDownHandlesNavigation_PreservesSelection()
    {
        // Arrange
        var tabs = Create(Create("First", "One"), Create("Second", "Two"));
        tabs.KeyDown += (_, eventArgs) => eventArgs.IsHandled = true;

        // Act
        Key(tabs, Code.Right);

        // Assert
        tabs.SelectedIndex.ShouldBe(0);
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
        tabs.HeaderAt(0).Text.ShouldBe("Original");

        // Act
        item.HeaderText = "Updated";

        // Assert
        tabs.HeaderAt(0).Text.ShouldBe("Updated");
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
    public void Items_WhenPageBeforeSelectionIsRemoved_ShiftsSelectedIndexWithoutRaisingSelectionChanged()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var tabs = Create(first, selected);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        tabs.Items.Remove(first).ShouldBeTrue();

        tabs.SelectedIndex.ShouldBe(0);
        tabs.SelectedItem.ShouldBeSameAs(selected);
        tabs.Items[0].ShouldBeSameAs(selected);
        IsHeaderSelected(tabs, 0).ShouldBeTrue();
        changes.ShouldBeEmpty();
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

    /// <summary>Verifies an authored Hidden page is just as ineligible for selection as an authored
    /// Collapsed one - IsEligible gates on RequestedVisibility == IsVisible exactly, so Hidden (which
    /// otherwise keeps its measured slot everywhere else in the framework) still repairs a selected
    /// page away, matching TabControl's own single-page-visible internal contract rather than the
    /// leaf Hidden-keeps-its-slot rule.</summary>
    [Fact]
    public void Availability_WhenSelectedPageBecomesHidden_RepairsSelectionLikeCollapsed()
    {
        var first = Create("First", "One");
        var second = Create("Second", "Two");
        var tabs = Create(first, second);
        tabs.SelectedIndex = 1;

        second.Visibility = Visibility.Hidden;

        tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(first);
        IsHeaderSelected(tabs, 0).ShouldBeTrue();
        second.Visibility.ShouldBe(Visibility.Collapsed, "the internal presentation policy still owns the item's live Visibility while unselected");
    }

    /// <summary>Verifies keyboard Left/Right navigation skips a Collapsed page exactly like a
    /// disabled one, and still wraps around it.</summary>
    [Fact]
    public void Keyboard_WhenPageIsCollapsed_ArrowNavigationSkipsIt()
    {
        var first = Create("First", "One");
        var collapsed = Create("Collapsed", "Two");
        collapsed.Visibility = Visibility.Collapsed;
        var third = Create("Third", "Three");
        var tabs = Create(first, collapsed, third);

        Key(tabs, Code.Right);

        tabs.SelectedIndex.ShouldBe(2);

        Key(tabs, Code.Right);

        tabs.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies a Collapsed TabItem's mirrored header both loses its own strip space and
    /// suppresses the divider RenderOverlay would otherwise draw immediately after it - proving
    /// TabControl's own header/divider logic (not just the header Stack's spacing) honors the
    /// item's authored Visibility.
    ///
    /// <para>The target must be the currently *selected* page: an unselected page's TabItem is
    /// already forced internally Collapsed by ApplyPresentation's single-page-visible policy (see
    /// its own reentrancy guard), so authoring Collapsed on an already-unselected item is a
    /// same-value no-op that never raises PropertyChanged and never reaches
    /// OnItemPropertyChanged's header-mirroring write at all.</para></summary>
    [Fact]
    public void Header_WhenSelectedTabItemIsCollapsed_HeaderMirrorsVisibilityAndSuppressesDivider()
    {
        var first = Create("A", "One");
        var middle = Create("B", "Two");
        var third = Create("C", "Three");
        var tabs = Create(first, middle, third);
        tabs.SelectedIndex = 1;
        tabs.HeaderWidth = Length.Cells(3);
        tabs.Style = TabControlStyle.Default with { DividerGlyph = new Rune('|') };
        var size = new Size(12, 4);
        var engine = new LayoutEngine();
        engine.Layout(tabs, size);
        using Frame baselineFrame = new(size);
        tabs.Render(baselineFrame.Canvas);
        FrameOracle.Get(baselineFrame, new Point(3, 0)).ShouldBe("|");
        FrameOracle.Get(baselineFrame, new Point(7, 0)).ShouldBe("|");

        middle.Visibility = Visibility.Collapsed;
        engine.Layout(tabs, size);

        tabs.HeaderAt(1).Visibility.ShouldBe(Visibility.Collapsed);
        // Selection repaired away from the now-Collapsed page.
        tabs.Items[tabs.SelectedIndex].ShouldNotBeSameAs(middle);
        // Headers "A" and "C" are now adjacent with exactly one divider between them - not two -
        // because RenderOverlay's own divider loop skips a Collapsed header instead of drawing a
        // stray divider immediately to its right.
        using Frame frame = new(size);
        tabs.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("|");
        tabs.HeaderAt(2).Bounds.X.ShouldBe(4);
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("C");
    }

    /// <summary>Verifies a consumer's Visibility write, made from a PropertyChanged handler while
    /// ApplyPresentation's own write for that item is still in flight, is honored instead of being
    /// silently discarded and re-issued on every following pass - the reentrancy previously
    /// misattributed to the control itself by the _updatingPresentation guard.</summary>
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

        tabs.HeaderAt(0).Text.ShouldBe("Inserted");
        tabs.HeaderAt(1).Text.ShouldBe("First");
        tabs.HeaderAt(2).Text.ShouldBe("Second");
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
        tabs.HeaderAt(2).Text.ShouldBe("Selected");
    }

    /// <summary>Verifies moving a different page across the selected page's position shifts the
    /// numeric SelectedIndex to keep tracking the same selected item's identity, without firing
    /// SelectionChanged (the selected item itself never moved or changed identity).</summary>
    [Fact]
    public void Move_WhenDifferentPageMovesAcrossSelectedPage_ShiftsSelectedIndexWithoutFiringChanged()
    {
        var first = Create("First", "One");
        var selected = Create("Selected", "Two");
        var third = Create("Third", "Three");
        var tabs = Create(first, selected, third);
        tabs.SelectedIndex = 1;
        var changes = new List<TabSelectionChangedEventArgs>();
        tabs.SelectionChanged += (_, args) => changes.Add(args);

        // Moving "third" (index 2) to before "first" (index 0) shifts everything after it right
        // by one, so the still-selected "selected" page moves from index 1 to index 2.
        tabs.Items.Move(2, 0);

        tabs.Items.ShouldBe([third, first, selected]);
        tabs.SelectedIndex.ShouldBe(2);
        tabs.SelectedItem.ShouldBeSameAs(selected);
        changes.ShouldBeEmpty();
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
        HeaderText = header,
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
