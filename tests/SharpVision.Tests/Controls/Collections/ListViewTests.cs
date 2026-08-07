// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;


/// <summary>Verifies realized ListView ownership, selection, input, scrolling, and rendering.</summary>
public sealed class ListViewTests
{
    /// <summary>Verifies a ListView starts as a quiet borderless collection surface without caller styling.</summary>
    [ComponentUnitEvidence(typeof(UiListView))]
    [Fact]
    public void Constructor_WhenCreated_UsesQuietBackgroundDefaults()
    {
        // Arrange and act
        var control = new UiListView();

        // Assert
        control.ActualBorder.Sides.ShouldBe(BorderSide.None);
        control.Face.Background.ShouldBe(SemanticColor.Control);
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
        previous.All(item => item is { Disposed: false, Parent: not null }).ShouldBeTrue();
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

        previous.All(item => item is { Disposed: true, Parent: null }).ShouldBeTrue();
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
        realized[1].Enabled = false;
        control.SelectedIndex = 0;

        control.SetSelected(1, true).ShouldBeFalse();

        control.SelectedIndex.ShouldBe(0);
        control.SelectedItem.ShouldBe("A");
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
        realized[1].Enabled = false;
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
                    label.Enabled = false;
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
                    label.Enabled = false;
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
        realized[1].Enabled = false;
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

    private static void Key(ControlBase target, Code code, Rune? character = null) =>
        _ = Router.Route(
            target,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

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
        _ = changes.ShouldHaveSingleItem();
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
            ItemTemplate = item => Add(realized, new ControlText(item?.ToString() ?? "null")),
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

    /// <summary>Verifies ItemInvocation rejects an undefined value.</summary>
    [Fact]
    public void ItemInvocation_WhenSetToUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var control = Create("A", "B", "C");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ItemInvocation = (ListItemInvocation) 99);
        control.ItemInvocation.ShouldBe(ListItemInvocation.SingleClick);
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
}
