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
                    label.Enabled = false;
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
                    text.Enabled = false;
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
                        eager.InsertItem(index, value);
                        virtualized.InsertItem(index, value);
                        break;
                    }
                case 5 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        eager.RemoveItem(index);
                        virtualized.RemoveItem(index);
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

        // Extent.Width is not a parity target: eager reports the widest realized row's natural
        // content width, while virtualized reports the incoming constraint width directly so
        // width never depends on which rows happen to be realized. Neither value
        // matters functionally, since ListView never enables horizontal scrolling by default.
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
}
