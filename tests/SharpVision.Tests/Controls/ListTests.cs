using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using KeyAction = SharpVision.Terminal.Input.Action;
using Label = SharpVision.Controls.Text;
using UiList = SharpVision.Controls.List;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies realized List ownership, selection, input, scrolling, and rendering.</summary>
public sealed class ListTests
{
    /// <summary>Verifies empty defaults and owned realization render every Unicode item.</summary>
    [Fact]
    public void Items_WhenAssigned_RealizesOwnedControlsAndExactCells()
    {
        var realized = new List<Label>();
        var control = new UiList
        {
            ItemTemplate = item => Add(realized, new Label(item?.ToString() ?? "null")),
            Items = new object?[] { "One", "界", null },
        };
        new Engine().Layout(control, new Size(5, 3));
        using var frame = new Frame(new Size(5, 3));

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

    /// <summary>Verifies failed item or template replacement leaves the complete old tree untouched.</summary>
    [Fact]
    public void ItemTemplate_WhenCandidateIsInvalid_PreservesItemsTemplateAndOwnership()
    {
        var previous = new List<Label>();
        ItemTemplate valid = item => Add(previous, new Label((string) item!));
        var control = new UiList
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
        var realized = new List<Label>();
        var control = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = new object?[] { "A", "B" },
        };
        var previous = realized.ToArray();

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
        var order = new List<string>();
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
        var realized = new List<Label>();
        var control = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = new object?[] { "A", "B", "C" },
        };
        realized[1].IsEnabled = false;
        var invoked = new List<int>();
        control.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using var focus = new FocusManager(control);
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
            using var capture = new CaptureManager(control);
            Click(capture, new Point(0, 1), Modifiers.Control);
            Click(capture, new Point(0, 3), Modifiers.Shift);
        }, TestContext.Current.CancellationToken);

        control.SelectedItems.ShouldBe(new object?[] { "B", "C", "D" });
    }

    /// <summary>Verifies active items are minimally brought through the composed ScrollView on resize.</summary>
    [Fact]
    public async Task Dispatch_WhenActiveItemMovesBeyondViewport_BringsItIntoViewAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var realized = new List<Label>();
        var control = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
        };
        new Engine().Layout(control, new Size(8, 3));

        await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            using var focus = new FocusManager(control);
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
    public void Render_WhenItemIsSelected_UsesCheckedStyleWithoutChangingTemplateContent()
    {
        var style = new SharpVision.Styling.Style();
        style.Set(
            SharpVision.Styling.State.Checked,
            new SharpVision.Styling.Appearance(attributes: Attributes.Reverse));
        var control = Create("界", "B");
        control.Style = style;
        control.SelectedIndex = 0;
        new Engine().Layout(control, new Size(3, 2));
        using var frame = new Frame(new Size(3, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("界");
        (frame.GetCell(default).Style.Attributes & Attributes.Reverse).ShouldBe(Attributes.Reverse);
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
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
