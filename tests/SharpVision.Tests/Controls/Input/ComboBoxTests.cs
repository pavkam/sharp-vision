// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies popup-style combo box geometry, keyboard opening, focus, and committed selection.</summary>
public sealed class ComboBoxTests
{
    /// <summary>Verifies a combo field is discoverable through light intrinsic chrome by default.</summary>
    [ComponentUnitEvidence(typeof(ComboBox))]
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
        new LayoutEngine().Layout(box, size);
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
    [ComponentUnitEvidence(typeof(ComboBox), ComponentBehavior.Disabled)]
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
        box.SetTheme(ThemeCatalog.Dark);
        var size = new Size(24, 3);
        new LayoutEngine().Layout(box, size);
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
        var list = OwnedTree.Find<UiListView>(popup).ShouldNotBeNull();
        popup.Parent.ShouldBeSameAs(box);
        list.Parent.ShouldBeSameAs(popup);
        popup.Content.ShouldBeSameAs(list);
        list.ActualBorder.Sides.ShouldBe(BorderSide.None);
    }

    /// <summary>Verifies drop-down rail local mechanics publish exact resolved-style notifications.</summary>
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
        box.ScrollBarStyle = ScrollBarStyle.ThinLine;
        box.ScrollBarStyle = null;
        notifications.ShouldBe([
            nameof(ComboBox.ScrollBarStyle),
            nameof(ComboBox.ActualScrollBarStyle),
            nameof(ComboBox.ScrollBarStyle),
            nameof(ComboBox.ActualScrollBarStyle)
        ]);
        notifications.Clear();

        // The framework's code-owned fallback (used while no theme is attached) resolves against
        // ThemeCatalog.Dark, not ThemeCatalog.White (see StyleDefinitions.Control), so switching to a
        // genuinely different theme is expected to change the resolved ActualScrollBarStyle and
        // publish exactly the one notification a value change produces - matching
        // ActualScrollBarStyleThemeTests, which explicitly asserts this same divergence.
        box.SetTheme(ThemeCatalog.White);
        notifications.ShouldBe([nameof(ComboBox.ActualScrollBarStyle)]);
    }

    /// <summary>Verifies PopupChrome applies border and shadow to the owned drop-down popup without
    /// leaking it.</summary>
    [Fact]
    public void PopupStyle_WhenSet_AppliesToOwnedPopup()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var shadow = AppearanceTestValues.Shadow(visible: true);
        var box = new ComboBox();
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();

        box.PopupChrome = new PopupChrome { Border = border, Shadow = shadow };

        popup.Border.ShouldBe(border);
        popup.Shadow.ShouldBe(shadow);
    }

    /// <summary>Verifies a PopupChrome component left null keeps that part on the PopupChrome
    /// ownership while the other component is still locally applied.</summary>
    [Fact]
    public void PopupStyle_WhenOnlyBorderIsSet_LeavesShadowOnThemeAppearance()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var box = new ComboBox();
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var themeRoleShadow = popup.Shadow;

        box.PopupChrome = new PopupChrome { Border = border };

        popup.Border.ShouldBe(border);
        popup.Shadow.ShouldBe(themeRoleShadow);
    }

    /// <summary>Verifies ResetPopupChrome returns the owned popup to its PopupChrome appearance.</summary>
    [Fact]
    public void ResetPopupStyle_WhenPopupHasLocalOverride_ReturnsToThemeAppearance()
    {
        var box = new ComboBox();
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var themeRoleBorder = popup.Border;
        box.PopupChrome = new PopupChrome
        {
            Border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
        };

        box.ResetPopupChrome();

        box.PopupChrome.ShouldBe(default);
        popup.Border.ShouldBe(themeRoleBorder);
    }

    /// <summary>Verifies RowHeight forwards to the owned drop-down list without leaking it.</summary>
    [Fact]
    public void RowHeight_WhenSet_AppliesToOwnedListView()
    {
        var box = new ComboBox();
        var list = OwnedTree.Find<UiListView>(box).ShouldNotBeNull();

        box.RowHeight = 3;

        list.RowHeight.ShouldBe(3);
        box.RowHeight.ShouldBe(3);
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

    /// <summary>Verifies assigning Items auto-selects index 0 through the normal selection-change
    /// path, raising SelectionChanged and PropertyChanged(SelectedIndex) like any other change.</summary>
    [Fact]
    public void Items_WhenAssignedWithNoPriorSelection_PublishesAutoSelectedIndexZero()
    {
        var box = new ComboBox();
        var propertyChanges = new List<string>();
        var selectionChanges = 0;
        box.PropertyChanged += (_, eventArgs) => propertyChanges.Add(eventArgs.PropertyName!);
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.Items = ["a", "b", "c"];

        box.SelectedIndex.ShouldBe(0);
        box.SelectedItem.ShouldBe("a");
        propertyChanges.ShouldContain(nameof(ComboBox.SelectedIndex));
        selectionChanges.ShouldBe(1);
    }

    /// <summary>Verifies setting SelectedIndex while the drop-down is open to a genuinely
    /// available item publishes exactly one SelectionChanged, not a duplicate from both the
    /// internal list's own notification and an unconditional explicit publish.</summary>
    [Fact]
    public void SelectedIndex_WhenSetWhileOpenToAvailableItem_FiresSelectionChangedExactlyOnce()
    {
        var box = new ComboBox { Items = ["One", "Two", "Three"], DropDownHeight = 4, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.SelectedIndex = 1;

        box.SelectedIndex.ShouldBe(1);
        box.SelectedItem.ShouldBe("Two");
        selectionChanges.ShouldBe(1);
    }

    /// <summary>Verifies a SelectedIndex assignment silently vetoed by the internal list's own
    /// SelectionChanging handler (while the drop-down is open, so the veto is genuine) is
    /// rolled back rather than reported through SelectionChanged as if it had taken effect.</summary>
    [Fact]
    public void SelectedIndex_WhenListVetoesWhileOpen_RollsBackWithoutPublishing()
    {
        var box = new ComboBox { Items = ["One", "Two", "Three"], DropDownHeight = 4, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<UiListView>();
        list.SelectionChanging += (_, eventArgs) => eventArgs.Cancel = true;
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.SelectedIndex = 1;

        box.SelectedIndex.ShouldBe(0);
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies the framed popup preserves the first visible choices instead of exposing the underlying page.</summary>
    [Fact]
    public void Render_WhenOpen_RendersChoicesInsideFramedSurface()
    {
        var box = new ComboBox { DropDownHeight = 4, Items = ["1Row", "3-D", "Standard"], IsOpen = true };
        var size = new Size(24, 12);
        new LayoutEngine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<UiListView>();
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("1");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y)).ShouldBe("R");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 3, list.Bounds.Y)).ShouldBe("w");
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y + 1)).ShouldBe("3");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 1, list.Bounds.Y + 1)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y + 1)).ShouldBe("D");
    }

    /// <summary>Verifies the drop-down shrinks to the actual item count instead of always
    /// reserving the full configured DropDownHeight, which would otherwise show trailing empty
    /// rows below a short item list (MeasureOverride only passes DropDownHeight as an upper
    /// measure bound; the list's own auto-sized DesiredSize height is what actually gets
    /// arranged).</summary>
    [Fact]
    public void Render_WhenOpenWithFewerItemsThanDropDownHeight_ShrinksToItemCount()
    {
        var box = new ComboBox { DropDownHeight = 8, Items = ["One", "Two"], IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 20));

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<UiListView>();

        list.Bounds.Height.ShouldBe(2);
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
        new LayoutEngine().Layout(root, size);
        using Frame frame = new(size);

        // Act
        root.Render(frame.Canvas);

        // Assert
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(popup).ShouldNotBeNull();
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
        new LayoutEngine().Layout(root, size);
        using Frame frame = new(size);

        // Act
        root.Render(frame.Canvas);

        // Assert
        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(popup).ShouldNotBeNull();
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
        new LayoutEngine().Layout(box, new Size(12, 6));

        box.ScrollBars.ShouldBe(ScrollBars.Vertical);
        box.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        box.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        var list = OwnedTree.Find<UiListView>(box).ShouldNotBeNull();
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
            new LayoutEngine().Layout(box, new Size(12, 6));
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

    /// <summary>Verifies an incidental Control modifier on Enter does not commit the highlighted
    /// row - the drop-down stays open and the selection is unchanged.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifier_DoesNotCommitSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox { Items = ["Small", "Large"], SelectedIndex = 0 };
            new LayoutEngine().Layout(box, new Size(12, 6));
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            focus.Focus(box).ShouldBeTrue();

            _ = Router.Route(box, Events.Key, Key(Code.Enter));
            _ = Router.Route(box, Events.Key, Key(Code.Down));
            _ = Router.Route(box, Events.Key, Key(Code.Enter, Modifiers.Control));

            box.SelectedIndex.ShouldBe(0);
            box.IsOpen.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still commits the highlighted row.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifier_StillCommitsSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var box = new ComboBox { Items = ["Small", "Large"], SelectedIndex = 0 };
            new LayoutEngine().Layout(box, new Size(12, 6));
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            focus.Focus(box).ShouldBeTrue();

            _ = Router.Route(box, Events.Key, Key(Code.Enter));
            _ = Router.Route(box, Events.Key, Key(Code.Down));
            _ = Router.Route(box, Events.Key, Key(Code.Enter, Modifiers.Shift));

            box.SelectedIndex.ShouldBe(1);
            box.IsOpen.ShouldBeFalse();
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
            new LayoutEngine().Layout(box, new Size(12, 6));
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
            new LayoutEngine().Layout(box, size);
            box.Attach(dispatcher);
            using FocusManager focus = new(box);
            using PointerManager capture = new(box);
            focus.Focus(box).ShouldBeTrue();
            box.IsOpen = true;
            new LayoutEngine().Layout(box, size);
            var list = OwnedTree.Find<UiListView>(box).ShouldNotBeNull();

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
            var outside = new ProbeControl { IsFocusable = true };
            root.Children.Add(box);
            root.Children.Add(outside);
            new LayoutEngine().Layout(root, new Size(20, 10));
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
            var sibling = new ProbeControl { IsFocusable = true };
            root.Children.Add(box);
            root.Children.Add(sibling);
            new LayoutEngine().Layout(root, new Size(20, 10));
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

        cleared.IsHandled.ShouldBeTrue();
        typed.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(2);
        combo.SelectedItem.ShouldBe("Gamma");
        combo.Placeholder.ShouldBe("Choose");
    }

    /// <summary>Verifies TextSelector drives closed-field text instead of Convert.ToString.</summary>
    [Fact]
    public void Render_WhenTextSelectorIsSet_ShowsProjectedTextNotToString()
    {
        var box = new ComboBox
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
            Items = [new Fruit("Kiwi"), new Fruit("Mango")],
            TextSelector = static item => ((Fruit) item!).Name,
            SelectedIndex = 1
        };
        var size = new Size(12, 6);
        new LayoutEngine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("M");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("a");
    }

    /// <summary>Verifies TextSelector drives type-ahead matching instead of Convert.ToString.</summary>
    [Fact]
    public void Input_WhenTextSelectorIsSet_DrivesTypeAheadMatching()
    {
        var combo = new ComboBox
        {
            Items = [new Fruit("Kiwi"), new Fruit("Mango"), new Fruit("Grape")],
            TextSelector = static item => ((Fruit) item!).Name,
            IsOpen = true
        };

        var typed = Router.Route(combo, Events.Key, CharacterKey('m'));

        typed.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(1);
        ((Fruit) combo.SelectedItem!).Name.ShouldBe("Mango");
    }

    /// <summary>Verifies type-ahead accepts printable Unicode scalars outside the basic multilingual plane.</summary>
    [Fact]
    public void Input_WhenPrintableSupplementaryRuneIsTyped_SelectsByPrefix()
    {
        // Arrange
        var character = new Rune(0x10000);
        var combo = new ComboBox
        {
            Items = ["Alpha", $"{character} Linear B"],
            SelectedIndex = 0,
            IsOpen = true
        };

        // Act
        var typed = Router.Route(combo, Events.Key, CharacterKey(character));

        // Assert
        typed.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies each popup session starts a fresh type-ahead prefix.</summary>
    [Fact]
    public void Input_WhenPopupIsReopened_DiscardsPreviousTypeAheadPrefix()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Alpha", "Lima"],
            SelectedIndex = 0,
            IsOpen = true
        };
        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.IsOpen = false;
        combo.IsOpen = true;

        // Act
        var typed = Router.Route(combo, Events.Key, CharacterKey('l'));

        // Assert
        typed.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies ItemTemplate forwards directly to the private drop-down ListView.</summary>
    [Fact]
    public void ItemTemplate_WhenAssigned_ForwardsToDropDownListAndRealizesRows()
    {
        var box = new ComboBox
        {
            DropDownHeight = 4,
            Items = [new Fruit("Kiwi"), new Fruit("Mango")],
            ItemTemplate = static item => new ControlText($"* {((Fruit) item!).Name}"),
            IsOpen = true
        };
        var size = new Size(24, 12);
        new LayoutEngine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        var popup = OwnedTree.Find<Popup>(box).ShouldNotBeNull();
        var list = popup.Content.ShouldBeOfType<UiListView>();
        list.ItemTemplate.ShouldBeSameAs(box.ItemTemplate);
        FrameOracle.Get(frame, new Point(list.Bounds.X, list.Bounds.Y)).ShouldBe("*");
        FrameOracle.Get(frame, new Point(list.Bounds.X + 2, list.Bounds.Y)).ShouldBe("K");
    }

    /// <summary>Verifies a made selection survives assigning a new ItemTemplate after the
    /// drop-down was opened and closed again — its collapsed content must not be mistaken for
    /// every row being genuinely unavailable.</summary>
    [Fact]
    public void ItemTemplate_WhenReassignedAfterOpenedThenClosed_PreservesSelection()
    {
        var box = new ComboBox { Items = [new Fruit("Kiwi"), new Fruit("Mango")], SelectedIndex = 1, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        box.IsOpen = false;
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.ItemTemplate = static item => new ControlText($"* {((Fruit) item!).Name}");

        box.SelectedIndex.ShouldBe(1);
        box.SelectedItem.ShouldBe(new Fruit("Mango"));
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies a made selection survives assigning ItemTemplate when the drop-down was
    /// never opened (the pre-existing, already-correct case, kept as the baseline).</summary>
    [Fact]
    public void ItemTemplate_WhenReassignedWhileNeverOpened_PreservesSelection()
    {
        var box = new ComboBox { Items = [new Fruit("Kiwi"), new Fruit("Mango")], SelectedIndex = 1 };
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.ItemTemplate = static item => new ControlText($"* {((Fruit) item!).Name}");

        box.SelectedIndex.ShouldBe(1);
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies a made selection survives assigning ItemTemplate while the drop-down is
    /// currently open (the pre-existing, already-correct case, kept as the baseline).</summary>
    [Fact]
    public void ItemTemplate_WhenReassignedWhileOpen_PreservesSelection()
    {
        var box = new ComboBox
        {
            Items = [new Fruit("Kiwi"), new Fruit("Mango")],
            SelectedIndex = 1,
            IsOpen = true
        };
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.ItemTemplate = static item => new ControlText($"* {((Fruit) item!).Name}");

        box.SelectedIndex.ShouldBe(1);
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies a made selection at a still-in-range index survives an Items
    /// reassignment after the drop-down was opened and closed again — a never-opened box already
    /// preserved this correctly, but the previously buggy path filtered the remapped selection
    /// away and auto-selected index 0 instead.</summary>
    [Fact]
    public void Items_WhenReassignedAfterOpenedThenClosed_PreservesInRangeSelection()
    {
        var box = new ComboBox { Items = ["a", "b", "c"], SelectedIndex = 2, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        box.IsOpen = false;
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.Items = ["a", "b", "c", "d"];

        box.SelectedIndex.ShouldBe(2);
        box.SelectedItem.ShouldBe("c");
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies shrinking Items below the selected index after the drop-down was opened
    /// and closed again still publishes the transition to no selection, instead of silently
    /// losing it because the list's own SelectionChanged never fires from an already-rejected
    /// state.</summary>
    [Fact]
    public void Items_WhenShrunkBelowSelectedIndexAfterOpenedThenClosed_PublishesNoSelection()
    {
        var box = new ComboBox { Items = ["a", "b", "c"], SelectedIndex = 2, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        box.IsOpen = false;
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.Items = ["a"];

        box.SelectedIndex.ShouldBe(-1);
        selectionChanges.ShouldBe(1);
    }

    /// <summary>Verifies shrinking Items below the selected index while the drop-down was never
    /// opened still publishes the transition to no selection, reproducible even without ever
    /// opening the drop-down.</summary>
    [Fact]
    public void Items_WhenShrunkBelowSelectedIndexWhileNeverOpened_PublishesNoSelection()
    {
        var box = new ComboBox { Items = ["a", "b", "c"], SelectedIndex = 2 };
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.Items = ["a"];

        box.SelectedIndex.ShouldBe(-1);
        selectionChanges.ShouldBe(1);
    }

    /// <summary>Verifies a made selection survives an Items reassignment while the drop-down is
    /// currently open (the pre-existing, already-correct case, kept as the baseline).</summary>
    [Fact]
    public void Items_WhenReassignedWhileOpen_PreservesInRangeSelection()
    {
        var box = new ComboBox { Items = ["a", "b", "c"], SelectedIndex = 2, IsOpen = true };
        new LayoutEngine().Layout(box, new Size(24, 12));
        var selectionChanges = 0;
        box.SelectionChanged += (_, _) => selectionChanges++;

        box.Items = ["a", "b", "c", "d"];

        box.SelectedIndex.ShouldBe(2);
        selectionChanges.ShouldBe(0);
    }

    /// <summary>Verifies a domain object with no ItemTemplate or TextSelector still falls back to
    /// Convert.ToString, preserving the pre-existing default behavior.</summary>
    [Fact]
    public void Render_WhenNeitherProjectionIsSet_FallsBackToToString()
    {
        var box = new ComboBox
        {
            Width = Length.Cells(12),
            Height = Length.Cells(3),
            Items = [new Fruit("Kiwi")],
            SelectedIndex = 0
        };
        var size = new Size(12, 6);
        new LayoutEngine().Layout(box, size);
        using Frame frame = new(size);

        box.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("Z");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("Z");
    }

    private sealed record Fruit(string Name)
    {
        public override string ToString() => "ZZ";
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

    /// <summary>Verifies IsOpen publishes PropertyChanged on open as well as on close, instead of
    /// only republishing the private Popup's Closed notification.</summary>
    [Fact]
    public async Task IsOpen_WhenChanged_PublishesPropertyChangedOnBothTransitionsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var box = new ComboBox { Items = ["A", "B"], SelectedIndex = 0 };
            root.Children.Add(box);
            new LayoutEngine().Layout(root, new Size(20, 10));
            root.Attach(dispatcher);
            var notifications = new List<bool>();
            box.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ComboBox.IsOpen))
                {
                    notifications.Add(box.IsOpen);
                }
            };

            box.IsOpen = true;
            box.IsOpen = false;

            notifications.ShouldBe([true, false]);
        }, TestContext.Current.CancellationToken);
    }

    #region Affixes

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent affix-less ComboBox.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        // Arrange
        using var box = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };
        using var bare = new ComboBox { Items = ["Alpha"], SelectedIndex = 0 };

        // Act
        new LayoutEngine().Layout(box, new Size(30, 3));
        new LayoutEngine().Layout(bare, new Size(30, 3));

        // Assert
        (box.DesiredSize.Width - bare.DesiredSize.Width).ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        // Arrange
        using var box = new ComboBox { Items = ["Alpha"], SelectedIndex = 0 };
        box.Clear(Invalidation.All);

        // Act
        box.StartAffix = new Affix("!");

        // Assert
        box.Pending.ShouldBe(Invalidation.All);
        box.Clear(Invalidation.All);

        // Act
        box.StartAffix = null;

        // Assert
        box.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only.</summary>
    [Fact]
    public void StartAffix_WhenContentChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        // Arrange
        using var box = new ComboBox { Items = ["Alpha"], SelectedIndex = 0, StartAffix = new Affix("|") };
        box.Clear(Invalidation.All);

        // Act
        box.StartAffix = new Affix("/");

        // Assert
        box.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change invalidates Measure again, not just Render.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        // Arrange
        using var box = new ComboBox { Items = ["Alpha"], SelectedIndex = 0, EndAffix = new Affix("!") };
        box.Clear(Invalidation.All);

        // Act - U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        box.EndAffix = new Affix("世");

        // Assert
        box.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        // Arrange
        var affix = new Affix("!");
        using var box = new ComboBox { Items = ["Alpha"], SelectedIndex = 0, StartAffix = affix };
        box.Clear(Invalidation.All);

        // Act
        box.StartAffix = affix;

        // Assert
        box.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies both affixes render pinned inside the field box beside the selected
    /// label, strictly inboard of the drop-down indicator.</summary>
    [Fact]
    public void Render_WhenBothAffixesAreSet_PinsThemInsideFieldBox()
    {
        // Arrange
        using var box = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(box, new Size(16, 3));
        using Frame frame = new(new Size(16, 3));

        // Act
        box.Render(frame.Canvas);

        // Assert - border, ">", gap, "Alpha", gap, "<", gap, "▼".
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe(">");
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(7, 1)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(9, 1)).ShouldBe("<");
        FrameOracle.Get(frame, new Point(11, 1)).ShouldBe("▼");
    }

    /// <summary>Verifies the drop-down indicator keeps its own column whether or not affixes are
    /// set, proving an affix is reserved strictly inboard of the indicator and never shifts or
    /// overlaps it.</summary>
    [Fact]
    public void Render_WhenAffixesAreSet_NeverMovesTheDropDownIndicator()
    {
        // Arrange
        using var bare = new ComboBox { Items = ["Alpha"], SelectedIndex = 0 };
        using var affixed = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(bare, new Size(16, 3));
        new LayoutEngine().Layout(affixed, new Size(16, 3));
        using Frame bareFrame = new(new Size(16, 3));
        using Frame affixedFrame = new(new Size(16, 3));

        // Act
        bare.Render(bareFrame.Canvas);
        affixed.Render(affixedFrame.Canvas);

        // Assert - the indicator sits at the same offset from the right edge of each box's own
        // (differently sized) bounds.
        var bareIndicatorX = bare.Bounds.Right - 2;
        var affixedIndicatorX = affixed.Bounds.Right - 2;
        FrameOracle.Get(bareFrame, new Point(bareIndicatorX, 1)).ShouldBe("▼");
        FrameOracle.Get(affixedFrame, new Point(affixedIndicatorX, 1)).ShouldBe("▼");
        FrameOracle.Get(affixedFrame, new Point(affixedIndicatorX, 1)).ShouldNotBe("<");
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole when the field box
    /// has room for only one, matching the documented priority order.</summary>
    [Fact]
    public void Render_WhenFieldBoxHasRoomForOnlyOneAffix_DropsTheEndAffixFirst()
    {
        // Arrange - a field box with exactly one drawable cell before the indicator's own reserved
        // columns: two border cells, one indicator reservation (2), one content cell.
        using var box = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(3),
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        new LayoutEngine().Layout(box, new Size(5, 3));
        using Frame frame = new(new Size(5, 3));

        // Act
        box.Render(frame.Canvas);

        // Assert - the one drawable field cell goes to the start affix.
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe(">");
    }

    #endregion

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

    private static KeyEventArgs Key(Code code, Modifiers modifiers) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static KeyEventArgs CharacterKey(char character) => CharacterKey(new Rune(character));

    private static KeyEventArgs CharacterKey(Rune character) => new(new Stroke(
        Code.Character,
        character,
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
