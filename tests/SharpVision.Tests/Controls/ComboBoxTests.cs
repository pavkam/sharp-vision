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

    /// <summary>Verifies an open drop-down uses an opaque inherited surface inside a visible frame.</summary>
    [Fact]
    public void Render_WhenOpen_UsesOpaqueFramedDropDownSurface()
    {
        var theme = new Theme();
        var controlStyle = ThemeTestSupport.CreateControlStyle();
        controlStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(255));
        controlStyle.Set(Control.BackgroundProperty, State.Normal, Color.Indexed(42));
        theme.SetStyle(controlStyle);
        var box = new ComboBox()
        {
            Height = Length.Cells(1),
            Items = ["Small", "Large"],
            IsOpen = true,
        };
        ThemeTestSupport.ApplyTheme(box, theme);
        var size = new Size(12, 6);
        new Engine().Layout(box, size);
        using Frame frame = new(size);
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune(' '),
            new TerminalStyle(Color.Default, Color.Indexed(7)));

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<List>();
        list.DesiredSize.Height.ShouldBeGreaterThan(0);
        popup.SurfaceBounds.Height.ShouldBeGreaterThan(2);
        frame.GetCell(new Point(popup.SurfaceBounds.X + 1, popup.SurfaceBounds.Y + 1)).Style.Background
            .ShouldBe(Color.Indexed(42));
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

    /// <summary>Verifies a selected popup choice fills every trailing cell in its realized row.</summary>
    [Fact]
    public void Render_WhenOpenWithSelectedChoice_FillsTheCompleteListRow()
    {
        var theme = new Theme();
        var controlStyle = ThemeTestSupport.CreateControlStyle();
        controlStyle.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(255));
        controlStyle.Set(Control.BackgroundProperty, State.Normal, Color.Indexed(240));
        controlStyle.Set(Control.ForegroundProperty, State.Selected, Color.Indexed(255));
        controlStyle.Set(Control.BackgroundProperty, State.Selected, Color.Indexed(99));
        theme.SetStyle(controlStyle);
        var box = new ComboBox()
        {
            Width = Length.Cells(20),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 0,
            IsOpen = true,
        };
        ThemeTestSupport.ApplyTheme(box, theme);
        var size = new Size(24, 8);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var list = OwnedTree.Find<List>(box).ShouldNotBeNull();
        frame.GetCell(new Point(list.Bounds.Right - 1, list.Bounds.Y)).Style.Background.ShouldBe(Color.Indexed(99));
        frame.GetCell(new Point(list.Bounds.Right - 1, list.Bounds.Y + 1)).Style.Background.ShouldBe(Color.Indexed(240));
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

    /// <summary>Verifies Enter opens the list below the field and transfers focus for directional selection.</summary>
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

            Router.Route(box, Events.Key, Key(Code.Enter));

            box.IsOpen.ShouldBeTrue();
            var list = focus.Focused.ShouldBeOfType<List>();
            Router.Route(list, Events.Key, Key(Code.Down));
            var selectedItem = focus.Focused.ShouldBeOfType<ListItem>();
            Router.Route(selectedItem, Events.Key, Key(Code.Enter));

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
            Router.Route(box, Events.Key, Key(Code.Enter));

            Router.Route(focus.Focused!, Events.Key, Key(Code.Escape));

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
            using CaptureManager capture = new(box);
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

    /// <summary>Verifies Tab stays inside the open popup by cycling through ListItems.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressedInOpenPopup_CyclesThroughListItemsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var box = new ComboBox() { Items = ["A", "B", "C"], SelectedIndex = 0 };
            var outside = new ProbeControl() { CanFocus = true };
            root.Children.Add(box);
            root.Children.Add(outside);
            new Engine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(box).ShouldBeTrue();

            Router.Route(box, Events.Key, Key(Code.Enter));
            box.IsOpen.ShouldBeTrue();
            var list = focus.Focused.ShouldBeOfType<List>();

            Router.Route(list, Events.Key, Tab());
            focus.Focused.ShouldBeOfType<ListItem>();

            Router.Route(focus.Focused!, Events.Key, Tab());
            focus.Focused.ShouldBeOfType<ListItem>();

            Router.Route(focus.Focused!, Events.Key, Tab());
            focus.Focused.ShouldBeOfType<ListItem>();

            focus.Focused.ShouldNotBeSameAs(outside);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab does not escape an open popup to reach sibling controls.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressedInOpenPopup_DoesNotEscapeToSiblingControlsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var box = new ComboBox() { Items = ["X", "Y"], SelectedIndex = 0 };
            var sibling = new ProbeControl() { CanFocus = true };
            root.Children.Add(box);
            root.Children.Add(sibling);
            new Engine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(box).ShouldBeTrue();
            Router.Route(box, Events.Key, Key(Code.Enter));
            box.IsOpen.ShouldBeTrue();
            var list = focus.Focused.ShouldBeOfType<List>();

            Router.Route(list, Events.Key, Tab());
            var first = focus.Focused;
            Router.Route(focus.Focused!, Events.Key, Tab());
            var second = focus.Focused;
            Router.Route(focus.Focused!, Events.Key, Tab());

            focus.Focused.ShouldBeSameAs(first);
            first.ShouldNotBeSameAs(sibling);
            second.ShouldNotBeSameAs(sibling);
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
