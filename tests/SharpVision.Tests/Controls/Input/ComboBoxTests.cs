// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies popup-style combo box geometry, keyboard opening, focus, and committed selection.</summary>
public sealed class ComboBoxTests
{
    /// <summary>Verifies a combo field is discoverable through light intrinsic chrome by default.</summary>
    [Fact]
    public void Properties_WhenConstructed_UsesLightFieldBorder()
    {
        // Arrange
        var box = new ComboBox();

        // Act

        // Assert
        box.ActualBorder.Sides.ShouldBe(BorderSide.All);
        box.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies a closed box frames only the selected label while its list remains unavailable.</summary>
    [Fact]
    public void Render_WhenClosed_ShowsSelectedLabelInsideLightFrameWithoutDropDown()
    {
        var box = new ComboBox
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
            Items = ["Small", "Large"],
            SelectedIndex = 1
        };
        var size = new Size(12, 6);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("┏");
        FrameOracle.Get(frame, new Point(11, 0)).ShouldBe("┓");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("┗");
        FrameOracle.Get(frame, new Point(11, 2)).ShouldBe("┛");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("L");
        FrameOracle.Get(frame, new Point(10, 1)).ShouldBe("▼");
        FrameOracle.Get(frame, new Point(0, 3)).ShouldBeEmpty();
        box.HitTest(new Point(0, 3)).ShouldBeNull();
    }

    /// <summary>Verifies a disabled field continues to display its committed selected value.</summary>
    [Fact]
    public void Render_WhenDisabled_PreservesSelectedLabel()
    {
        var box = new ComboBox
        {
            Width = Length.Cells(24),
            Items = ["Locked choice"],
            SelectedIndex = 0,
            IsEnabled = false
        };
        box.SetTheme(Themes.Dark);
        var size = new Size(24, 3);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        box.SelectedIndex.ShouldBe(0);
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("L");
        frame.GetCell(new Point(1, 1)).Style.Foreground.ShouldNotBe(
            frame.GetCell(new Point(1, 1)).Style.Background);
    }

    /// <summary>Verifies the field owns only a Popup and the Popup exclusively owns the private ListView.</summary>
    [Fact]
    public void Ownership_WhenConstructed_UsesOnePrivatePopupChain()
    {
        var box = new ComboBox();

        box.OwnedControlCount.ShouldBe(1);
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<ListView>(popup).ShouldNotBeNull();
        popup.Parent.ShouldBeSameAs(box);
        list.Parent.ShouldBeSameAs(popup);
        popup.Content.ShouldBeSameAs(list);
        list.ActualBorder.Sides.ShouldBe(BorderSide.None);
    }

    /// <summary>Verifies drop-down rail ownership publishes exact local and resolved-style notifications.</summary>
    [Fact]
    public void ScrollBarStyle_WhenOwnershipChanges_PublishesLocalAndActualNotifications()
    {
        var box = new ComboBox();
        List<string?> notifications = [];
        box.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(ComboBox.ScrollBarStyle) or nameof(ComboBox.ActualScrollBarStyle))
            {
                notifications.Add(eventArgs.PropertyName);
            }
        };
        var thinTheme = ScrollBarTheme(ScrollBarStyle.ThinLine);
        var fullTheme = ScrollBarTheme(ScrollBarStyle.FullLine);

        box.SetTheme(thinTheme);
        notifications.ShouldBe([nameof(ComboBox.ActualScrollBarStyle)]);
        notifications.Clear();

        box.ScrollBarStyle = ScrollBarStyle.FullBlock;
        box.ScrollBarStyle = null;
        notifications.ShouldBe([
            nameof(ComboBox.ScrollBarStyle),
            nameof(ComboBox.ActualScrollBarStyle),
            nameof(ComboBox.ScrollBarStyle),
            nameof(ComboBox.ActualScrollBarStyle)
        ]);
        notifications.Clear();

        box.SetTheme(fullTheme);
        notifications.ShouldBe([nameof(ComboBox.ActualScrollBarStyle)]);
    }

    /// <summary>Verifies ComboBox publishes its committed index before forwarding selection change.</summary>
    [Fact]
    public void SelectedIndex_WhenChanged_PublishesPropertyBeforeSelectionEvent()
    {
        var box = new ComboBox { Items = ["Small", "Large"], SelectedIndex = 0 };
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
        var box = new ComboBox { DropDownHeight = 4, Items = ["1Row", "3-D", "Standard"], IsOpen = true };
        var size = new Size(24, 12);
        new Engine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<ListView>();
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("1");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y)).ShouldBe("R");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 3, list.Bounds.Y)).ShouldBe("w");
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y + 1)).ShouldBe("3");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y + 1)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y + 1)).ShouldBe("D");
    }

    /// <summary>Verifies the below drop-down starts with content at the field edge and omits its top frame.</summary>
    [Fact]
    public void Render_WhenOpenBelow_ConnectsListWithoutTopFrame()
    {
        // Arrange
        var box = new ComboBox
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 2,
            Items = ["One", "Two"],
            SelectedIndex = 0,
            IsOpen = true
        };
        var root = new Overlay { Children = { box } };
        var size = new Size(12, 7);
        new Engine().Layout(root, size);
        using Frame frame = new(size);

        // Act
        root.Render(frame.Canvas);

        // Assert
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<ListView>(popup).ShouldNotBeNull();
        popup.SurfaceBounds.ShouldBe(new Rect(0, box.Bounds.Bottom, 10, 3));
        list.Bounds.ShouldBe(new Rect(1, box.Bounds.Bottom, 8, 2));
        FrameOracle.Get(frame, new Point(0, box.Bounds.Bottom)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(1, box.Bounds.Bottom)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(9, box.Bounds.Bottom)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(0, popup.SurfaceBounds.Bottom - 1)).ShouldBe("╰");
        FrameOracle.Get(frame, new Point(9, popup.SurfaceBounds.Bottom - 1)).ShouldBe("╯");
    }

    /// <summary>Verifies an above fallback moves the open seam to the edge adjoining the field.</summary>
    [Fact]
    public void Render_WhenDropDownFlipsAbove_ConnectsListWithoutBottomFrame()
    {
        // Arrange
        var box = new ComboBox
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            VerticalAlignment = VerticalAlignment.Bottom,
            DropDownHeight = 2,
            Items = ["One", "Two"],
            SelectedIndex = 0,
            IsOpen = true
        };
        var root = new Overlay { Children = { box } };
        var size = new Size(12, 6);
        new Engine().Layout(root, size);
        using Frame frame = new(size);

        // Act
        root.Render(frame.Canvas);

        // Assert
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<ListView>(popup).ShouldNotBeNull();
        popup.SurfaceBounds.ShouldBe(new Rect(0, 0, 10, 3));
        list.Bounds.ShouldBe(new Rect(1, 1, 8, 2));
        list.Bounds.Bottom.ShouldBe(box.Bounds.Y);
        FrameOracle.Get(frame, new Point(0, box.Bounds.Y - 1)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(1, box.Bounds.Y - 1)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(9, box.Bounds.Y - 1)).ShouldBe("│");
    }

    /// <summary>Verifies long popup choices expose the same configured canonical scrollbar as a standalone ListView.</summary>
    [Fact]
    public void ScrollBars_WhenConfigured_ForwardPolicyToOpenDropDown()
    {
        var box = new ComboBox
        {
            Items = ["one", "two", "three", "four", "five", "six"],
            DropDownHeight = 3,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            IsOpen = true
        };
        new Engine().Layout(box, new Size(12, 6));

        box.ScrollBars.ShouldBe(ScrollBars.Vertical);
        box.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        box.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        var list = OwnedTree.Find<ListView>(box).ShouldNotBeNull();
        var rail = list.HitTest(new Point(list.Bounds.Right - 1, list.Bounds.Y)).ShouldBeOfType<ScrollBar>();
        rail.Orientation.ShouldBe(Orientation.Vertical);
        rail.ActualStyle.ShouldBe(ScrollBarStyle.ThinLine);
    }

    /// <summary>Verifies Enter opens the list while the composite owner retains focus for directional selection.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterOpens_TransfersFocusAndInvokesSelectedListItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox { Items = ["Small", "Large"], SelectedIndex = 0 };
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

    /// <summary>Verifies closing a drop-down returns focus to its visible field instead of its hidden ListView.</summary>
    [Fact]
    public async Task Dispatch_WhenEscapeClosesDropDown_ReturnsFocusToComboBoxAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox { Items = ["Small", "Large"], SelectedIndex = 0 };
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

    /// <summary>Verifies pointer input reaches a ListView item through the framed popup and returns focus after committing the choice.</summary>
    [Fact]
    public async Task Dispatch_WhenPopupItemIsClicked_CommitsChoiceClosesAndRestoresFocusAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox
            {
                Height = Length.Cells(1),
                Items = ["Small", "Large"],
                SelectedIndex = 1,
                DropDownHeight = 2
            };
            var size = new Size(12, 6);
            new Engine().Layout(box, size);
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            using PointerManager capture = new(box);
            focus.Focus(box).ShouldBeTrue();
            box.IsOpen = true;
            new Engine().Layout(box, size);
            var list = OwnedTree.Find<ListView>(box).ShouldNotBeNull();

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
            var box = new ComboBox { Items = ["A", "B", "C"], SelectedIndex = 0 };
            var outside = new ProbeControl { Focusable = true };
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
            var box = new ComboBox { Items = ["X", "Y"], SelectedIndex = 0 };
            var sibling = new ProbeControl { Focusable = true };
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

    /// <summary>Verifies SelectedItem returns the correct item when an index is selected.</summary>
    [Fact]
    public void SelectedItem_ReturnsCorrectItem()
    {
        var comboBox = new ComboBox { Items = new object?[] { "A", "B", "C" }, SelectedIndex = 1 };
        comboBox.SelectedItem.ShouldBe("B");
    }

    /// <summary>Verifies SelectedItem returns null when no selection is active.</summary>
    [Fact]
    public void SelectedItem_WhenNoSelection_ReturnsNull()
    {
        var comboBox = new ComboBox { Items = new object?[] { "A" }, SelectedIndex = -1 };
        comboBox.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies the basic empty face, keyboard clearing, and type-ahead selection contract.</summary>
    [Fact]
    public void Input_WhenEmptyAndTypedOrCleared_UsesPlaceholderAndSelectsByPrefix()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            Placeholder = "Choose",
            SelectedIndex = -1
        };

        combo.SelectedIndex = 1;
        var cleared = Router.Route(combo, Events.Key, Key(Code.Delete));
        combo.IsOpen = true;
        var typed = Router.Route(combo, Events.Key, CharacterKey('g'));

        cleared.Handled.ShouldBeTrue();
        typed.Handled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(2);
        combo.SelectedItem.ShouldBe("Gamma");
        combo.Placeholder.ShouldBe("Choose");
    }

    /// <summary>Verifies DropDownOpened fires when the drop-down opens and DropDownClosed fires when it closes.</summary>
    [Fact]
    public void DropDownOpened_WhenDropDownOpens_FiresEvent()
    {
        // Arrange
        var box = new ComboBox { Items = ["A", "B"], SelectedIndex = 0 };
        var opened = 0;
        var closed = 0;
        box.DropDownOpened += (_, _) => opened++;
        box.DropDownClosed += (_, _) => closed++;

        // Act
        box.IsOpen = true;

        // Assert
        opened.ShouldBe(1);
        closed.ShouldBe(0);

        // Act
        box.IsOpen = false;

        // Assert
        opened.ShouldBe(1);
        closed.ShouldBe(1);
    }

    private static KeyEventArgs Tab() => new(new Stroke(
        Code.Tab,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static Theme ScrollBarTheme(ScrollBarStyle _)
    {
        var theme = new Theme();

        theme.Freeze();
        return theme;
    }

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static KeyEventArgs CharacterKey(char character) => new(new Stroke(
        Code.Character,
        new Rune(character),
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
