// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




using Label = ControlText;
using UiList = List;

/// <summary>Verifies realized List ownership, selection, input, scrolling, and rendering.</summary>
public sealed class ListTests
{
    /// <summary>Verifies empty defaults and owned realization render every Unicode item.</summary>
    [Fact]
    public void Items_WhenAssigned_RealizesOwnedControlsAndExactCells()
    {
        List<Label> realized = [];
        var control = new UiList()
        {
            ItemTemplate = item => Add(realized, new Label(item?.ToString() ?? "null")),
            Items = new object?[] { "One", "界", null },
        };
        new Engine().Layout(control, new Size(5, 3));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas);

        control.SelectionMode.ShouldBe(SelectionMode.Single);
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

    /// <summary>Verifies the List paints its surface and uses the checked state for the selected row.</summary>
    [Fact]
    public void Render_WhenStyledAndSelected_PaintsSurfaceAndSelectedRow()
    {
        var style = ThemeTestSupport.OverlayStyle<UiList>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(255), background: Color.Indexed(240))),
            (State.Selected, new ThemeOverlay(foreground: Color.Indexed(255), background: Color.Indexed(99))));
        var control = new UiList()
        {
            Items = new object?[] { "One", "Two" },
            SelectedIndex = 1,
            ScrollBars = ScrollBars.None,
            Style = style,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var size = new Size(8, 2);
        new Engine().Layout(control, size);
        using Frame frame = new(size);
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(234)));

        control.Render(frame.Canvas);

        frame.GetCell(new Point(7, 0)).Style.Background.ShouldBe(Color.Indexed(240));
        frame.GetCell(new Point(7, 1)).Style.Background.ShouldBe(Color.Indexed(99));
    }

    /// <summary>Verifies realized-item observers see selected state already propagated to content.</summary>
    [Fact]
    public void CommitSelection_WhenPropertyPublishes_ContentAlreadyResolvesSelectedState()
    {
        var style = ThemeTestSupport.OverlayStyle<Control>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(1))),
            (State.Selected, new ThemeOverlay(foreground: Color.Indexed(2))));
        var content = new Label("row") { Style = style };
        var item = new ListItem(0, content);
        content.Foreground.ShouldBe(Color.Indexed(1));
        var observed = false;
        item.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ListItem.IsSelected))
            {
                (content.Foreground == Color.Indexed(2)).ShouldBeTrue(
                    "Selected style must be visible before ListItem publishes its property.");
                observed = true;
            }
        };

        item.CommitSelection(true);

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies List exposes the canonical overflow policy and its actual composed scrollbar.</summary>
    [Fact]
    public void ScrollBars_WhenConfigured_ForwardCommonPolicyToComposedViewport()
    {
        var control = Create("one", "two", "three", "four", "five", "six");
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        control.ScrollBars = ScrollBars.Vertical;
        control.ShowScrollBars = ShowScrollBars.Always;
        control.ScrollBarChrome = ScrollBarChrome.Thin;
        control.ScrollBarFill = ScrollBarFill.Line;
        new Engine().Layout(control, new Size(6, 3));

        control.ScrollBars.ShouldBe(ScrollBars.Vertical);
        control.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        control.ScrollBarChrome.ShouldBe(ScrollBarChrome.Thin);
        control.ScrollBarFill.ShouldBe(ScrollBarFill.Line);
        var rail = control.HitTest(new Point(5, 0)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.Chrome.ShouldBe(ScrollBarChrome.Thin);
        rail.Fill.ShouldBe(ScrollBarFill.Line);

        control.ShowScrollBars = ShowScrollBars.Never;
        new Engine().Layout(control, new Size(6, 3));

        control.HitTest(new Point(5, 0)).ShouldNotBeOfType<ScrollBar>();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ScrollBars = (ScrollBars) 99);
    }

    /// <summary>Verifies List preserves a popup result already found by base registry traversal without searching twice.</summary>
    [Fact]
    public void HitTest_WhenRegistryFindsPopup_PreservesResultWithoutSecondTraversal()
    {
        PopupHitProbe? probe = null;
        var control = new UiList
        {
            ItemTemplate = _ => probe = new PopupHitProbe(),
            Items = ["item"],
        };
        new Engine().Layout(control, new Size(4, 1));

        var hit = control.HitTest(default);

        _ = probe.ShouldNotBeNull();
        hit.ShouldBeSameAs(probe);
        probe.PopupHitTestCalls.ShouldBe(1);
    }

    /// <summary>Verifies unchanged overflow policy assignments do not raise duplicate public notifications.</summary>
    [Fact]
    public void ShowScrollBars_WhenValueIsUnchanged_DoesNotRaisePropertyChanged()
    {
        var control = new UiList();
        var notifications = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UiList.ShowScrollBars))
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
        List<Label> previous = [];
        ItemTemplate valid = item => Add(previous, new Label((string) item!));
        var control = new UiList()
        {
            ItemTemplate = valid,
            Items = new object?[] { "A", "B" },
        };
        var duplicate = new Label("bad");

        _ = Should.Throw<ArgumentNullException>(() => control.Items = null!);
        _ = Should.Throw<ArgumentNullException>(() => control.ItemTemplate = null!);
        _ = Should.Throw<ArgumentException>(() => control.ItemTemplate = _ => null!);
        _ = Should.Throw<ArgumentException>(() => control.ItemTemplate = _ => duplicate);

        control.ItemTemplate.ShouldBeSameAs(valid);
        control.Items.ShouldBe(new object?[] { "A", "B" });
        previous.All(item => !item.IsDisposed && item.Parent is not null).ShouldBeTrue();
    }

    /// <summary>Verifies successful replacement disposes every detached realized wrapper and child.</summary>
    [Fact]
    public void Items_WhenReplaced_DisposesPreviousRealizationWithoutStateLeakage()
    {
        List<Label> realized = [];
        var control = new UiList()
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = new object?[] { "A", "B" },
        };
        Label[] previous = [.. realized];

        control.Items = new object?[] { "C" };

        previous.All(item => item.IsDisposed && item.Parent is null).ShouldBeTrue();
        control.Items.ShouldBe(new object?[] { "C" });
        _ = realized[^1].Parent.ShouldNotBeNull();
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

        control.SelectionMode = SelectionMode.Multiple;
        control.SetSelected(2, true).ShouldBeTrue();
        control.SelectedItems.ShouldBe(new object?[] { "B", "C" });
        control.SelectionMode = SelectionMode.Single;
        control.SelectedItems.ShouldBe(new object?[] { "B" });
        control.SelectionMode = SelectionMode.None;
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
            "changing:2:1",
        ]);
    }

    /// <summary>Verifies arrows skip unavailable items, Space selects, Enter invokes, and Home/End navigate.</summary>
    [Fact]
    public async Task Dispatch_WhenKeyboardNavigates_UsesStableRealizedOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        List<Label> realized = [];
        var control = new UiList()
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = new object?[] { "A", "B", "C" },
        };
        realized[1].IsEnabled = false;
        List<int> invoked = [];
        control.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(realized[0].Parent!).ShouldBeTrue();
            Key(realized[0].Parent!, Code.Down);
            focus.Focused.ShouldBeSameAs(realized[2].Parent);
            Space(realized[2].Parent!);
            control.SelectedIndex.ShouldBe(2);
            Key(realized[2].Parent!, Code.Enter);
            Key(realized[2].Parent!, Code.Home);
            focus.Focused.ShouldBeSameAs(realized[0].Parent);
            Key(realized[0].Parent!, Code.End);
            focus.Focused.ShouldBeSameAs(realized[2].Parent);
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
        control.SelectionMode = SelectionMode.Multiple;
        new Engine().Layout(control, new Size(4, 4));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using CaptureManager capture = new(control);
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
        List<Label> realized = [];
        var control = new UiList()
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
        };
        new Engine().Layout(control, new Size(8, 3));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            focus.Focus(realized[0].Parent!).ShouldBeTrue();

            for (var index = 0; index < 7; index++)
            {
                Key(focus.Focused!, Code.Down);
            }

            control.VerticalOffset.ShouldBeGreaterThan(0);
            control.ActiveIndex.ShouldBe(7);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies selected visual state reaches exact semantic item cells.</summary>
    [Fact]
    public void Render_WhenItemIsSelected_UsesSelectedStyleWithoutChangingTemplateContent()
    {
        var style = ThemeTestSupport.OverlayStyle<UiList>(
            (State.Selected, new ThemeOverlay(attributes: Attributes.Reverse)));
        var control = Create("界", "B");
        control.Style = style;
        control.SelectedIndex = 0;
        new Engine().Layout(control, new Size(3, 2));
        using Frame frame = new(new Size(3, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("界");
        (frame.GetCell(default).Style.Attributes & Attributes.Reverse).ShouldBe(Attributes.Reverse);
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies List.DisposeChildren releases the base Container's own owned
    /// bars, not only its private _chrome/_stack, so a caller externally
    /// arming the outer List's inherited AutoScroll does not leak the
    /// resulting ScrollBar chrome on dispose.
    /// </summary>
    [Fact]
    public void Dispose_WhenBaseAutoScrollIsArmedExternally_DisposesTheOwnedBaseBars()
    {
        var list = new UiList() { AutoScroll = true };
        var field = typeof(Container).GetField(
            "_bars",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var bars = (Children) field.GetValue(list)!;
        bars.Count.ShouldBe(2);
        var horizontal = bars[0].ShouldBeOfType<ScrollBar>();
        var vertical = bars[1].ShouldBeOfType<ScrollBar>();

        list.Dispose();

        horizontal.IsDisposed.ShouldBeTrue();
        vertical.IsDisposed.ShouldBeTrue();
    }

    private static UiList Create(params object?[] items) => new() { Items = items };

    private static Label Add(List<Label> controls, Label control)
    {
        controls.Add(control);
        return control;
    }

    private static string Join(ReadOnlyMemory<int> values) => string.Join(',', values.ToArray());

    private static void Key(Control target, Code code, Rune? character = null) =>
        Router.Route(
            target,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    private static void Space(Control target)
    {
        Key(target, Code.Character, new Rune(' '));
        Router.Route(
            target,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(' '),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Release)));
    }

    private static void Click(CaptureManager capture, Point point, Modifiers modifiers)
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
