// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies popup-style combo box geometry, keyboard opening, focus, and committed selection.</summary>
public sealed class ComboBoxTests
{
    /// <summary>Verifies a closed box presents only the selected label while its list is neither rendered nor hit-testable.</summary>
    [Fact]
    public void Render_WhenClosed_ShowsSelectedLabelWithoutDropDown()
    {
        var box = new ComboBox()
        {
            Height = Length.Cells(1),
            Items = ["Small", "Large"],
            SelectedIndex = 1,
        };
        var size = new Size(12, 6);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("L");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
        box.HitTest(new Point(0, 1)).ShouldBeNull();
    }

    /// <summary>Verifies the field owns only a Popup and the Popup exclusively owns the private List.</summary>
    [Fact]
    public void Ownership_WhenConstructed_UsesOnePrivatePopupChain()
    {
        var box = new ComboBox();

        box.OwnedControlCount.ShouldBe(1);
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<List>(popup).ShouldNotBeNull();
        popup.Parent.ShouldBeSameAs(box);
        list.Parent.ShouldBeSameAs(popup);
        popup.Content.ShouldBeSameAs(list);
    }

    /// <summary>Verifies ComboBox publishes its committed index before forwarding selection change.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_PublishesPropertyBeforeSelectionEvent()
    {
        var box = new ComboBox() { Items = ["Small", "Large"], SelectedIndex = 0 };
        List<string> order = [];
        box.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ComboBox.SelectedIndex))
            {
                box.SelectedIndex.ShouldBe(1);
                order.Add("property");
            }
        };
        box.SelectionChanged += (_, eventArgs) =>
        {
            box.SelectedIndex.ShouldBe(1);
            eventArgs.AddedIndexes.ToArray().ShouldBe([1]);
            eventArgs.RemovedIndexes.ToArray().ShouldBe([0]);
            order.Add("event");
        };

        box.SelectedIndex = 1;

        order.ShouldBe(["property", "event"]);
    }

    /// <summary>Verifies the framed popup preserves the first visible choices instead of exposing the underlying page.</summary>
    [Fact]
    public void Render_WhenOpen_RendersChoicesInsideFramedSurface()
    {
        var box = new ComboBox()
        {
            DropDownHeight = 4,
            Items = ["1Row", "3-D", "Standard"],
            IsOpen = true,
        };
        var size = new Size(24, 12);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<List>();
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("1");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y)).ShouldBe("R");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 3, list.Bounds.Y)).ShouldBe("w");
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y + 1)).ShouldBe("3");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y + 1)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y + 1)).ShouldBe("D");
    }

    /// <summary>Verifies long popup choices expose the same configured canonical scrollbar as a standalone List.</summary>
    [Fact]
    public void ScrollBars_WhenConfigured_ForwardPolicyToOpenDropDown()
    {
        var box = new ComboBox()
        {
            Items = ["one", "two", "three", "four", "five", "six"],
            DropDownHeight = 3,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            IsOpen = true,
        };
        new Engine().Layout(box, new Size(12, 6));

        box.ScrollBars.ShouldBe(ScrollBars.Vertical);
        box.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        box.ScrollBarChrome.ShouldBe(ScrollBarChrome.Thin);
        box.ScrollBarFill.ShouldBe(ScrollBarFill.Line);
        var list = OwnedTree.Find<List>(box).ShouldNotBeNull();
        var rail = list.HitTest(new Point(list.Bounds.Right - 1, list.Bounds.Y)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.Chrome.ShouldBe(ScrollBarChrome.Thin);
        rail.Fill.ShouldBe(ScrollBarFill.Line);
    }

    /// <summary>Verifies Enter opens the list while the composite owner retains focus for directional selection.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterOpens_TransfersFocusAndInvokesSelectedListItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox() { Items = ["Small", "Large"], SelectedIndex = 0 };
            new Engine().Layout(box, new Size(12, 6));
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            focus.Focus(box).ShouldBeTrue();

            _ = Router.Route(box, Events.Key, Key(Code.Enter));

            box.IsOpen.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(box);
            _ = Router.Route(box, Events.Key, Key(Code.Down));
            _ = Router.Route(box, Events.Key, Key(Code.Enter));

            box.SelectedIndex.ShouldBe(1);
            box.IsOpen.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(box);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies closing a drop-down returns focus to its visible field instead of its hidden List.</summary>
    [Fact]
    public async Task Dispatch_WhenEscapeClosesDropDown_ReturnsFocusToComboBoxAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox() { Items = ["Small", "Large"], SelectedIndex = 0 };
            new Engine().Layout(box, new Size(12, 6));
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            focus.Focus(box).ShouldBeTrue();
            _ = Router.Route(box, Events.Key, Key(Code.Enter));

            _ = Router.Route(box, Events.Key, Key(Code.Escape));

            box.IsOpen.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(box);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies pointer input reaches a List item through the framed popup and returns focus after committing the choice.</summary>
    [Fact]
    public async Task Dispatch_WhenPopupItemIsClicked_CommitsChoiceClosesAndRestoresFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox()
            {
                Height = Length.Cells(1),
                Items = ["Small", "Large"],
                SelectedIndex = 1,
                DropDownHeight = 2,
            };
            var size = new Size(12, 6);
            new Engine().Layout(box, size);
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            using PointerManager capture = new(box);
            focus.Focus(box).ShouldBeTrue();
            box.IsOpen = true;
            new Engine().Layout(box, size);
            var list = OwnedTree.Find<List>(box).ShouldNotBeNull();

            _ = capture.Dispatch(Pointer(new Point(list.Bounds.X + 1, list.Bounds.Y), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(list.Bounds.X + 1, list.Bounds.Y), PointerAction.Release));

            box.SelectedIndex.ShouldBe(0);
            box.IsOpen.ShouldBeFalse();
            focus.Focused.ShouldBeSameAs(box);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab closes the transient popup and leaves exactly one deferred traversal command.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressedInOpenPopup_CyclesThroughListItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var box = new ComboBox() { Items = ["A", "B", "C"], SelectedIndex = 0 };
            var outside = new ProbeControl() { Focusable = true };
            root.Children.Add(box);
            root.Children.Add(outside);
            new Engine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(box).ShouldBeTrue();

            _ = Router.Route(box, Events.Key, Key(Code.Enter));
            box.IsOpen.ShouldBeTrue();
            var result = Router.Route(box, Events.Key, Tab());

            box.IsOpen.ShouldBeFalse();
            result.Command.ShouldBe(PostRouteCommand.TabNext);
            result.Anchor.ShouldBeSameAs(box);
            focus.Focused.ShouldBeSameAs(box);
            focus.Focused.ShouldNotBeSameAs(outside);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the popup's private list and rows never enter traversal.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressedInOpenPopup_DoesNotEscapeToSiblingControlsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var box = new ComboBox() { Items = ["X", "Y"], SelectedIndex = 0 };
            var sibling = new ProbeControl() { Focusable = true };
            root.Children.Add(box);
            root.Children.Add(sibling);
            new Engine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(box).ShouldBeTrue();
            _ = Router.Route(box, Events.Key, Key(Code.Enter));
            box.IsOpen.ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(sibling);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(box);
        }, TestContext.Current.CancellationToken);
    }

    private static KeyEventArgs Tab() => new(new Stroke(
        Code.Tab,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);
}
