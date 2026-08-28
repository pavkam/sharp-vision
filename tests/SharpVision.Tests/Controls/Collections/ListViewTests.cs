// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;


/// <summary>Verifies realized ListView ownership, selection, input, scrolling, and rendering.</summary>
public sealed class ListViewTests
{
    /// <summary>Verifies both realization inputs use the newest values committed from their owner
    /// notifications, keeping generated rows aligned with the public template and height.</summary>
    [Fact]
    public void RealizationProperties_WhenObserversCommitNewerValues_UseNewestTemplateAndHeight()
    {
        ItemTemplate outerTemplate = _ => new ControlText("Outer") { Height = Length.Star(1) };
        ItemTemplate nestedTemplate = _ => new ControlText("Nested") { Height = Length.Star(1) };
        var list = new UiListView();
        list.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiListView.ItemTemplate) &&
                ReferenceEquals(list.ItemTemplate, outerTemplate))
            {
                list.ItemTemplate = nestedTemplate;
            }

            if (eventArgs.PropertyName == nameof(UiListView.RowHeight) && list.RowHeight == 2)
            {
                list.RowHeight = 3;
            }
        };

        list.ItemTemplate = outerTemplate;
        list.RowHeight = 2;
        list.Items = ["value"];
        new LayoutEngine().Layout(list, new Size(20, 6));

        list.ItemTemplate.ShouldBeSameAs(nestedTemplate);
        list.RowHeight.ShouldBe(3);
        OwnedTree.FindAll<ControlText>(list).ShouldContain(text => text.Content == "Nested");
        OwnedTree.FindAll<ControlText>(list).ShouldNotContain(text => text.Content == "Outer");
        OwnedTree.Find<ListItem>(list).ShouldNotBeNull().Bounds.Height.ShouldBe(3);
    }
    /// <summary>Verifies a ListView starts as a quiet borderless collection surface without caller styling.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesQuietBackgroundDefaults()
    {
        // Arrange and act
        var control = new UiListView();

        // Assert
        control.ActualBorder.Sides.ShouldBe(BorderSide.None);
        control.Face.Background.ShouldBe(SemanticColor.Control);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes flip EffectiveIsEnabled and
    /// the derived focus eligibility it drives, and re-enabling restores both.</summary>
    [Fact]
    public void Enabled_WhenToggledDirectlyOrByAncestor_UpdatesEffectiveEnabledAndFocusEligibility()
    {
        var control = new UiListView { Items = ["A", "B"], IsEnabled = false };

        control.EffectiveIsEnabled.ShouldBeFalse();
        control.CanFocus.ShouldBeFalse();

        control.IsEnabled = true;

        control.EffectiveIsEnabled.ShouldBeTrue();
        control.CanFocus.ShouldBeTrue();

        var ancestor = new Overlay { Children = { control }, IsEnabled = false };

        control.IsEnabled.ShouldBeTrue();
        control.EffectiveIsEnabled.ShouldBeFalse();
        control.CanFocus.ShouldBeFalse();

        ancestor.IsEnabled = true;

        control.EffectiveIsEnabled.ShouldBeTrue();
        control.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies empty defaults and owned realization render every Unicode item.</summary>
    [Fact]
    public void Items_WhenAssigned_RealizesOwnedControlsAndExactCells()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = new object?[] { "One", "界", null }
        };
        new LayoutEngine().Layout(control, new Size(5, 3));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas);

        control.SelectionMode.ShouldBe(ListSelectionMode.Single);
        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItem.ShouldBeNull();
        control.Items.Count.ShouldBe(3);
        realized.Count.ShouldBe(3);
        realized.All(item => item.Parent is not null).ShouldBeTrue();
        realized.Select(item => item.Parent).Distinct().Count().ShouldBe(3);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("界");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("n");
    }

    /// <summary>Verifies ListView exposes the canonical overflow policy and its actual composed scrollbar.</summary>
    [Fact]
    public void ScrollBars_WhenConfigured_ForwardCommonPolicyToComposedViewport()
    {
        var control = Create("one", "two", "three", "four", "five", "six");
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        control.ScrollBars = ScrollBars.Vertical;
        control.ShowScrollBars = ShowScrollBars.Always;
        control.ScrollBarStyle = ScrollBarStyle.ThinLine;
        new LayoutEngine().Layout(control, new Size(6, 3));

        control.ScrollBars.ShouldBe(ScrollBars.Vertical);
        control.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        control.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        var rail = control.HitTest(new Point(5, 0)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.ActualStyle.ShouldBe(ScrollBarStyle.ThinLine);

        control.ShowScrollBars = ShowScrollBars.Never;
        new LayoutEngine().Layout(control, new Size(6, 3));

        control.HitTest(new Point(5, 0)).ShouldNotBeOfType<ScrollBar>();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ScrollBars = (ScrollBars) 99);
    }

    /// <summary>Verifies generated-rail local mechanics publish exact resolved-style notifications.</summary>
    [Fact]
    public void ScrollBarStyle_WhenOwnershipChanges_PublishesLocalAndActualNotifications()
    {
        var control = new UiListView();
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(UiListView.ScrollBarStyle) or nameof(UiListView.ActualScrollBarStyle))
            {
                notifications.Add(eventArgs.PropertyName);
            }
        };
        control.ScrollBarStyle = ScrollBarStyle.ThinLine;
        control.ScrollBarStyle = null;
        notifications.ShouldBe([
            nameof(UiListView.ScrollBarStyle),
            nameof(UiListView.ActualScrollBarStyle),
            nameof(UiListView.ScrollBarStyle),
            nameof(UiListView.ActualScrollBarStyle)
        ]);
        notifications.Clear();

        // The framework's code-owned fallback (used while no theme is attached) resolves against
        // ThemeCatalog.Dark, not ThemeCatalog.White (see StyleDefinitions.Control), so switching to a
        // genuinely different theme is expected to change the resolved ActualScrollBarStyle and
        // publish exactly the one notification a value change produces - matching
        // ActualScrollBarStyleThemeTests, which explicitly asserts this same divergence.
        control.SetTheme(ThemeCatalog.White);
        notifications.ShouldBe([nameof(UiListView.ActualScrollBarStyle)]);
    }

    /// <summary>Verifies ListView preserves a popup result already found by base registry traversal without searching twice.</summary>
    [Fact]
    public void HitTest_WhenRegistryFindsPopup_PreservesResultWithoutSecondTraversal()
    {
        PopupHitProbe? probe = null;
        var control = new UiListView
        {
            ItemTemplate = _ => probe = new PopupHitProbe(),
            Items = ["item"]
        };
        new LayoutEngine().Layout(control, new Size(4, 1));

        var hit = control.HitTest(default);

        _ = probe.ShouldNotBeNull();
        hit.ShouldBeSameAs(probe);
        probe.PopupHitTestCalls.ShouldBe(1);
    }

    /// <summary>Verifies unchanged overflow policy assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void ShowScrollBars_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var control = new UiListView();
        var notifications = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiListView.ShowScrollBars))
            {
                notifications++;
            }
        };

        control.ShowScrollBars = ShowScrollBars.WhenNeeded;
        control.ShowScrollBars = ShowScrollBars.Always;
        control.ShowScrollBars = ShowScrollBars.Always;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies ShowScrollBars rejects an undefined value forwarded to the composed viewport.</summary>
    [Fact]
    public void ShowScrollBars_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var control = new UiListView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ShowScrollBars = (ShowScrollBars) 99);

        control.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
    }

    /// <summary>Verifies unchanged LineSize assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void LineSize_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var control = new UiListView();
        var notifications = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiListView.LineSize))
            {
                notifications++;
            }
        };

        control.LineSize = 3;
        control.LineSize = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies unchanged PageOverlap assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void PageOverlap_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var control = new UiListView();
        var notifications = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiListView.PageOverlap))
            {
                notifications++;
            }
        };

        control.PageOverlap = 3;
        control.PageOverlap = 3;

        notifications.ShouldBe(1);
    }

    /// <summary>Verifies failed item or template replacement leaves the complete old tree untouched.</summary>
    [Fact]
    public void ItemTemplate_WhenCandidateIsInvalid_PreservesItemsTemplateAndOwnership()
    {
        List<ControlText> previous = [];
        ItemTemplate valid = item => Add(previous, new ControlText((string) item!));
        var control = new UiListView
        {
            ItemTemplate = valid,
            Items = new object?[] { "A", "B" }
        };
        var duplicate = new ControlText("bad");

        _ = Should.Throw<ArgumentNullException>(() => control.Items = null!);
        _ = Should.Throw<ArgumentNullException>(() => control.ItemTemplate = null!);
        _ = Should.Throw<ArgumentException>(() => control.ItemTemplate = _ => null!);
        _ = Should.Throw<ArgumentException>(() => control.ItemTemplate = _ => duplicate);

        control.ItemTemplate.ShouldBeSameAs(valid);
        control.Items.ShouldBe(new object?[] { "A", "B" });
        previous.All(item => item is { IsDisposed: false, Parent: not null }).ShouldBeTrue();
    }

    /// <summary>Verifies successful replacement disposes every detached realized wrapper and child.</summary>
    [Fact]
    public void Items_WhenReplaced_DisposesPreviousRealizationWithoutStateLeakage()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = new object?[] { "A", "B" }
        };
        ControlText[] previous = [.. realized];

        control.Items = new object?[] { "C" };

        previous.All(item => item is { IsDisposed: true, Parent: null }).ShouldBeTrue();
        control.Items.ShouldBe(new object?[] { "C" });
        _ = realized[^1].Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies an item reset preserves selected values and the active value when their indexes move.</summary>
    [Fact]
    public void Items_WhenResetReordersExistingItems_PreservesSelectedAndActiveItems()
    {
        var first = new object();
        var selected = new object();
        var active = new object();
        var control = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            Items = [first, selected, active]
        };
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);

        control.Items = [active, first, selected];

        control.SelectedItems.ShouldBe([active, selected]);
        control.SelectedIndex.ShouldBe(0);
        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBeSameAs(active);
    }

    /// <summary>Verifies equal duplicate occurrences map in their original occurrence order.</summary>
    [Fact]
    public void Items_WhenResetReordersEqualDuplicates_PreservesSelectedOccurrence()
    {
        var control = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            Items = ["A", "A", "B"]
        };
        _ = control.SetSelected(1, true);

        control.Items = ["A", "B", "A"];

        control.SelectedIndex.ShouldBe(2);
        control.SelectedItem.ShouldBe("A");
        control.ActiveIndex.ShouldBe(2);
    }

    /// <summary>Verifies an item reset removes stale selection and repairs the active index deterministically.</summary>
    [Fact]
    public void Items_WhenSelectedItemIsRemoved_ClearsSelectionAndKeepsValidActiveItem()
    {
        var selected = new object();
        var remaining = new object();
        var control = new UiListView
        {
            Items = [selected, remaining],
            SelectedIndex = 0
        };

        control.Items = [new object(), remaining];

        control.SelectedIndex.ShouldBe(-1);
        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldNotBeSameAs(selected);
    }

    /// <summary>Verifies complete item replacement publishes the same removed-selection delta in
    /// eager and fixed-row-height realization modes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Items_WhenReplacementRemovesSelection_PublishesDelta(bool virtualized)
    {
        // Arrange
        var selected = new object();
        var remaining = new object();
        var control = new UiListView
        {
            RowHeight = virtualized ? 1 : null,
            Items = [selected, remaining],
            SelectedIndex = 0
        };
        var changes = new List<ListSelectionChangedEventArgs>();
        control.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        // Act
        control.Items = [new object(), remaining];

        // Assert
        control.SelectedIndex.ShouldBe(-1);
        var change = changes.ShouldHaveSingleItem();
        change.AddedIndexes.ToArray().ShouldBeEmpty();
        change.RemovedIndexes.ToArray().ShouldBe([0]);
    }

    /// <summary>Verifies selected items remapped to another index publish identical deltas in eager
    /// and fixed-row-height realization modes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Items_WhenReplacementRemapsSelection_PublishesIndexDelta(bool virtualized)
    {
        // Arrange
        var selected = new object();
        var control = new UiListView
        {
            RowHeight = virtualized ? 1 : null,
            Items = [selected, new object()],
            SelectedIndex = 0
        };
        ListSelectionChangedEventArgs? change = null;
        control.SelectionChanged += (_, eventArgs) => change = eventArgs;

        // Act
        control.Items = [new object(), selected];

        // Assert
        control.SelectedIndex.ShouldBe(1);
        _ = change.ShouldNotBeNull();
        change.AddedIndexes.ToArray().ShouldBe([1]);
        change.RemovedIndexes.ToArray().ShouldBe([0]);
    }

    /// <summary>Verifies programmatic selection rejects an unavailable row without disturbing valid selection.</summary>
    [Fact]
    public void SetSelected_WhenItemIsDisabled_RejectsSelectionAndPreservesValidSelection()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B"]
        };
        realized[1].IsEnabled = false;
        control.SelectedIndex = 0;

        control.SetSelected(1, true).ShouldBeFalse();

        control.SelectedIndex.ShouldBe(0);
        control.SelectedItem.ShouldBe("A");
    }

    /// <summary>Verifies SetSelected validates its index the same way SelectedIndex and
    /// BringIntoView do, and leaves selection state untouched when it rejects.</summary>
    [Fact]
    public void SetSelected_WhenIndexIsOutOfRange_ThrowsArgumentOutOfRangeExceptionAndPreservesSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SetSelected(3, true));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SetSelected(-1, true));

        control.SelectedIndex.ShouldBe(1);
        control.SelectedItem.ShouldBe("B");
    }

    /// <summary>Verifies SetSelected(index, true) rejects selection in None mode the same way the
    /// SelectedIndex setter does, without mutating selection state.</summary>
    [Fact]
    public void SetSelected_WhenSelectingInNoneMode_ThrowsInvalidOperationExceptionAndPreservesSelection()
    {
        var control = Create("A", "B");
        control.SelectionMode = ListSelectionMode.None;

        _ = Should.Throw<InvalidOperationException>(() => control.SetSelected(0, true));

        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies deselecting is always legal in None mode since it never adds anything to
    /// an already-empty selection.</summary>
    [Fact]
    public void SetSelected_WhenDeselectingInNoneMode_DoesNotThrow()
    {
        var control = Create("A", "B");
        control.SelectionMode = ListSelectionMode.None;

        control.SetSelected(0, false).ShouldBeFalse();

        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies selecting an index that is already selected is a no-op that returns false
    /// and raises neither SelectionChanging nor SelectionChanged.</summary>
    [Fact]
    public void SetSelected_WhenIndexIsAlreadySelected_ReturnsFalseWithoutRaisingEvents()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;
        var changingRaised = false;
        var changedRaised = false;
        control.SelectionChanging += (_, _) => changingRaised = true;
        control.SelectionChanged += (_, _) => changedRaised = true;

        control.SetSelected(1, true).ShouldBeFalse();

        control.SelectedIndex.ShouldBe(1);
        changingRaised.ShouldBeFalse();
        changedRaised.ShouldBeFalse();
    }

    /// <summary>Verifies deselecting an index that is not currently selected is a no-op that
    /// returns false without disturbing the existing selection.</summary>
    [Fact]
    public void SetSelected_WhenIndexIsNotSelected_DeselectReturnsFalse()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.SetSelected(2, false).ShouldBeFalse();

        control.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies assigning a disabled SelectedIndex preserves the existing valid selection.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsDisabled_PreservesExistingSelection()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B"]
        };
        realized[1].IsEnabled = false;
        control.SelectedIndex = 0;

        control.SelectedIndex = 1;

        control.SelectedIndex.ShouldBe(0);
        control.SelectedItem.ShouldBe("A");
    }

    /// <summary>Verifies snapshot active fallback skips a disabled last row and chooses the last available row.</summary>
    [Fact]
    public void Items_WhenActiveFallbackIsDisabled_ChoosesLastAvailableRow()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var label = new ControlText((string) item!);

                if (Equals(item, "Disabled"))
                {
                    label.IsEnabled = false;
                }

                return Add(realized, label);
            },
            Items = ["A", "B", "C"],
            SelectedIndex = 2
        };

        control.Items = ["Available", "Disabled"];

        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBe("Available");
        realized[^1].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies the clamped starting index itself is returned immediately when available,
    /// without the outward bidirectional search skipping it or double-checking it via the
    /// higher-index branch (which requires distance greater than zero).</summary>
    [Fact]
    public void Items_WhenClampedStartingIndexIsAvailable_ReturnsItDirectly()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!),
            Items = ["A", "B", "C"],
            SelectedIndex = 1
        };

        control.Items = ["A", "B", "C"];

        control.ActiveIndex.ShouldBe(1);
        control.Items[control.ActiveIndex].ShouldBe("B");
    }

    /// <summary>Verifies Up at the first index re-resolves to itself through FindEligible's clamp
    /// rather than failing to move - without the clamp, FindLinear would see the raw start index
    /// -1 as already out of range and report no eligible index at all.</summary>
    [Fact]
    public void MoveCurrent_WhenUpAtFirstIndex_StaysClampedAtFirstIndex()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!),
            Items = ["A", "B", "C"],
            SelectedIndex = 0
        };

        var moved = control.MoveCurrent(Code.Up);

        moved.ShouldBeTrue();
        control.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies Down at the last index re-resolves to itself through FindEligible's
    /// clamp rather than failing to move - the raw start index equals Items.Count, which the
    /// clamp resolves back into range before FindLinear ever runs.</summary>
    [Fact]
    public void MoveCurrent_WhenDownAtLastIndex_StaysClampedAtLastIndex()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!),
            Items = ["A", "B", "C"],
            SelectedIndex = 2
        };

        var moved = control.MoveCurrent(Code.Down);

        moved.ShouldBeTrue();
        control.ActiveIndex.ShouldBe(2);
    }

    /// <summary>Verifies a PageDown whose raw StepPage target overshoots past the last item is
    /// clamped by FindEligible into range rather than reporting no eligible index - the
    /// out-of-range raw index StepPage hands FindEligible is exactly what FindLinear alone
    /// cannot resolve.</summary>
    [Fact]
    public void MoveCurrent_WhenPageDownOvershootsPastLastItem_LandsAtLastIndex()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!),
            Items = Enumerable.Range(0, 5).Select(value => (object?) $"Item {value}").ToArray(),
            RowHeight = 1
        };
        new LayoutEngine().Layout(control, new Size(10, 20));
        control.SelectedIndex = 0;

        var moved = control.MoveCurrent(Code.PageDown);

        moved.ShouldBeTrue();
        control.ActiveIndex.ShouldBe(4);
    }

    /// <summary>Verifies a PageUp whose raw StepPage target overshoots past the first item is
    /// clamped by FindEligible into range rather than reporting no eligible index.</summary>
    [Fact]
    public void MoveCurrent_WhenPageUpOvershootsPastFirstItem_LandsAtFirstIndex()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!),
            Items = Enumerable.Range(0, 5).Select(value => (object?) $"Item {value}").ToArray(),
            RowHeight = 1
        };
        new LayoutEngine().Layout(control, new Size(10, 20));
        control.SelectedIndex = 4;

        var moved = control.MoveCurrent(Code.PageUp);

        moved.ShouldBeTrue();
        control.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies the RowHeight fast path's PageDown distance matches what eager mode's
    /// PagingStep.Accumulate produces for the same viewport/row-height combination: RowHeight=3
    /// against a Viewport.Height of 10 accumulates 3, 6, 9, 12 and stops at 12 (the fourth row),
    /// so PageDown must advance by 4 rows rather than floor(10/3) = 3.</summary>
    [Fact]
    public void MoveCurrent_WhenPageDownWithFixedRowHeightLeavesRemainder_RoundsUpLikeEagerAccumulate()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText((string) item!) { Height = Length.Cells(3) },
            Items = Enumerable.Range(0, 10).Select(value => (object?) $"Item {value}").ToArray(),
            RowHeight = 3
        };
        new LayoutEngine().Layout(control, new Size(10, 10));
        control.SelectedIndex = 0;

        var moved = control.MoveCurrent(Code.PageDown);

        moved.ShouldBeTrue();
        control.ActiveIndex.ShouldBe(4);
    }

    /// <summary>Verifies active fallback chooses a closer available higher row over a farther lower row.</summary>
    [Fact]
    public void Items_WhenHigherAvailableRowIsCloser_ChoosesHigherRow()
    {
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var label = new ControlText((string) item!);

                if (Equals(item, "Disabled"))
                {
                    label.IsEnabled = false;
                }

                return label;
            },
            Items = ["A", "B", "C"],
            SelectedIndex = 2
        };

        control.Items = ["Low", "Disabled", "Disabled", "High"];

        control.ActiveIndex.ShouldBe(3);
        control.Items[control.ActiveIndex].ShouldBe("High");
    }

    /// <summary>Verifies each realized row's private ListItem.IsSelected mirrors the owning
    /// ListView's committed selection, toggling as SelectedIndex moves between rows.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_TogglesTheRealizedRowsOwnIsSelected()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = ["A", "B", "C"]
        };

        var first = (ListItem) realized[0].Parent!;
        var second = (ListItem) realized[1].Parent!;

        control.SelectedIndex = 0;

        first.IsSelected.ShouldBeTrue();
        second.IsSelected.ShouldBeFalse();

        control.SelectedIndex = 1;

        first.IsSelected.ShouldBeFalse();
        second.IsSelected.ShouldBeTrue();
    }

    /// <summary>Verifies none, single, and multiple modes normalize selection deterministically.</summary>
    [Fact]
    public void SetSelected_WhenModesDiffer_EnforcesModeAndIndexContracts()
    {
        var control = Create("A", "B", "C");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SelectedIndex = 3);
        control.SelectedIndex = 1;
        control.SelectedItem.ShouldBe("B");
        control.SelectedItems.ShouldBe(new object?[] { "B" });

        control.SelectionMode = ListSelectionMode.Multiple;
        control.SetSelected(2, true).ShouldBeTrue();
        control.SelectedItems.ShouldBe(new object?[] { "B", "C" });
        control.SelectionMode = ListSelectionMode.Single;
        control.SelectedItems.ShouldBe(new object?[] { "B" });
        control.SelectionMode = ListSelectionMode.None;
        control.SelectedIndex.ShouldBe(-1);
        _ = Should.Throw<InvalidOperationException>(() => control.SelectedIndex = 0);
    }

    /// <summary>Verifies cancellable selection precedes one committed added/removed notification.</summary>
    [Fact]
    public void SelectedIndex_WhenChangingIsCancelled_PreservesStateAndStableSelectedView()
    {
        var control = Create("A", "B", "C");
        var view = control.SelectedItems;
        List<string> order = [];
        control.SelectionChanging += (_, eventArgs) =>
        {
            order.Add($"changing:{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
            eventArgs.Cancel = eventArgs.AddedIndexes.Span.Contains(2);
        };
        control.SelectionChanged += (_, eventArgs) =>
            order.Add($"changed:{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");

        control.SelectedIndex = 1;
        control.SelectedIndex = 2;

        control.SelectedIndex.ShouldBe(1);
        control.ActiveIndex.ShouldBe(1);
        control.SelectedItems.ShouldBeSameAs(view);
        view.ShouldBe(new object?[] { "B" });
        order.ShouldBe([
            "changing:1:",
            "changed:1:",
            "changing:2:1"
        ]);
    }

    /// <summary>Verifies a SelectionChanging subscriber that reentrantly commits another selection
    /// change from inside its own handler does not let the now-superseded outer proposal reach a
    /// second subscriber registered after it on the same event - the second subscriber must only
    /// ever observe the reentrant transition that actually won.</summary>
    [Fact]
    public void SelectedIndex_WhenChangingSubscriberReentrantlyChangesSelection_LaterSubscriberNeverSeesSupersededProposal()
    {
        var control = Create("A", "B", "C");
        List<string> secondSubscriberObservations = [];

        control.SelectionChanging += (_, eventArgs) =>
        {
            if (eventArgs.AddedIndexes.Span.Contains(1))
            {
                control.SelectedIndex = 2;
            }
        };
        control.SelectionChanging += (_, eventArgs) =>
            secondSubscriberObservations.Add(Join(eventArgs.AddedIndexes));

        control.SelectedIndex = 1;

        control.SelectedIndex.ShouldBe(2);
        secondSubscriberObservations.ShouldBe(["2"]);
    }

    /// <summary>Verifies a first subscriber that reenters through item removal - rather than through
    /// another selection assignment - still stops a later subscriber from observing the now-obsolete
    /// outer proposal, proving the version-checked delivery in <c>SelectionCommit&lt;TKey&gt;</c>
    /// generalizes to any reentrant selection-version bump, not only a reentrant selection call.</summary>
    [Fact]
    public void SelectedIndex_WhenChangingSubscriberReentrantlyRemovesAnItem_LaterSubscriberNeverSeesSupersededProposal()
    {
        var control = Create("A", "B", "C");
        List<string> secondSubscriberObservations = [];

        control.SelectionChanging += (_, eventArgs) =>
        {
            if (eventArgs.AddedIndexes.Span.Contains(1))
            {
                control.Items = ["A", "C"];
            }
        };
        control.SelectionChanging += (_, eventArgs) =>
            secondSubscriberObservations.Add(Join(eventArgs.AddedIndexes));

        control.SelectedIndex = 1;

        secondSubscriberObservations.ShouldBeEmpty();
    }

    /// <summary>Verifies arrows skip unavailable items, Space selects, Enter invokes, and Home/End navigate.</summary>
    [Fact]
    public async Task Dispatch_WhenKeyboardNavigates_UsesStableRealizedOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = new object?[] { "A", "B", "C" }
        };
        realized[1].IsEnabled = false;
        List<int> invoked = [];
        control.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Down);
            focus.Focused.ShouldBeSameAs(control);
            control.ActiveIndex.ShouldBe(2);
            control.SelectedIndex.ShouldBe(2);
            Space(control);
            control.SelectedIndex.ShouldBe(2);
            Key(control, Code.Enter);
            Key(control, Code.Home);
            control.ActiveIndex.ShouldBe(0);
            control.SelectedIndex.ShouldBe(0);
            Key(control, Code.End);
            control.ActiveIndex.ShouldBe(2);
            control.SelectedIndex.ShouldBe(2);
        }, TestContext.Current.CancellationToken);

        invoked.ShouldBe([2]);
    }

    /// <summary>Verifies pointer invocation stops when selection callbacks replace the exact
    /// realized row, even if another item occupies its former index.</summary>
    [Theory]
    [InlineData("Clear")]
    [InlineData("Replace")]
    [InlineData("InsertBefore")]
    public async Task Dispatch_WhenSelectionChangingReplacesActivatedRow_AbandonsInvocationAsync(
        string mutation)
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var control = new UiListView { Items = new object?[] { "A", "B" } };
        var invocations = new List<ItemInvokedEventArgs>();
        control.ItemInvoked += (_, eventArgs) => invocations.Add(eventArgs);
        control.SelectionChanging += (_, _) => control.Items = mutation switch
        {
            "Clear" => [],
            "Replace" => ["replacement", "other"],
            "InsertBefore" => ["inserted", "A", "B"],
            _ => throw new UnreachableException()
        };

        // Act and assert
        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            new LayoutEngine().Layout(control, new Size(10, 4));
            using PointerManager capture = new(control);
            Should.NotThrow(() => Click(capture, new Point(0, 1), Modifiers.None));
        }, TestContext.Current.CancellationToken);
        invocations.ShouldBeEmpty();
    }

    /// <summary>Verifies modifier families unavailable in legacy mouse encoding still suppress direct pointer invocation.</summary>
    [Theory]
    [InlineData(Modifiers.Super)]
    [InlineData(Modifiers.Meta)]
    [InlineData(Modifiers.Hyper)]
    public async Task Dispatch_WhenDoubleClickCarriesExtendedCommandModifier_DoesNotInvokeAsync(Modifiers modifiers)
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new UiListView
        {
            Items = ["A"],
            ItemInvocation = ListItemInvocation.DoubleClick
        };
        var invocations = 0;
        control.ItemInvoked += (_, _) => invocations++;

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            new LayoutEngine().Layout(control, new Size(10, 2));
            using PointerManager pointer = new(control);
            Click(pointer, new Point(0, 0), modifiers);
            Click(pointer, new Point(0, 0), modifiers);
        }, TestContext.Current.CancellationToken);

        invocations.ShouldBe(0);
    }

    /// <summary>Verifies selector-owned keyboard activation cannot overwrite the immutable
    /// pointer metadata captured for a held row's later double-click completion.</summary>
    [Fact]
    public async Task Dispatch_WhenKeyboardActivationOccursDuringSecondPointerPress_PreservesPointerMetadataAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new UiListView
        {
            Items = ["A"],
            ItemInvocation = ListItemInvocation.DoubleClick
        };
        var invocations = new List<ActivationCause>();
        control.ItemInvoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            new LayoutEngine().Layout(control, new Size(10, 2));
            using PointerManager pointer = new(control);
            Click(pointer, new Point(0, 0), Modifiers.None);
            _ = pointer.Dispatch(Pointer(new Point(0, 0), PointerAction.Press, Modifiers.None));

            _ = KeyWithModifiers(control, Code.Enter, Modifiers.Control);
            _ = pointer.Dispatch(Pointer(new Point(0, 0), PointerAction.Release, Modifiers.None));
        }, TestContext.Current.CancellationToken);

        invocations.ShouldBe([ActivationCause.Pointer]);
    }

    /// <summary>Verifies an incidental Control modifier on Enter still moves the active item but
    /// does not raise ItemInvoked - only the invocation is gated, not selection tracking.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifier_MovesActiveItemButDoesNotInvokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = Create("A", "B", "C");
        List<int> invoked = [];
        control.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Down);

            var press = KeyWithModifiers(control, Code.Enter, Modifiers.Control);

            press.IsHandled.ShouldBeTrue();
            control.ActiveIndex.ShouldBe(1);
        }, TestContext.Current.CancellationToken);

        invoked.ShouldBeEmpty();
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still invokes.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifier_StillInvokesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = Create("A", "B", "C");
        List<int> invoked = [];
        control.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(control).ShouldBeTrue();

            _ = KeyWithModifiers(control, Code.Enter, Modifiers.Shift);
        }, TestContext.Current.CancellationToken);

        invoked.ShouldBe([0]);
    }

    /// <summary>Verifies Space preserves Control/Shift selection gestures while rejecting
    /// modifiers reserved for application commands.</summary>
    [Theory]
    [InlineData(ListSelectionMode.Single, Modifiers.None, true)]
    [InlineData(ListSelectionMode.Single, Modifiers.Control, true)]
    [InlineData(ListSelectionMode.Single, Modifiers.Shift, true)]
    [InlineData(ListSelectionMode.Single, Modifiers.Alt, false)]
    [InlineData(ListSelectionMode.Single, Modifiers.Super, false)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.None, true)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Control, true)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Shift, true)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Control | Modifiers.Shift, true)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.CapsLock | Modifiers.NumLock, true)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Alt, false)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Super, false)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Hyper, false)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Meta, false)]
    [InlineData(ListSelectionMode.Multiple, Modifiers.Control | Modifiers.Alt, false)]
    public void Dispatch_WhenSpaceCarriesModifiers_SelectsOnlyForCollectionGesture(
        ListSelectionMode selectionMode,
        Modifiers modifiers,
        bool expectedSelection)
    {
        // Arrange
        var control = new UiListView
        {
            SelectionMode = selectionMode,
            Items = new object?[] { "A", "B" }
        };
        var key = CharacterKeyWithModifiers(control, new Rune(' '), modifiers);

        // Assert
        control.SelectedItems.ShouldBe(expectedSelection ? new object?[] { "A" } : []);
        key.IsHandled.ShouldBe(expectedSelection);
    }

    /// <summary>Verifies pointer modifiers toggle and range-select in multiple mode.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerUsesModifiers_AppliesToggleAndRangeSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = Create("A", "B", "C", "D");
        control.Bounds = new Rect(0, 0, 4, 4);
        control.SelectionMode = ListSelectionMode.Multiple;
        new LayoutEngine().Layout(control, new Size(4, 4));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using PointerManager capture = new(control);
            Click(capture, new Point(0, 1), Modifiers.Control);
            Click(capture, new Point(0, 3), Modifiers.Shift);
        }, TestContext.Current.CancellationToken);

        control.SelectedItems.ShouldBe(new object?[] { "B", "C", "D" });
    }

    /// <summary>Verifies active items are minimally brought through the armed item Stack on resize.</summary>
    [Fact]
    public async Task Dispatch_WhenActiveItemMovesBeyondViewport_BringsItIntoViewAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(8, 3));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(control).ShouldBeTrue();

            for (var index = 0; index < 7; index++)
            {
                Key(control, Code.Down);
            }

            control.VerticalOffset.ShouldBeGreaterThan(0);
            control.ActiveIndex.ShouldBe(7);
            control.SelectedIndex.ShouldBe(7);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct selection uses the same active-row and minimal-visibility
    /// transaction as keyboard navigation.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsBeyondViewport_SynchronizesActiveRowAndBringsItIntoView()
    {
        // Arrange
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(8, 3));

        // Act
        control.SelectedIndex = 5;

        // Assert
        control.SelectedIndex.ShouldBe(5);
        control.ActiveIndex.ShouldBe(5);
        control.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies selection assigned before the first layout is revealed once the viewport
    /// exists instead of losing the visibility request against zero-sized geometry.</summary>
    [Fact]
    public void SelectedIndex_WhenAssignedBeforeLayout_RevealsTargetAfterViewportCommits()
    {
        // Arrange
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectedIndex = 5
        };

        // Act
        new LayoutEngine().Layout(control, new Size(8, 3));

        // Assert
        control.SelectedIndex.ShouldBe(5);
        control.ActiveIndex.ShouldBe(5);
        control.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies the additive selection method uses the same active-row visibility
    /// transaction as the exclusive SelectedIndex property.</summary>
    [Fact]
    public void SetSelected_WhenTargetIsBeyondViewport_SynchronizesActiveRowAndBringsItIntoView()
    {
        // Arrange
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectionMode = ListSelectionMode.Multiple
        };
        new LayoutEngine().Layout(control, new Size(8, 3));

        // Act
        control.SetSelected(5, selected: true).ShouldBeTrue();

        // Assert
        control.SelectedIndex.ShouldBe(5);
        control.ActiveIndex.ShouldBe(5);
        control.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a cancelled exclusive assignment to an already-selected member does not
    /// move the active row or viewport independently of the rejected selection transaction.</summary>
    [Fact]
    public void SelectedIndex_WhenExclusiveAssignmentIsCancelled_PreservesActiveRowAndViewport()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectionMode = ListSelectionMode.Multiple
        };
        new LayoutEngine().Layout(control, new Size(8, 3));
        _ = control.SetSelected(0, selected: true);
        _ = control.SetSelected(5, selected: true);
        control.SelectionChanging += (_, eventArgs) => eventArgs.Cancel = true;

        control.SelectedIndex = 0;

        control.SelectedItems.ShouldBe(new object?[] { "Item 0", "Item 5" });
        control.ActiveIndex.ShouldBe(5);
        control.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a reentrant selection change wins the complete active-row and visibility
    /// transaction instead of letting the outer assignment reveal its now-unselected target.</summary>
    [Fact]
    public void SelectedIndex_WhenSelectionChangedReenters_PreservesTheFinalSelectionTarget()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(8, 3));
        control.SelectionChanged += (_, _) =>
        {
            if (control.SelectedIndex != 6)
            {
                control.SelectedIndex = 6;
            }
        };

        control.SelectedIndex = 5;

        control.SelectedIndex.ShouldBe(6);
        control.ActiveIndex.ShouldBe(6);
        control.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies selection property reentry suppresses the superseded typed delta.</summary>
    [Theory]
    [InlineData(nameof(UiListView.SelectedIndex))]
    [InlineData(nameof(UiListView.SelectedItem))]
    [InlineData(nameof(UiListView.SelectedItems))]
    public void SelectedIndex_WhenSelectionPropertyObserverReenters_PublishesOnlyCurrentTypedEvent(
        string propertyName)
    {
        var control = new UiListView { Items = ["Zero", "One", "Two"] };
        var events = new List<ListSelectionChangedEventArgs>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == propertyName && control.SelectedIndex == 1)
            {
                control.SelectedIndex = 2;
            }
        };
        control.SelectionChanged += (_, eventArgs) => events.Add(eventArgs);

        control.SelectedIndex = 1;

        control.SelectedIndex.ShouldBe(2);
        events.Count.ShouldBe(1);
        events[0].AddedIndexes.ToArray().ShouldBe([2]);
    }

    /// <summary>Verifies additive selection also refuses to activate a stale target removed by a
    /// reentrant exclusive selection transaction.</summary>
    [Fact]
    public void SetSelected_WhenSelectionChangedReenters_PreservesTheFinalSelectionTarget()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectionMode = ListSelectionMode.Multiple
        };
        new LayoutEngine().Layout(control, new Size(8, 3));
        control.SelectionChanged += (_, _) =>
        {
            if (control.SelectedIndex != 6)
            {
                control.SelectedIndex = 6;
            }
        };

        _ = control.SetSelected(5, selected: true);

        control.SelectedIndex.ShouldBe(6);
        control.ActiveIndex.ShouldBe(6);
        control.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies an outer clear cannot discard the deferred reveal established by a
    /// reentrant pre-layout selection.</summary>
    [Fact]
    public void SelectedIndex_WhenClearReentersBeforeLayout_RevealsTheFinalSelectionTarget()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectedIndex = 5
        };
        control.SelectionChanged += (_, _) =>
        {
            if (control.SelectedIndex < 0)
            {
                control.SelectedIndex = 6;
            }
        };

        control.SelectedIndex = -1;
        new LayoutEngine().Layout(control, new Size(8, 3));

        control.SelectedIndex.ShouldBe(6);
        control.ActiveIndex.ShouldBe(6);
        control.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies an outer additive selection cannot override the active target committed
    /// by a reentrant additive selection merely because both targets remain selected.</summary>
    [Fact]
    public void SetSelected_WhenAdditiveSelectionReenters_PreservesTheReentrantActiveTarget()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectionMode = ListSelectionMode.Multiple
        };
        new LayoutEngine().Layout(control, new Size(8, 3));
        control.SelectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.AddedIndexes.Span.Contains(5) && !control.SelectedItems.Contains("Item 6"))
            {
                _ = control.SetSelected(6, selected: true);
            }
        };

        _ = control.SetSelected(5, selected: true);

        control.SelectedItems.ShouldBe(new object?[] { "Item 5", "Item 6" });
        control.ActiveIndex.ShouldBe(6);
        control.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies a reentrant Items replacement triggered from the outer replacement's own
    /// SelectionChanged notification wins the active row - the outer replacement's now-stale
    /// continuation must be skipped rather than resuming with an active index it computed against
    /// data the reentrant call already superseded.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Items_WhenSelectionChangedReentersDuringReplace_PreservesReentrantActiveRow(bool virtualized)
    {
        var control = new UiListView
        {
            RowHeight = virtualized ? 1 : null,
            Items = ["A", "B", "C"],
            SelectedIndex = 1
        };
        var reentered = false;
        control.SelectionChanged += (_, _) =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            control.Items = ["X", "Y", "Z", "W"];
            control.SelectedIndex = 3;
        };

        control.Items = ["P", "Q"];

        control.Items.ShouldBe(new object?[] { "X", "Y", "Z", "W" });
        control.SelectedIndex.ShouldBe(3);
        control.ActiveIndex.ShouldBe(3);
    }

    /// <summary>Verifies an explicit repeated directional key is the same repeatable navigation
    /// command as an initial key down.</summary>
    [Fact]
    public void Dispatch_WhenDirectionalKeyRepeats_ContinuesNavigation()
    {
        // Arrange
        var control = Create("A", "B", "C");
        control.SelectedIndex = 0;

        // Act
        Key(control, Code.Down, action: KeyAction.Repeat);

        // Assert
        control.SelectedIndex.ShouldBe(1);
        control.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies SelectedItem setter selects the matching item by value.</summary>
    [Fact]
    public void SelectedItem_WhenSetToValue_SelectsMatchingIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedItem = "B";
        control.SelectedIndex.ShouldBe(1);
        control.SelectedItem.ShouldBe("B");
    }

    /// <summary>Verifies SelectedItem setter clears selection when set to null.</summary>
    [Fact]
    public void SelectedItem_WhenSetToNull_ClearsSelection()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 0;
        control.SelectedItem = null;
        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies SelectedItem setter clears selection when value is not found.</summary>
    [Fact]
    public void SelectedItem_WhenValueNotFound_ClearsSelection()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 0;
        control.SelectedItem = "Z";
        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItem.ShouldBeNull();
    }

    private static UiListView Create(params object?[] items) => new() { Items = items };

    private static ControlText Add(List<ControlText> controls, ControlText control)
    {
        controls.Add(control);
        return control;
    }

    private static string Join(ReadOnlyMemory<int> values) => string.Join(',', values.ToArray());

    private static void Key(
        ControlBase target,
        Code code,
        Rune? character = null,
        KeyAction action = KeyAction.Press) =>
        _ = Router.Route(
            target,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character,
                nativeCode: 0,
                Modifiers.None,
                action)));

    private static KeyEventArgs KeyWithModifiers(ControlBase target, Code code, Modifiers modifiers)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press));
        _ = Router.Route(target, Events.Key, eventArgs);
        return eventArgs;
    }

    private static KeyEventArgs CharacterKeyWithModifiers(
        ControlBase target,
        Rune character,
        Modifiers modifiers)
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

    private static void Space(ControlBase target)
    {
        Key(target, Code.Character, new Rune(' '));
        _ = Router.Route(
            target,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(' '),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Release)));
    }

    /// <summary>Verifies the composed scroll container's contract is reachable directly on
    /// ListView, without a caller needing to know about the private items Stack.</summary>
    [Fact]
    public void ScrollBy_WhenContentExceedsViewport_MovesVerticalOffsetAndRaisesScrollChanged()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));
        List<ScrollChangedEventArgs> changes = [];
        control.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        control.Extent.Height.ShouldBeGreaterThan(control.Viewport.Height);
        var moved = control.ScrollBy(0, 3);

        moved.ShouldBeTrue();
        control.VerticalOffset.ShouldBe(3);
        var change = changes.ShouldHaveSingleItem();
        change.PreviousOffset.ShouldBe(new Point(0, 0));
        change.Offset.ShouldBe(new Point(0, 3));
        change.Cause.ShouldBe(ScrollCause.Programmatic);
    }

    /// <summary>Verifies ScrollBy reports no movement, and raises no ScrollChanged, once the
    /// viewport is already saturated at the requested end - a strict distinct case from an
    /// unsaturated move, since a caller relies on the return value to know whether the offset
    /// actually changed.</summary>
    [Fact]
    public void ScrollBy_WhenAlreadyAtSaturatedEndpoint_ReturnsFalseWithoutRaisingScrollChanged()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));
        var changes = 0;
        control.ScrollChanged += (_, _) => changes++;

        var moved = control.ScrollBy(0, -1);

        moved.ShouldBeFalse();
        control.VerticalOffset.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies ScrollBy propagates the composed viewport's own cause validation, rather
    /// than silently accepting an undefined <see cref="ScrollCause"/>.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ScrollBy(0, 1, (ScrollCause) 99));
    }

    /// <summary>Verifies VerticalOffset and HorizontalOffset default to zero and round-trip a
    /// directly assigned in-range value, without requiring a caller to go through ScrollBy.</summary>
    [Fact]
    public void VerticalAndHorizontalOffset_WhenAssignedDirectly_DefaultToZeroAndRoundTrip()
    {
        var control = new UiListView
        {
            ScrollBars = ScrollBars.Both,
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value} 0123456789").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));

        control.VerticalOffset.ShouldBe(0);
        control.HorizontalOffset.ShouldBe(0);

        control.VerticalOffset = 5;
        control.HorizontalOffset = 2;

        control.VerticalOffset.ShouldBe(5);
        control.HorizontalOffset.ShouldBe(2);
    }

    /// <summary>Verifies VerticalOffset and HorizontalOffset each reject a value outside the
    /// composed viewport's current extent, and leave the previously committed offset unchanged.</summary>
    [Fact]
    public void VerticalAndHorizontalOffset_WhenOutsideExtent_ThrowsArgumentOutOfRangeExceptionAndPreservesOffset()
    {
        var control = new UiListView
        {
            ScrollBars = ScrollBars.Both,
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value} 0123456789").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));
        control.VerticalOffset = 3;
        control.HorizontalOffset = 1;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.VerticalOffset = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.VerticalOffset = 9999);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.HorizontalOffset = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.HorizontalOffset = 9999);

        control.VerticalOffset.ShouldBe(3);
        control.HorizontalOffset.ShouldBe(1);
    }

    /// <summary>Verifies RowHeight defaults to null and that leaving it unset produces byte-identical
    /// eager realization, rendering, and disposal - the one guarantee the windowed-realization
    /// scaffolding must never break, since ComboBox and the file-picker dialogs embed a ListView
    /// on it.</summary>
    [Fact]
    public void RowHeight_WhenUnset_KeepsEagerRealizationByteIdentical()
    {
        List<ControlText> realizedWithoutRowHeight = [];
        var baseline = new UiListView
        {
            ItemTemplate = item => Add(realizedWithoutRowHeight, new ControlText(item?.ToString() ?? "null")),
            Items = Enumerable.Range(0, 5).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(baseline, new Size(10, 3));
        using Frame baselineFrame = new(new Size(10, 3));
        baseline.Render(baselineFrame.Canvas);

        List<ControlText> realizedWithNullRowHeight = [];
        var control = new UiListView
        {
            RowHeight = null,
            ItemTemplate = item => Add(realizedWithNullRowHeight, new ControlText(item?.ToString() ?? "null")),
            Items = Enumerable.Range(0, 5).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);

        control.RowHeight.ShouldBeNull();
        realizedWithNullRowHeight.Count.ShouldBe(realizedWithoutRowHeight.Count);
        realizedWithNullRowHeight.Count.ShouldBe(5);
        realizedWithNullRowHeight.All(item => item.Parent is not null).ShouldBeTrue();

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                var point = new Point(x, y);
                FrameOracle.Get(frame, point).ShouldBe(FrameOracle.Get(baselineFrame, point));
            }
        }
    }

    /// <summary>Verifies RowHeight can be assigned and read back, and that assigning it engages
    /// windowed realization - only the viewport-bounded window plus overscan is ever realized,
    /// not the full collection.</summary>
    [Fact]
    public void RowHeight_WhenAssigned_RealizesOnlyTheViewportWindow()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 1,
            // Star tracks whatever RowHeight is current at measure time (see the reassignment
            // below), the same way a real fixed-row template is expected to.
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null") { Height = Length.Star(1) }),
            Items = Enumerable.Range(0, 500).Select(value => (object?) $"Item {value}").ToArray()
        };

        control.RowHeight.ShouldBe(1);
        new LayoutEngine().Layout(control, new Size(10, 3));
        using Frame frame = new(new Size(10, 3));
        control.Render(frame.Canvas);

        realized.Count.ShouldBeLessThan(500);
        realized.All(item => item.Parent is not null).ShouldBeTrue();

        control.RowHeight = 4;
        control.RowHeight.ShouldBe(4);
    }

    /// <summary>Verifies first realization repairs a previously selected virtual row whose
    /// template is already unavailable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedIndex_WhenUnrealizedTemplateIsInitiallyUnavailable_RepairsSelection(bool collapsed)
    {
        const int target = 30;
        var control = new UiListView
        {
            RowHeight = 1,
            Items = Enumerable.Range(0, 40).Cast<object?>().ToArray(),
            ItemTemplate = item => new ControlText(item?.ToString() ?? string.Empty)
            {
                IsEnabled = collapsed || (int) item! != target,
                Visibility = collapsed && (int) item! == target ? Visibility.Collapsed : Visibility.Visible
            }
        };
        new LayoutEngine().Layout(control, new Size(10, 5));

        control.SelectedIndex = target;

        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItems.ShouldBeEmpty();
        control.ActiveIndex.ShouldNotBe(target);
    }

    /// <summary>Verifies direct template disposal removes its semantic item before layout can observe an empty wrapper.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItemTemplate_WhenOwnedContentIsDisposed_RemovesSemanticItem(bool virtualized)
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = virtualized ? 1 : null,
            Items = ["first", "second"],
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? string.Empty))
        };
        new LayoutEngine().Layout(control, new Size(10, 2));
        control.SelectedIndex = 0;

        realized[0].Dispose();
        new LayoutEngine().Layout(control, new Size(10, 2));

        control.Items.ShouldBe(["second"]);
        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies a horizontally scrolled, fixed-RowHeight virtualized ListView renders its
    /// realized rows at the offset-shifted screen position - regression for Rewindow computing
    /// each row's X from the un-shifted RowOrigin.X alone, unlike its own Y arithmetic, which
    /// already subtracts VerticalOffset the same way.</summary>
    [Fact]
    public void Rewindow_WhenHorizontallyScrolled_RendersRealizedRowsAtTheShiftedX()
    {
        const string text = "0123456789ABCDEFGHIJ";
        var control = new UiListView
        {
            RowHeight = 1,
            ScrollBars = ScrollBars.Both,
            ItemTemplate = _ => new ControlText(text),
            Items = Enumerable.Range(0, 30).Select(value => (object?) value).ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 5));

        control.HorizontalOffset = 3;

        using Frame frame = new(new Size(10, 5));
        control.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("3");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("6");
    }

    /// <summary>Verifies a horizontally scrolled, fixed-RowHeight virtualized ListView still renders
    /// real row content across the whole viewport once HorizontalOffset reaches or exceeds the
    /// viewport's content width - regression for Rewindow shifting each row's arrange-X left by
    /// HorizontalOffset without ever widening the row to match, which left the row's arranged rect
    /// entirely outside the static viewport clip (and the whole row blank) as soon as the offset
    /// caught up with the viewport width. The viewport content width here is 9 (10 columns minus the
    /// 1-column vertical scrollbar reserved by ScrollBars.Both against 30 items and 20-character
    /// rows), so HorizontalOffset = 9 is exactly the smallest offset that used to blank the row.</summary>
    [Fact]
    public void Rewindow_WhenHorizontalOffsetReachesViewportWidth_StillRendersRealizedRowContent()
    {
        const string text = "0123456789ABCDEFGHIJ";
        var control = new UiListView
        {
            RowHeight = 1,
            ScrollBars = ScrollBars.Both,
            ItemTemplate = _ => new ControlText(text),
            Items = Enumerable.Range(0, 30).Select(value => (object?) value).ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 5));

        control.HorizontalOffset = 9;

        using Frame frame = new(new Size(10, 5));
        control.Render(frame.Canvas);
        // Near edge (viewport column 0) is text index 9 + 0 = 9 -> '9'.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("9");
        // Far edge (viewport column 8, the last content column before the reserved scrollbar
        // column) is text index 9 + 8 = 17 -> 'H'.
        FrameOracle.Get(frame, new Point(8, 0)).ShouldBe("H");
    }

    /// <summary>Verifies Rewindow widens a realized row to the same width the real (non-Rewindow)
    /// horizontal-scroll arrange path produces via Container.ResolveContentSlot -
    /// Math.Max(Extent.Width, Viewport.Width), which does not depend on the current offset - rather
    /// than to origin.Width.Add(HorizontalOffset). Regression for Rewindow instead widening rows to
    /// Viewport.Width + HorizontalOffset: the two formulas only agree at the single point
    /// HorizontalOffset == Extent.Width - Viewport.Width (the existing
    /// Rewindow_WhenHorizontalOffsetReachesViewportWidth_StillRendersRealizedRowContent test lands
    /// exactly there), so a row re-arranged by Rewindow at any other offset - including the common
    /// HorizontalOffset == 0 - used to measure/arrange narrower than a full layout pass would,
    /// shifting width-dependent template content (e.g. right- or stretch-aligned content) out of its
    /// true position. Asserts directly against the row's arranged content width rather than a
    /// hardcoded literal, since ListItem forces its template content to fill the row's full arranged
    /// width (ArrangeChild(..., ResolvedAxes.Width)) regardless of the content's own alignment.</summary>
    [Fact]
    public void Rewindow_WhenHorizontalOffsetIsNonZeroAndNonMaximum_ArrangesRealizedRowAtFullContentWidth()
    {
        const string text = "0123456789ABCDEFGHIJ";
        Dictionary<int, ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 1,
            ScrollBars = ScrollBars.Both,
            ItemTemplate = item =>
            {
                var index = (int) item!;
                var row = new ControlText(text);
                realized[index] = row;
                return row;
            },
            Items = Enumerable.Range(0, 30).Select(value => (object?) value).ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 5));

        // A mid-range offset - strictly between 0 (the common case) and the maximum (the single
        // point where the buggy and correct formulas coincide) - derived from the control's own
        // committed Extent/Viewport rather than a hardcoded literal.
        var maximumOffset = control.Extent.Width - control.Viewport.Width;
        maximumOffset.ShouldBeGreaterThan(1);
        var offset = maximumOffset / 2;
        offset.ShouldBeInRange(1, maximumOffset - 1);

        control.HorizontalOffset = offset;

        realized.ShouldContainKey(1);
        var expectedRowWidth = Math.Max(control.Extent.Width, control.Viewport.Width);
        realized[1].Bounds.Width.ShouldBe(expectedRowWidth);
    }

    /// <summary>Verifies RowHeight rejects non-positive cell counts.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RowHeight_WhenNonPositive_ThrowsArgumentOutOfRangeException(int value)
    {
        var control = Create("A", "B", "C");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.RowHeight = value);
        control.RowHeight.ShouldBeNull();
    }

    /// <summary>Verifies LineSize rejects a negative value.</summary>
    [Fact]
    public void LineSize_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var control = Create("A", "B", "C");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.LineSize = -1);
    }

    /// <summary>Verifies LineSize forwards to, and reads back from, the composed viewport.</summary>
    [Fact]
    public void LineSize_WhenSet_ForwardsToComposedViewport()
    {
        var control = Create("A", "B", "C");

        control.LineSize = 3;

        control.LineSize.ShouldBe(3);
    }

    /// <summary>Verifies ItemInvocation rejects an undefined value.</summary>
    [Fact]
    public void ItemInvocation_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var control = Create("A", "B", "C");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ItemInvocation = (ListItemInvocation) 99);
        control.ItemInvocation.ShouldBe(ListItemInvocation.SingleClick);
    }

    /// <summary>Verifies SelectionMode rejects an undefined value and leaves selection untouched.</summary>
    [Fact]
    public void SelectionMode_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SelectionMode = (ListSelectionMode) 99);

        control.SelectionMode.ShouldBe(ListSelectionMode.Single);
        control.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies BringIntoView(index) scrolls minimally to reveal an item below the
    /// viewport, addressed by position rather than a realized private control.</summary>
    [Fact]
    public void BringIntoView_WhenIndexIsBelowViewport_ScrollsToRevealIt()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));

        var moved = control.BringIntoView(19);

        moved.ShouldBeTrue();
        control.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies BringIntoView validates its index like the underlying realized-control
    /// lookup does.</summary>
    [Fact]
    public void BringIntoView_WhenIndexIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var control = Create("A", "B", "C");
        new LayoutEngine().Layout(control, new Size(10, 4));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.BringIntoView(3));
    }

    /// <summary>Verifies BringIntoView leaves the offset untouched and still reports true - fully
    /// contained, matching <see cref="Container.BringIntoView(ControlBase)"/>'s own "no clamping
    /// occurred" contract - once the requested item is already entirely inside the viewport.</summary>
    [Fact]
    public void BringIntoView_WhenIndexIsAlreadyFullyVisible_ReturnsTrueWithoutMovingOffset()
    {
        var control = new UiListView
        {
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));

        var moved = control.BringIntoView(0);

        moved.ShouldBeTrue();
        control.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies BringIntoView scrolls arithmetically by logical index - without requiring
    /// a realized row - once RowHeight opts into windowed realization, and that the target row is
    /// realized by the window Rewindow computes afterward.</summary>
    [Fact]
    public void BringIntoView_WhenRowHeightIsSet_ScrollsArithmeticallyAndRealizesTarget()
    {
        var control = new UiListView
        {
            RowHeight = 1,
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null"),
            Items = Enumerable.Range(0, 200).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 4));

        var moved = control.BringIntoView(150);

        moved.ShouldBeTrue();
        control.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies the mouse wheel scrolls a windowed viewport by the configured LineSize,
    /// not by one item - distinct from RowHeight, which stays at its own 3-cell fixture value.</summary>
    [Fact]
    public void Wheel_WhenLineSizeIsConfigured_ScrollsViewportByConfiguredCells()
    {
        var control = new UiListView
        {
            RowHeight = 3,
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(3) },
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray(),
            LineSize = 3
        };
        new LayoutEngine().Layout(control, new Size(10, 6));
        var target = control.HitTest(new Point(0, 0));
        _ = target.ShouldNotBeNull();

        _ = Router.Route(target, Events.Pointer, new PointerEventArgs(Wheel(wheelY: -1)));

        control.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies the mouse wheel still scrolls a windowed viewport by exactly one cell when
    /// LineSize keeps its default, pinning non-breakage of the pre-existing forwarding.</summary>
    [Fact]
    public void Wheel_WhenLineSizeIsDefault_ScrollsViewportByOneCell()
    {
        var control = new UiListView
        {
            RowHeight = 3,
            ItemTemplate = item => new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(3) },
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 6));
        var target = control.HitTest(new Point(0, 0));
        _ = target.ShouldNotBeNull();

        _ = Router.Route(target, Events.Pointer, new PointerEventArgs(Wheel(wheelY: -1)));

        control.VerticalOffset.ShouldBe(1);
    }

    private static void Click(PointerManager capture, Point point, Modifiers modifiers)
    {
        _ = capture.Dispatch(Pointer(point, PointerAction.Press, modifiers));
        _ = capture.Dispatch(Pointer(point, PointerAction.Release, modifiers));
    }

    private static Pointer Pointer(Point cells, PointerAction action, Modifiers modifiers) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        modifiers,
        isMotion: false,
        isCellPositionInferred: false);

    private static Pointer Wheel(int wheelY) => new(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX: 0,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    /// <summary>Verifies inserting at the end appends one item and realizes one control.</summary>
    [Fact]
    public void InsertItem_WhenAppended_AddsOneItemAndControl()
    {
        var control = Create("A", "B");

        control.InsertItem(2, "C");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting at the beginning shifts existing items.</summary>
    [Fact]
    public void InsertItem_WhenPrepended_ShiftsExistingItems()
    {
        var control = Create("B", "C");

        control.InsertItem(0, "A");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting in the middle maintains correct ordering.</summary>
    [Fact]
    public void InsertItem_WhenInsertedInMiddle_MaintainsOrdering()
    {
        var control = Create("A", "C");

        control.InsertItem(1, "B");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting shifts the selected index when it follows the insertion point.</summary>
    [Fact]
    public void InsertItem_WhenSelectionFollowsInsertionPoint_ShiftsSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.InsertItem(0, "Z");

        control.SelectedIndex.ShouldBe(2);
        control.Items[control.SelectedIndex].ShouldBe("B");
    }

    /// <summary>Verifies collection mutation remaps a deferred pre-layout reveal with its selected
    /// item instead of discarding the visibility request at the old logical index.</summary>
    [Fact]
    public void InsertItem_WhenPreLayoutSelectionShifts_RevealsTheShiftedSelectionAfterLayout()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            SelectedIndex = 5
        };

        control.InsertItem(0, "Inserted");
        new LayoutEngine().Layout(control, new Size(8, 3));

        control.SelectedIndex.ShouldBe(6);
        control.ActiveIndex.ShouldBe(6);
        control.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies inserting before the active index shifts it correctly.</summary>
    [Fact]
    public void InsertItem_WhenActiveIndexFollowsInsertionPoint_ShiftsActiveIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;

        control.InsertItem(0, "Z");

        control.ActiveIndex.ShouldBe(3);
    }

    /// <summary>Verifies inserting before the active row reports the shifted active index.</summary>
    [Fact]
    public void InsertItem_WhenActiveIndexShifts_NotifiesActiveIndexObservers()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.InsertItem(0, "Z");

        control.ActiveIndex.ShouldBe(3);
        notifications.Count(name => name == nameof(UiListView.ActiveIndex)).ShouldBe(1);
    }

    /// <summary>Verifies shifting selection indexes notifies selected properties without raising a selection event.</summary>
    [Fact]
    public void InsertItem_WhenSelectionFollowsInsertionPoint_NotifiesSelectedPropertyObservers()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;
        List<string?> notifications = [];
        var selectionChanged = 0;
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.SelectionChanged += (_, _) => selectionChanged++;

        control.InsertItem(0, "Z");

        control.SelectedIndex.ShouldBe(3);
        control.SelectedItem.ShouldBe("C");
        control.SelectedItems.ShouldBe(new object?[] { "C" });
        notifications.ShouldContain(nameof(UiListView.ActiveIndex));
        notifications.ShouldContain(nameof(UiListView.SelectedIndex));
        notifications.ShouldContain(nameof(UiListView.SelectedItem));
        notifications.ShouldContain(nameof(UiListView.SelectedItems));
        selectionChanged.ShouldBe(0);
    }

    /// <summary>Verifies inserting after the selection does not shift it.</summary>
    [Fact]
    public void InsertItem_WhenInsertedAfterSelection_PreservesSelectedIndex()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 0;

        control.InsertItem(2, "C");

        control.SelectedIndex.ShouldBe(0);
        control.Items[control.SelectedIndex].ShouldBe("A");
    }

    /// <summary>Verifies inserting into an empty list produces one item.</summary>
    [Fact]
    public void InsertItem_WhenListIsEmpty_ProducesOneItem()
    {
        var control = Create();

        control.InsertItem(0, "A");

        control.Items.ShouldBe(new object?[] { "A" });
    }

    /// <summary>Verifies removing the last item produces an empty list.</summary>
    [Fact]
    public void RemoveItem_WhenSingleItemRemoved_ProducesEmptyList()
    {
        var control = Create("A");

        control.RemoveItem(0);

        control.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies removing the first item shifts remaining items down.</summary>
    [Fact]
    public void RemoveItem_WhenFirstItemRemoved_ShiftsRemainingItems()
    {
        var control = Create("A", "B", "C");

        control.RemoveItem(0);

        control.Items.ShouldBe(new object?[] { "B", "C" });
    }

    /// <summary>Verifies removing a middle item preserves surrounding items.</summary>
    [Fact]
    public void RemoveItem_WhenMiddleItemRemoved_PreservesSurroundingItems()
    {
        var control = Create("A", "B", "C");

        control.RemoveItem(1);

        control.Items.ShouldBe(new object?[] { "A", "C" });
    }

    /// <summary>Verifies removing a selected item clears the selection.</summary>
    [Fact]
    public void RemoveItem_WhenSelectedItemRemoved_ClearsSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.RemoveItem(1);

        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies removing the active row never leaves a disabled row active.</summary>
    [Fact]
    public void RemoveItem_WhenNextActiveRowIsDisabled_FallsBackToNoActiveRow()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var label = new ControlText((string) item!);

                if (Equals(item, "B"))
                {
                    label.IsEnabled = false;
                }

                realized.Add(label);
                return label;
            },
            Items = ["A", "B"],
            SelectedIndex = 0
        };

        control.RemoveItem(0);

        control.ActiveIndex.ShouldBe(-1);
        realized[1].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies removing before the selection shifts the selected index down.</summary>
    [Fact]
    public void RemoveItem_WhenRemovedBeforeSelection_ShiftsSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;

        control.RemoveItem(0);

        control.SelectedIndex.ShouldBe(1);
        control.Items[control.SelectedIndex].ShouldBe("C");
    }

    /// <summary>Verifies removing one of shifted multiple selections reports the stable removed occurrence.</summary>
    [Fact]
    public void RemoveItem_WhenSelectedItemsShift_ReportsStableSelectionDelta()
    {
        var control = Create("A", "B", "C");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);
        ListSelectionChangedEventArgs? changed = null;
        control.SelectionChanged += (_, eventArgs) => changed = eventArgs;

        control.RemoveItem(1);

        _ = changed.ShouldNotBeNull();
        changed.RemovedIndexes.ToArray().ShouldBe([1]);
        changed.AddedIndexes.ToArray().ShouldBeEmpty();
        control.SelectedItems.ShouldBe(new object?[] { "C" });
    }

    /// <summary>Verifies removing after the selection preserves it.</summary>
    [Fact]
    public void RemoveItem_WhenRemovedAfterSelection_PreservesSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 0;

        control.RemoveItem(2);

        control.SelectedIndex.ShouldBe(0);
        control.Items[control.SelectedIndex].ShouldBe("A");
    }

    /// <summary>Verifies a reentrant removal triggered from the outer removal's own
    /// SelectionChanged notification wins the active row - the outer removal's now-stale
    /// continuation must be skipped instead of re-applying its own index shift on top of an active
    /// row the reentrant removal already resolved, which would otherwise split the active row from
    /// the (correctly unaffected) selection.</summary>
    [Fact]
    public void RemoveItem_WhenSelectionChangedReentersWithAnotherRemoval_PreservesReentrantActiveRow()
    {
        var control = Create("A", "B", "C", "D", "E");
        control.SelectedIndex = 1;
        var reentered = false;
        control.SelectionChanged += (_, _) =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            control.RemoveItem(0);
            control.SelectedIndex = 2;
        };

        control.RemoveItem(1);

        control.Items.ShouldBe(new object?[] { "C", "D", "E" });
        control.SelectedIndex.ShouldBe(2);
        control.ActiveIndex.ShouldBe(2);
    }

    /// <summary>Verifies replacing an item swaps the value and realized control.</summary>
    [Fact]
    public void ReplaceItem_WhenCalled_SwapsItemValue()
    {
        var control = Create("A", "B", "C");

        control.ReplaceItem(1, "X");

        control.Items.ShouldBe(new object?[] { "A", "X", "C" });
    }

    /// <summary>Verifies replacing a selected item with an equal value preserves the selection.</summary>
    [Fact]
    public void ReplaceItem_WhenSelectedItemIsEqual_PreservesSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.ReplaceItem(1, "B");

        control.SelectedIndex.ShouldBe(1);
        control.Items[control.SelectedIndex].ShouldBe("B");
    }

    /// <summary>Verifies replacing one selected item with an unequal value narrows selection and keeps the active row valid.</summary>
    [Fact]
    public void ReplaceItem_WhenSelectedItemIsUnequal_ClearsOnlyReplacedSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);

        control.ReplaceItem(1, "X");

        control.SelectedItems.ShouldBe(new object?[] { "C" });
        control.SelectedIndex.ShouldBe(2);
        control.ActiveIndex.ShouldBe(2);
        control.Items[control.ActiveIndex].ShouldBe("C");
    }

    /// <summary>Verifies replacing the active row with a disabled row selects the nearest available fallback.</summary>
    [Fact]
    public void ReplaceItem_WhenReplacementIsDisabled_FallsBackToAvailableActiveIndex()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var text = new ControlText((string) item!);

                if (Equals(item, "Disabled"))
                {
                    text.IsEnabled = false;
                }

                realized.Add(text);
                return text;
            },
            Items = ["A", "B"],
            SelectedIndex = 1
        };

        control.ReplaceItem(1, "Disabled");

        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBe("A");
        realized[^1].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies a reentrant removal triggered from the outer replacement's own
    /// SelectionChanged notification - one that never touches the replaced index's anchor itself -
    /// is not overwritten by the outer replacement's stale continuation afterward. The reentrant
    /// removal correctly leaves the anchor untouched (it removes an unrelated, later item); the
    /// outer replacement's own anchor-repair check coincidentally still matches its now-stale
    /// index and, unfixed, forces the anchor to -1 anyway. A later Shift-click range-select from
    /// that anchor is the only way to observe the private anchor field: a live anchor covers both
    /// endpoints, while a corrupted (-1) anchor collapses the gesture to an exclusive single pick.</summary>
    [Fact]
    public async Task ReplaceItem_WhenSelectionChangedReentersWithUnrelatedRemoval_PreservesAnchorForFollowingRangeSelectAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = Create("A", "B", "C", "D", "E");
        control.SelectionMode = ListSelectionMode.Multiple;
        control.Bounds = new Rect(0, 0, 5, 5);
        new LayoutEngine().Layout(control, new Size(5, 5));
        control.SelectedIndex = 1;
        var reentered = false;
        control.SelectionChanged += (_, _) =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;

            // Unrelated to the anchor at index 1: removing the last item must never disturb it.
            control.RemoveItem(4);
        };

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using PointerManager capture = new(control);

            // Replaces the anchor/selected item itself, clearing selection and reentering via
            // SelectionChanged into the unrelated removal above.
            control.ReplaceItem(1, "Z");

            // A Shift-click range-select from the still-live anchor (index 1) down to index 2
            // must cover both.
            Click(capture, new Point(0, 2), Modifiers.Shift);
        }, TestContext.Current.CancellationToken);

        control.SelectedItems.ShouldBe(new object?[] { "Z", "C" });
    }

    /// <summary>Verifies multiple sequential inserts produce the correct final state.</summary>
    [Fact]
    public void InsertItem_WhenMultipleSequentialInserts_ProducesCorrectState()
    {
        var control = Create();

        control.InsertItem(0, "C");
        control.InsertItem(0, "A");
        control.InsertItem(1, "B");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies interleaved insert and remove produces correct state.</summary>
    [Fact]
    public void InsertAndRemove_WhenInterleaved_ProducesCorrectState()
    {
        var control = Create("A", "B", "C");

        control.InsertItem(1, "X");
        control.RemoveItem(3);
        control.InsertItem(3, "D");

        control.Items.ShouldBe(new object?[] { "A", "X", "B", "D" });
    }

    /// <summary>Verifies removing the last item when it is active clamps the active index.</summary>
    [Fact]
    public void RemoveItem_WhenLastItemIsActive_ClampsActiveIndex()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 1;

        control.RemoveItem(1);

        control.ActiveIndex.ShouldBeLessThan(control.Items.Count);
    }

    /// <summary>Verifies removing the item that is itself the selection anchor repairs the anchor to
    /// the active item rather than leaving it pointing at whatever item slid into the removed slot -
    /// otherwise a later Shift-click range-selects from the wrong, stale position.</summary>
    [Fact]
    public async Task RemoveItem_WhenAnchorItselfIsRemoved_RepairsSelectionAnchorAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = Create("A", "B", "C", "D");
        control.Bounds = new Rect(0, 0, 4, 4);
        control.SelectionMode = ListSelectionMode.Multiple;
        new LayoutEngine().Layout(control, new Size(4, 4));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using PointerManager capture = new(control);

            // Selects B and sets the anchor to index 1.
            Click(capture, new Point(0, 1), Modifiers.None);

            // Extends the range to D; the anchor stays at index 1 (B) while D becomes active.
            Click(capture, new Point(0, 3), Modifiers.Shift);

            // Removes B, the anchor item itself. The active item (D) shifts from index 3 to
            // index 2 and remains selected, so the repaired anchor should follow it there.
            control.RemoveItem(1);

            // Ranging from the repaired anchor (D, now at index 2) back to A must cover A, C, and
            // D. A stale anchor left at index 1 (now C, after the shift) would wrongly narrow the
            // range to just A and C.
            Click(capture, new Point(0, 0), Modifiers.Shift);
        }, TestContext.Current.CancellationToken);

        control.SelectedItems.ShouldBe(new object?[] { "A", "C", "D" });
    }

    /// <summary>Verifies multiple selections shift correctly on insert in Multiple mode.</summary>
    [Fact]
    public void InsertItem_WhenMultipleSelectionMode_ShiftsAllSelectedIndices()
    {
        var control = Create("A", "B", "C", "D");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(3, true);

        control.InsertItem(0, "Z");

        control.SelectedItems.ShouldBe(new object?[] { "B", "D" });
    }

    /// <summary>Verifies collapsing the current row's own content eagerly repairs ActiveIndex and
    /// drops that index from selection, rather than leaving stale state until the next navigation
    /// key press.</summary>
    [Fact]
    public void Visibility_WhenCurrentItemContentCollapses_RepairsActiveIndexAndDropsSelection()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectedIndex = 1
        };
        ListSelectionChangedEventArgs? changed = null;
        control.SelectionChanged += (_, eventArgs) => changed = eventArgs;

        realized[1].Visibility = Visibility.Collapsed;

        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItems.ShouldBeEmpty();
        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBe("A");
        _ = changed.ShouldNotBeNull();
        changed.AddedIndexes.ToArray().ShouldBeEmpty();
        changed.RemovedIndexes.ToArray().ShouldBe([1]);
    }

    /// <summary>Verifies Hidden repairs the current row the same way Collapsed does - both make
    /// content EffectiveIsVisible false, and this repair has no Hidden-specific branch.</summary>
    [Fact]
    public void Visibility_WhenCurrentItemContentBecomesHidden_RepairsActiveIndexAndDropsSelection()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectedIndex = 1
        };

        realized[1].Visibility = Visibility.Hidden;

        control.SelectedIndex.ShouldBe(-1);
        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBe("A");
    }

    /// <summary>Verifies disabling selected realized content repairs active and selected state in
    /// both eager and fixed-row-height realization modes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsEnabled_WhenSelectedRowContentBecomesDisabled_RepairsAvailability(bool virtualized)
    {
        // Arrange
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = virtualized ? 1 : null,
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B"],
            SelectedIndex = 1
        };
        new LayoutEngine().Layout(control, new Size(10, 2));
        var changes = new List<ListSelectionChangedEventArgs>();
        control.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        // Act
        realized.Single(item => item.Content == "B").IsEnabled = false;

        // Assert
        control.SelectedIndex.ShouldBe(-1);
        control.ActiveIndex.ShouldBe(0);
        var change = changes.ShouldHaveSingleItem();
        change.RemovedIndexes.ToArray().ShouldBe([1]);

        realized.Single(item => item.Content == "B").IsEnabled = true;
        control.SelectedIndex.ShouldBe(-1);
        control.ActiveIndex.ShouldBe(0);
        changes.Count.ShouldBe(1);
    }

    /// <summary>Verifies collapsing the sole row's content clears both ActiveIndex and selection
    /// instead of indexing an empty available set.</summary>
    [Fact]
    public void Visibility_WhenOnlySelectedItemContentCollapses_ClearsActiveIndexAndSelection()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A"],
            SelectedIndex = 0
        };

        realized[0].Visibility = Visibility.Collapsed;

        control.ActiveIndex.ShouldBe(-1);
        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItems.ShouldBeEmpty();
    }

    /// <summary>Verifies collapsing one selected row's content in Multiple mode drops only that
    /// index - ListView selection is drop-only and never promotes to an adjacent row the way
    /// NavigationView's own group-collapse repair does.</summary>
    [Fact]
    public void Visibility_WhenSoleSelectedRowContentCollapsesInMultipleMode_DropsSelectionWithoutPromotingAdjacentRow()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectionMode = ListSelectionMode.Multiple
        };
        _ = control.SetSelected(1, true);

        realized[1].Visibility = Visibility.Collapsed;

        control.SelectedItems.ShouldBeEmpty();
        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies Multiple mode only drops the row that became ineligible, leaving every
    /// other selected index untouched.</summary>
    [Fact]
    public void Visibility_WhenOneOfSeveralSelectedRowsCollapsesInMultipleMode_DropsOnlyThatIndex()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectionMode = ListSelectionMode.Multiple
        };
        _ = control.SetSelected(0, true);
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);

        realized[1].Visibility = Visibility.Collapsed;

        control.SelectedItems.ShouldBe(new object?[] { "A", "C" });
    }

    /// <summary>Verifies a row that survives RemoveItem's index shift still repairs correctly when
    /// its content collapses, proving the AvailabilityChanged subscription moved with the shifted
    /// index rather than a stale handler firing for the disposed removed row.</summary>
    [Fact]
    public void Visibility_WhenSurvivingRowContentCollapsesAfterEarlierRemoval_RepairsShiftedIndex()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"]
        };
        control.RemoveItem(0);
        control.SelectedIndex = 1;
        var changed = 0;
        control.SelectionChanged += (_, _) => changed++;

        _ = Should.NotThrow(() => realized[2].Visibility = Visibility.Collapsed);

        changed.ShouldBe(1);
        control.SelectedIndex.ShouldBe(-1);
        control.ActiveIndex.ShouldBe(0);
        control.Items.ShouldBe(new object?[] { "B", "C" });
    }

    /// <summary>Verifies a replacement row's content collapsing repairs selection, proving the
    /// new row was subscribed and the disposed previous row left no stale callback behind.</summary>
    [Fact]
    public void Visibility_WhenReplacementRowContentCollapses_RepairsSelectionWithoutStaleCallback()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"]
        };
        control.ReplaceItem(1, "X");
        control.SelectedIndex = 1;
        var changed = 0;
        control.SelectionChanged += (_, _) => changed++;

        _ = Should.NotThrow(() => realized[^1].Visibility = Visibility.Collapsed);

        changed.ShouldBe(1);
        control.SelectedIndex.ShouldBe(-1);
        control.Items[1].ShouldBe("X");
    }

    /// <summary>Verifies a freshly realized row's content collapsing after a full Items reset
    /// repairs selection, proving every disposed pre-reset row's subscription was torn down and
    /// every newly realized row was wired.</summary>
    [Fact]
    public void Visibility_WhenRowContentCollapsesAfterItemsReset_RepairsSelectionWithoutStaleCallback()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B"]
        };

        control.Items = ["X", "Y", "Z"];
        control.SelectedIndex = 2;
        var changed = 0;
        control.SelectionChanged += (_, _) => changed++;

        _ = Should.NotThrow(() => realized[^1].Visibility = Visibility.Collapsed);

        changed.ShouldBe(1);
        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies a row becoming visible again never reclaims selection or active state -
    /// becoming available never auto-reclaims, matching removal's own no-auto-reclaim precedent.</summary>
    [Fact]
    public void Visibility_WhenCollapsedRowContentBecomesVisibleAgain_DoesNotReclaimSelectionOrActive()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectedIndex = 1
        };
        realized[1].Visibility = Visibility.Collapsed;
        var activeAfterCollapse = control.ActiveIndex;

        realized[1].Visibility = Visibility.Visible;

        control.SelectedIndex.ShouldBe(-1);
        control.SelectedItems.ShouldBeEmpty();
        control.ActiveIndex.ShouldBe(activeAfterCollapse);
    }

    private const int _corpusSeed = 0x4C15757A;

    /// <summary>Verifies 40 independently seeded randomized cases of mixed operations never
    /// desynchronize a virtualized ListView from its eager twin.</summary>
    [Fact]
    public void Mutate_WhenOperationsAreRandomized_MatchesEagerRealization()
    {
        var corpus = new Random(_corpusSeed);

        for (var caseIndex = 0; caseIndex < 40; caseIndex++)
        {
            RunCase(caseIndex, corpus.Next());
        }
    }

    private static void RunCase(int caseIndex, int caseSeed)
    {
        var random = new Random(caseSeed);
        var itemCount = random.Next(10, 120);
        var items = Enumerable.Range(0, itemCount).Select(value => (object?) $"Item {value:D4}").ToArray();

        // Stretched to the full given size on both axes so Bounds never depends on content width -
        // eager reports the widest realized row's natural width while virtualized reports only its
        // currently realized subset's width (see the Extent.Width note below), an accepted
        // difference that is otherwise indistinguishable from a real desynchronization once it
        // changes which cell a render comparison is even looking at.
        var eager = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = items
        };
        var virtualized = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowHeight = 1,
            Items = items
        };

        var size = new Size(random.Next(6, 20), random.Next(2, 12));
        var engine = new LayoutEngine();
        engine.Layout(eager, size);
        engine.Layout(virtualized, size);
        AssertEquivalent(engine, eager, virtualized, size, caseIndex, caseSeed, step: -1);

        for (var step = 0; step < 120; step++)
        {
            switch (random.Next(7))
            {
                case 0:
                    {
                        var delta = random.Next(-10, 11);
                        _ = eager.ScrollBy(0, delta);
                        _ = virtualized.ScrollBy(0, delta);
                        break;
                    }
                case 1 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        var selected = random.Next(2) == 0;
                        _ = eager.SetSelected(index, selected);
                        _ = virtualized.SetSelected(index, selected);
                        break;
                    }
                case 2:
                    {
                        var code = RandomNavigationKey(random);
                        _ = eager.MoveCurrent(code);
                        _ = virtualized.MoveCurrent(code);
                        break;
                    }
                case 3:
                    {
                        size = new Size(random.Next(6, 20), random.Next(2, 12));
                        engine.Layout(eager, size);
                        engine.Layout(virtualized, size);
                        break;
                    }
                case 4:
                    {
                        var index = random.Next(eager.Items.Count + 1);
                        var value = (object?) $"New {step:D3}";

                        // Only virtualized's fixed-RowHeight branch anchors VerticalOffset to the
                        // same logical first-visible row on an insertion at or above it - eager
                        // rows have no fixed per-row size to make that arithmetic safe, so it
                        // stays unchanged there by design. Every realized row here is exactly one
                        // cell tall (the default single-line template, matching RowHeight = 1
                        // below), so mirroring the identical shift on eager keeps this parity
                        // baseline honest instead of comparing against eager's un-anchored offset.
                        var firstVisible = eager.VerticalOffset;
                        eager.InsertItem(index, value);
                        virtualized.InsertItem(index, value);

                        if (index <= firstVisible)
                        {
                            // Mirrors production's own stale-Extent gotcha: eager.InsertItem just
                            // added an unmeasured row, so eager's own Extent still reflects the
                            // pre-insertion height until a layout pass runs. Without settling that
                            // first, this compensating ScrollBy could get clamped against the same
                            // stale bound the production compensation once did, desynchronizing
                            // this baseline from virtualized's now-correctly-computed offset
                            // instead of actually verifying it.
                            engine.Layout(eager, size);
                            _ = eager.ScrollBy(0, 1);
                        }

                        break;
                    }
                case 5 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        var firstVisible = eager.VerticalOffset;
                        eager.RemoveItem(index);
                        virtualized.RemoveItem(index);

                        if (index < firstVisible)
                        {
                            _ = eager.ScrollBy(0, -1);
                        }

                        break;
                    }
                case 6 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        var value = (object?) $"Replaced {step:D3}";
                        eager.ReplaceItem(index, value);
                        virtualized.ReplaceItem(index, value);
                        break;
                    }
                default:
                    break;
            }

            AssertEquivalent(engine, eager, virtualized, size, caseIndex, caseSeed, step);
        }
    }

    private static Code RandomNavigationKey(Random random) => random.Next(6) switch
    {
        0 => Code.Up,
        1 => Code.Down,
        2 => Code.Home,
        3 => Code.End,
        4 => Code.PageUp,
        _ => Code.PageDown
    };

    private static void AssertEquivalent(
        LayoutEngine engine,
        UiListView eager,
        UiListView virtualized,
        Size size,
        int caseIndex,
        int caseSeed,
        int step)
    {
        var context = $"corpus {_corpusSeed:X}, case {caseIndex} (seed {caseSeed:X}), step {step}";

        // A structural mutation (insert/remove/replace) never implicitly re-runs layout for
        // either mode - eager's own newly built row is left unmeasured and unarranged exactly the
        // same way a virtualized row would be without Rewindow's direct-arrange bridge. Settling
        // layout first, before every assertion below, matches how a real render loop always
        // measures and arranges before every frame; it is not a parity target of realization mode
        // itself, and asserting state before this could observe a transient pre-settle snapshot
        // that the very next layout pass would still change.
        engine.Layout(eager, size);
        engine.Layout(virtualized, size);

        virtualized.Items.ShouldBe(eager.Items, context);
        virtualized.SelectedIndex.ShouldBe(eager.SelectedIndex, context);
        virtualized.ActiveIndex.ShouldBe(eager.ActiveIndex, context);
        virtualized.SelectedItems.ShouldBe(eager.SelectedItems, context);

        // Extent.Width is not a parity target: both modes report the widest currently realized
        // row's natural content width, but virtualized only ever realizes a window of rows, so its
        // reported width can jitter across a scroll depending on which rows happen to be realized
        // at the moment it is read. Neither value matters functionally, since ListView never
        // enables horizontal scrolling by default.
        virtualized.Extent.Height.ShouldBe(eager.Extent.Height, context);
        virtualized.VerticalOffset.ShouldBe(eager.VerticalOffset, context);

        using var eagerFrame = new Frame(new Size(eager.Bounds.Width, eager.Bounds.Height));
        using var virtualizedFrame = new Frame(new Size(virtualized.Bounds.Width, virtualized.Bounds.Height));
        eager.Render(eagerFrame.Canvas);
        virtualized.Render(virtualizedFrame.Canvas);

        for (var y = 0; y < eager.Bounds.Height; y++)
        {
            for (var x = 0; x < eager.Bounds.Width; x++)
            {
                var point = new Point(x, y);
                FrameOracle.Get(virtualizedFrame, point).ShouldBe(FrameOracle.Get(eagerFrame, point), $"{context}, cell ({x},{y})");
            }
        }
    }

    /// <summary>Verifies collapsing a realized item's template content in eager (auto-height) mode
    /// removes that row's contribution to ListView's own desired height and reflows the surviving
    /// rows without a gap - ListItem.MeasureOverride forces its own DesiredSize to zero when its
    /// Content is Collapsed, and ListViewHost's auto-height measure/arrange sums that same zero
    /// contribution, so the net effect matches a genuinely skipped row even though the host's own
    /// per-child Collapsed check (which inspects the realized ListItem wrapper, never toggled by
    /// ListView itself) never actually trips.</summary>
    [Fact]
    public void Visibility_WhenItemContentCollapsesInEagerMode_RemovesRowAndReflowsSiblings()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = new object?[] { "A", "B", "C" }
        };
        var engine = new LayoutEngine();
        var size = new Size(10, 5);
        engine.Layout(control, size);
        control.DesiredSize.ShouldBe(new Size(1, 3));

        realized[1].Visibility = Visibility.Collapsed;
        engine.Layout(control, size);

        control.DesiredSize.ShouldBe(new Size(1, 2));
        realized[0].Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        realized[2].Bounds.ShouldBe(new Rect(0, 1, 1, 1));
        using Frame frame = new(size);
        control.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("C");

        // A further transition back to IsVisible restores the original geometry - proving the
        // Collapsed skip is a live measure/arrange decision, not a one-time realization effect.
        realized[1].Visibility = Visibility.Visible;
        engine.Layout(control, size);

        control.DesiredSize.ShouldBe(new Size(1, 3));
        realized[1].Bounds.ShouldBe(new Rect(0, 1, 1, 1));
        realized[2].Bounds.ShouldBe(new Rect(0, 2, 1, 1));
    }

    /// <summary>Verifies a Hidden realized item's template content keeps its row slot - both in
    /// ListView's own desired height and in the row's committed arranged Bounds - while excluding
    /// only rendering, matching the documented Hidden contract ("keeps its measured/arranged slot
    /// but does not render or accept input").</summary>
    [Fact]
    public void Visibility_WhenItemContentIsHiddenInEagerMode_RetainsRowSlotButRendersNothing()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
            Items = new object?[] { "A", "B", "C" }
        };
        var engine = new LayoutEngine();
        var size = new Size(10, 5);
        engine.Layout(control, size);

        realized[1].Visibility = Visibility.Hidden;
        engine.Layout(control, size);

        control.DesiredSize.ShouldBe(new Size(1, 3));
        realized[1].Bounds.ShouldBe(new Rect(0, 1, 1, 1));
        realized[2].Bounds.ShouldBe(new Rect(0, 2, 1, 1));
        using Frame frame = new(size);
        control.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("C");
    }

    /// <summary>Verifies a Hidden realized item's template content keeps its row's full arithmetic
    /// slot while RowHeight virtualization is active, and that ListView's scroll Extent - purely
    /// ItemCount * RowHeight by design - is unaffected by any per-item visibility.</summary>
    [Fact]
    public void Visibility_WhenItemContentIsHiddenInRowHeightMode_RetainsArithmeticRowSlotAndExtent()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 1,
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(1) }),
            Items = new object?[] { "A", "B", "C" }
        };
        var engine = new LayoutEngine();
        var size = new Size(10, 5);
        engine.Layout(control, size);
        var baselineExtent = control.Extent;

        realized[1].Visibility = Visibility.Hidden;
        engine.Layout(control, size);

        control.Extent.ShouldBe(baselineExtent);
        realized[1].Bounds.ShouldBe(new Rect(0, 1, 1, 1));
        using Frame frame = new(size);
        control.Render(frame.Canvas);
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies collapsing a realized row's template content while RowHeight virtualization
    /// is active zero-arranges that row's wrapper instead of tripping ListViewHost's fixed-height
    /// Debug.Assert - the wrapper's own DesiredSize already mirrors the collapsed content's zero
    /// size, so arranging it to a zero-height slot rather than the full RowHeight satisfies the
    /// asserted contract exactly like a genuinely short template would. Sibling rows keep their
    /// untouched arithmetic Y, proving the collapse never perturbs the rest of the fixed grid.</summary>
    [Fact]
    public void Arrange_WhenRealizedRowContentCollapsesInRowHeightMode_ZeroArrangesRowAndKeepsSiblingSlots()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 2,
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(2) }),
            Items = new object?[] { "A", "B", "C" }
        };
        var engine = new LayoutEngine();
        engine.Layout(control, new Size(10, 6));

        realized[1].Visibility = Visibility.Collapsed;

        // A same-size relayout is a no-op here: nothing about the row's own arrange slot would
        // differ from its last committed one, so the per-row Arrange call inside ArrangeOverride
        // would short-circuit before ever reaching the collapsed check below. A genuinely different
        // overall size forces every row's slot to actually change, guaranteeing this exercises the
        // fixed-height branch rather than silently no-oping.
        engine.Layout(control, new Size(12, 7));

        var collapsed = (ListItem) realized[1].Parent!;
        var first = (ListItem) realized[0].Parent!;
        var last = (ListItem) realized[2].Parent!;
        collapsed.Bounds.Height.ShouldBe(0);
        first.Bounds.Y.ShouldBe(0);
        last.Bounds.Y.ShouldBe(4);
    }

    /// <summary>Verifies the dedicated Rewindow re-arrange path - reached only by an interactive
    /// scroll, never by a full container arrange - applies the identical zero-arrange treatment to a
    /// collapsed in-window row. Unlike ArrangeOverride's own fixed-height branch, Rewindow's per-row
    /// arrange never carried a Debug.Assert at all, so a mismatched template there silently produced
    /// a stale full-height Bounds instead of failing loudly; this pins the fix at the site that used
    /// to fail silently in both Debug and Release.</summary>
    [Fact]
    public void Rewindow_WhenInWindowRowContentCollapsesThenScrolls_ZeroArrangesRow()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 2,
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(2) }),
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 6));
        var row = (ListItem) realized[1].Parent!;

        realized[1].Visibility = Visibility.Collapsed;
        var moved = control.ScrollBy(0, 1);

        moved.ShouldBeTrue();
        row.Bounds.Height.ShouldBe(0);
    }

    /// <summary>Verifies ListViewHost's fixed-height RowHeight contract still holds for a genuinely
    /// mismatched non-collapsed template - the collapsed short-circuit added above only covers the
    /// specific case content reports as Collapsed. Debug builds assert loudly; Release builds clip
    /// the disagreeing template into the full arithmetic slot rather than misaligning siblings.</summary>
    [Fact]
    public void Arrange_WhenRealizedRowTemplateHeightDisagreesWithRowHeight_StillEnforcesTheSlot()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            RowHeight = 2,
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null") { Height = Length.Cells(2) }),
            Items = new object?[] { "A", "B", "C" }
        };
        var engine = new LayoutEngine();
        engine.Layout(control, new Size(10, 6));

        realized[1].Height = Length.Cells(1);

#if DEBUG
        _ = Should.Throw<Xunit.Sdk.TraceAssertException>(() => engine.Layout(control, new Size(12, 7)));
#else
        engine.Layout(control, new Size(12, 7));
        var row = (ListItem) realized[1].Parent!;
        row.Bounds.Height.ShouldBe(2);
        ((ListItem) realized[2].Parent!).Bounds.Y.ShouldBe(row.Bounds.Y + 2);
#endif
    }

    /// <summary>Verifies an insertion at or above the current first-visible row shifts
    /// VerticalOffset by exactly one RowHeight, so the item that was first-visible before the
    /// insertion is still first-visible afterward.</summary>
    [Fact]
    public void InsertItem_WhenIndexIsAtOrAboveFirstVisibleRowInFixedRowHeightMode_ShiftsVerticalOffsetByRowHeight()
    {
        var control = CreateAnchored();
        _ = control.ScrollBy(0, 20);
        control.VerticalOffset.ShouldBe(20);
        var anchoredItem = FirstVisibleItem(control);

        control.InsertItem(0, "New 00");

        control.VerticalOffset.ShouldBe(21);
        FirstVisibleItem(control).ShouldBe(anchoredItem);
    }

    /// <summary>Verifies the compensation still reaches the true post-insertion maximum when the
    /// list was already scrolled to its exact current maximum before the insertion. Extent only
    /// refreshes on a layout pass, which has not run yet at compensation time, so a naive clamp
    /// against that still-stale, pre-insertion maximum would otherwise swallow the compensating
    /// shift entirely and leave the wrong item first-visible.</summary>
    [Fact]
    public void InsertItem_WhenScrolledToExactCurrentMaximumInFixedRowHeightMode_ReachesTruePostInsertionMaximum()
    {
        var control = CreateAnchored();
        _ = control.ScrollBy(0, 35);
        control.VerticalOffset.ShouldBe(35);
        var anchoredItem = FirstVisibleItem(control);

        control.InsertItem(0, "New 00");

        control.VerticalOffset.ShouldBe(36);
        FirstVisibleItem(control).ShouldBe(anchoredItem);
    }

    /// <summary>Mirrors the insertion case: a removal strictly above the current first-visible row
    /// shifts VerticalOffset by exactly one RowHeight, clamped no lower than zero, so the item that
    /// was first-visible before the removal is still first-visible afterward.</summary>
    [Fact]
    public void RemoveItem_WhenIndexIsAboveFirstVisibleRowInFixedRowHeightMode_ShiftsVerticalOffsetByRowHeight()
    {
        var control = CreateAnchored();
        _ = control.ScrollBy(0, 20);
        control.VerticalOffset.ShouldBe(20);
        var anchoredItem = FirstVisibleItem(control);

        control.RemoveItem(5);

        control.VerticalOffset.ShouldBe(19);
        FirstVisibleItem(control).ShouldBe(anchoredItem);
    }

    /// <summary>Verifies the anchor compensation does not fire for a mutation at or below the
    /// visible window - an insertion below the first-visible row, an append at the end, and a
    /// removal of the first-visible row itself (which has no surviving logical item to re-anchor
    /// to) all leave VerticalOffset exactly where it was, guarding against over-correction.</summary>
    [Fact]
    public void InsertOrRemoveItem_WhenIndexIsAtOrBelowFirstVisibleRow_LeavesVerticalOffsetUnchanged()
    {
        var control = CreateAnchored();
        _ = control.ScrollBy(0, 20);
        control.VerticalOffset.ShouldBe(20);

        control.InsertItem(21, "New 21");
        control.VerticalOffset.ShouldBe(20);

        control.InsertItem(control.Items.Count, "Appended");
        control.VerticalOffset.ShouldBe(20);

        control.RemoveItem(20);
        control.VerticalOffset.ShouldBe(20);
    }

    /// <summary>Verifies eager (non-fixed-RowHeight) mode is entirely unaffected by this
    /// compensation - RowHeight left unset keeps InsertItem/RemoveItem's original behavior of
    /// leaving VerticalOffset numerically unchanged, regardless of where the mutation occurs.</summary>
    [Fact]
    public void InsertOrRemoveItem_WhenRowHeightIsUnset_NeverCompensatesVerticalOffset()
    {
        var control = new UiListView
        {
            Items = Enumerable.Range(0, 40).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 5));
        _ = control.ScrollBy(0, 20);
        control.VerticalOffset.ShouldBe(20);

        control.InsertItem(0, "New 00");
        control.VerticalOffset.ShouldBe(20);

        control.RemoveItem(5);
        control.VerticalOffset.ShouldBe(20);
    }

    private static UiListView CreateAnchored()
    {
        var control = new UiListView
        {
            RowHeight = 1,
            Items = Enumerable.Range(0, 40).Select(value => (object?) $"Item {value}").ToArray()
        };
        new LayoutEngine().Layout(control, new Size(10, 5));
        return control;
    }

    private static object? FirstVisibleItem(UiListView control) =>
        control.Items[control.VerticalOffset / control.RowHeight!.Value];
}
