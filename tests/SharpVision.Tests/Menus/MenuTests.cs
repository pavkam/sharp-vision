// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies typed menu ownership, selection navigation, check states, and cells.</summary>
public sealed class MenuTests
{
    /// <summary>Verifies layout forwarding follows the newest owner value after synchronous
    /// property reentry for both orientation and spacing.</summary>
    [Fact]
    public void LayoutProperties_WhenPropertyObserversCommitNewerValues_UseNewestStackConfiguration()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Menu.Orientation) && menu.Orientation == Orientation.Vertical)
            {
                menu.Orientation = Orientation.Horizontal;
            }

            if (eventArgs.PropertyName == nameof(Menu.Spacing) && menu.Spacing == 4)
            {
                menu.Spacing = 2;
            }
        };

        menu.Orientation = Orientation.Vertical;
        menu.Spacing = 4;
        new LayoutEngine().Layout(menu, new Size(30, 3));

        menu.Orientation.ShouldBe(Orientation.Horizontal);
        menu.Spacing.ShouldBe(2);
        second.Bounds.Y.ShouldBe(first.Bounds.Y);
        second.Bounds.X.ShouldBe(first.Bounds.Right + 2);
    }
    /// <summary>Verifies menus begin with a useful minimum while retaining inherited width configuration.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesConfigurableFifteenCellMinimumWidth()
    {
        // Arrange and act
        var menu = new Menu();

        // Assert default and validation-before-mutation
        menu.MinWidth.ShouldBe(Length.Cells(15));
        menu.MaxWidth.ShouldBeNull();
        _ = Should.Throw<ArgumentException>(() => menu.MaxWidth = Length.Cells(9));
        menu.MaxWidth.ShouldBeNull();

        // Act and assert direct inherited configuration
        menu.MinWidth = Length.Cells(8);
        menu.MaxWidth = Length.Cells(24);
        menu.MinWidth.ShouldBe(Length.Cells(8));
        menu.MaxWidth.ShouldBe(Length.Cells(24));
    }

    /// <summary>Verifies every Menu-declared property starts at its documented default.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var menu = new Menu();

        menu.Orientation.ShouldBe(Orientation.Horizontal);
        menu.Spacing.ShouldBe(0);
        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
        menu.Items.Count.ShouldBe(0);
        menu.Face.Background.ShouldBe(SemanticColor.Bar);
    }

    /// <summary>Verifies an unknown orientation is rejected before the previous value changes,
    /// matching the sibling Stack.Orientation setter's own validation.</summary>
    [Fact]
    public void Orientation_WhenValueIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.Orientation = (Orientation) 99);

        menu.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies a negative spacing is rejected before the previous value changes.</summary>
    [Fact]
    public void Spacing_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var menu = new Menu { Spacing = 2 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.Spacing = -1);

        menu.Spacing.ShouldBe(2);
    }

    /// <summary>Verifies a horizontal Menu arranges its owned items left to right with Spacing cells
    /// between them, proving both properties' effect on the underlying Stack presentation host.</summary>
    [Fact]
    public void MeasureOverride_WhenOrientationIsHorizontalWithSpacing_ArrangesItemsSideBySideWithGap()
    {
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 2 };
        var first = new MenuItem { Text = "One" };
        var second = new MenuItem { Text = "Two" };
        menu.Items.Add(first);
        menu.Items.Add(second);

        new LayoutEngine().Layout(menu, new Size(30, 1));

        first.Bounds.Y.ShouldBe(second.Bounds.Y);
        second.Bounds.X.ShouldBe(first.Bounds.Right + 2);
    }

    /// <summary>Verifies typed collection ownership selects the first available item and renders compact shared-width rows.</summary>
    [Fact]
    public void Items_WhenAdded_UseTypedOwnershipSelectionAndVerticalCells()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Text = "Open" });
        menu.Items.Add(
            new MenuItem { Text = "Pinned", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuSeparator());
        var first = menu.Items[0];
        var second = menu.Items[1];
        var separator = menu.Items[2];
        var size = new Size(12, 3);
        new LayoutEngine().Layout(menu, size);
        using Frame frame = new(size);

        menu.Render(frame.Canvas);

        menu.Items.Count.ShouldBe(3);
        menu.SelectedIndex.ShouldBe(0);
        menu.SelectedItem.ShouldBeSameAs(first);
        menu.Spacing.ShouldBe(0);
        first.Bounds.ShouldBe(new Rect(0, 0, menu.Bounds.Width, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, menu.Bounds.Width, 1));
        separator.Bounds.ShouldBe(new Rect(0, 2, menu.Bounds.Width, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("O");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("[");
        FrameOracle.Get(frame, new Point(menu.Bounds.Right - 1, 2)).ShouldBe("─");
    }

    /// <summary>Verifies Menu proves direct and ancestor-inherited disabled state at the detached
    /// unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled - the same
    /// disabled contract exercised on a live mounted terminal surface.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenMenuIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        var menu = new Menu();
        var host = new Stack();
        host.Children.Add(menu);

        menu.IsEnabled = false;
        menu.EffectiveIsEnabled.ShouldBeFalse();

        menu.IsEnabled = true;
        menu.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        menu.IsEnabled.ShouldBeTrue();
        menu.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        menu.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies MenuItem proves direct and owning-Menu-inherited disabled state at the
    /// detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenItemIsDisabledDirectlyOrByOwningMenu_ReportsDisabledAndRecovers()
    {
        var item = new MenuItem { Text = "Open" };
        var menu = new Menu();
        menu.Items.Add(item);

        item.IsEnabled = false;
        item.EffectiveIsEnabled.ShouldBeFalse();

        item.IsEnabled = true;
        item.EffectiveIsEnabled.ShouldBeTrue();

        menu.IsEnabled = false;
        item.IsEnabled.ShouldBeTrue();
        item.EffectiveIsEnabled.ShouldBeFalse();

        menu.IsEnabled = true;
        item.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies MenuSeparator proves direct and owning-Menu-inherited disabled state at
    /// the detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenSeparatorIsDisabledDirectlyOrByOwningMenu_ReportsDisabledAndRecovers()
    {
        var separator = new MenuSeparator();
        var menu = new Menu();
        menu.Items.Add(separator);

        separator.IsEnabled = false;
        separator.EffectiveIsEnabled.ShouldBeFalse();

        separator.IsEnabled = true;
        separator.EffectiveIsEnabled.ShouldBeTrue();

        menu.IsEnabled = false;
        separator.IsEnabled.ShouldBeTrue();
        separator.EffectiveIsEnabled.ShouldBeFalse();

        menu.IsEnabled = true;
        separator.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies SelectedItem mirrors SelectedIndex and reports the selected item identity.</summary>
    [Fact]
    public void SelectedItem_WhenSet_UpdatesSelectedIndexAndReportsIdentity()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);

        menu.SelectedItem = second;

        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeSameAs(second);

        menu.SelectedIndex = 0;

        menu.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>Verifies setting SelectedItem to null clears selection, matching SelectedIndex = -1.</summary>
    [Fact]
    public void SelectedItem_WhenSetToNull_ClearsSelection()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "First" });

        menu.SelectedItem = null;

        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies setting SelectedItem to an item this menu does not own clears selection.</summary>
    [Fact]
    public void SelectedItem_WhenItemIsNotOwned_ClearsSelection()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "First" });
        var foreign = new MenuItem { Text = "Foreign" };

        menu.SelectedItem = foreign;

        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies a value below -1 is rejected instead of being silently stored, matching
    /// the sibling TabControl/ListView/ComboBox.SelectedIndex setters.</summary>
    [Fact]
    public void SelectedIndex_WhenValueIsBelowNegativeOne_ThrowsArgumentOutOfRangeException()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "First" });

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.SelectedIndex = -5);

        menu.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies too-low, too-high, and separator-target indexes are each rejected and the
    /// committed selection survives every one of them, matching TabControl's equivalent contract.</summary>
    [Fact]
    public void SelectedIndex_WhenTargetIsInvalid_PreservesSelectionBeforeThrowing()
    {
        var first = new MenuItem { Text = "First" };
        var separator = new MenuSeparator();
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(separator);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.SelectedIndex = -2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.SelectedIndex = 2);
        _ = Should.Throw<ArgumentException>(() => menu.SelectedIndex = 1);

        menu.SelectedIndex.ShouldBe(0);
        menu.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>
    /// Verifies a backward directional key (Up in a vertical menu) from an explicitly cleared
    /// selection (<c>SelectedIndex = -1</c>) wraps to the last item, symmetric with how a forward
    /// directional key from the same cleared state selects the first item (index 0). Both
    /// directions conceptually navigate from "no current item," so both must land on a boundary
    /// item, not have the backward direction land one item short of the end.
    /// </summary>
    [Fact]
    public async Task Dispatch_WhenBackwardKeyArrivesFromClearedSelection_SelectsLastItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(new MenuItem { Text = "First" });
            menu.Items.Add(new MenuItem { Text = "Second" });
            menu.Items.Add(new MenuItem { Text = "Third" });
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.SelectedIndex = -1;

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Up,
                default,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies directional keys skip separators while focus remains on the menu owner.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsSeparatorAndFocusesNextItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "First" };
            var separator = new MenuSeparator();
            var second = new MenuItem { Text = "Second" };
            menu.Items.Add(first);
            menu.Items.Add(separator);
            menu.Items.Add(second);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down,
                default,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(2);
            focus.Focused.ShouldBeSameAs(menu);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies directional keys skip a Collapsed item exactly like a separator, and still
    /// wrap past it back to the first item.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsCollapsedItemAndWrapsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "First" };
            var collapsed = new MenuItem { Text = "Collapsed", Visibility = Visibility.Collapsed };
            var third = new MenuItem { Text = "Third" };
            menu.Items.Add(first);
            menu.Items.Add(collapsed);
            menu.Items.Add(third);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(2);

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            // Wraps past the Collapsed middle item straight back to the first.
            menu.SelectedIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Hidden item is excluded from directional navigation exactly like a
    /// Collapsed one - Menu's own eligibility gate reads EffectiveIsVisible, which requires
    /// Visibility == IsVisible, so Hidden's usual "keeps its slot, only excludes render/input" leaf
    /// contract still yields the same navigation exclusion Collapsed produces at this layer.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsHiddenItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "First" };
            var hidden = new MenuItem { Text = "Hidden", Visibility = Visibility.Hidden };
            var third = new MenuItem { Text = "Third" };
            menu.Items.Add(first);
            menu.Items.Add(hidden);
            menu.Items.Add(third);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Home and End jump straight to the first and last available entries,
    /// skipping a disabled boundary entry exactly like the existing directional keys already skip
    /// separators, collapsed, and hidden entries elsewhere in the collection. Neither key wraps:
    /// unlike Left/Right/Up/Down/Tab, a disabled entry at the far boundary is skipped toward the
    /// interior, never around to the opposite end, mirroring NavigationView's sibling contract.</summary>
    [Fact]
    public async Task Dispatch_WhenHomeOrEndKeyArrives_SelectsBoundaryAvailableEntrySkippingDisabledAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "First", IsEnabled = false };
            var second = new MenuItem { Text = "Second" };
            var third = new MenuItem { Text = "Third" };
            var fourth = new MenuItem { Text = "Fourth", IsEnabled = false };
            menu.Items.Add(first);
            menu.Items.Add(second);
            menu.Items.Add(third);
            menu.Items.Add(fourth);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.SelectedIndex = 1;

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.End, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            // Skips the disabled Fourth entry and lands on the last genuinely available one.
            menu.SelectedIndex.ShouldBe(2);

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Home, default, nativeCode: 0, Modifiers.None, KeyAction.Press)));

            // Skips the disabled First entry and lands on the first genuinely available one.
            menu.SelectedIndex.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the vertical shared shortcut-column measurement - which scans every owned
    /// MenuItem for its widest label and shortcut columns - excludes a Collapsed item's own label
    /// width from that shared maximum, matching the general Collapsed-excludes-size contract for a
    /// menu-specific derived measurement, not merely the base Stack item spacing.</summary>
    [Fact]
    public void MeasureOverride_WhenVerticalItemIsCollapsed_ExcludesItsLabelFromSharedColumnWidth()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        var narrow = new MenuItem { Text = "Hi", Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('h')) };
        var wide = new MenuItem { Text = "A very long label indeed", Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('w')) };
        menu.Items.Add(narrow);
        menu.Items.Add(wide);
        var engine = new LayoutEngine();
        var size = new Size(60, 4);
        engine.Layout(menu, size);
        var baselineWidth = menu.DesiredSize.Width;

        wide.Visibility = Visibility.Collapsed;
        engine.Layout(menu, size);

        menu.DesiredSize.Width.ShouldBeLessThan(baselineWidth);
    }

    /// <summary>Verifies a vertical Menu negotiates one shared leading affix column across every
    /// owned row: a sibling row with no StartAffix of its own still gets its caption pushed right
    /// to match the row that does, rather than starting flush at its own (zero-width) marker
    /// column.</summary>
    [Fact]
    public void MeasureOverride_WhenOnlyOneVerticalItemHasStartAffix_AlignsEveryCaptionToTheSharedColumn()
    {
        // Arrange
        var withAffix = new MenuItem { Text = "Go", StartAffix = new Affix(">") };
        var withoutAffix = new MenuItem { Text = "Stop" };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(withAffix);
        menu.Items.Add(withoutAffix);

        // Act
        new LayoutEngine().Layout(menu, new Size(30, 3));

        // Assert - both captions begin at the same column, and it is the wider row's own
        // reserved column (marker 0 + affix width 1 + gap 1 = 2), not the narrower row's own
        // (zero) reservation.
        withAffix.TextControl!.Bounds.X.ShouldBe(withoutAffix.TextControl!.Bounds.X);
        withoutAffix.TextControl!.Bounds.X.ShouldBe(withoutAffix.Bounds.X + 2);
    }

    /// <summary>Verifies the shared start-affix column widens Menu's own desired width when a
    /// narrow-labeled affix row would otherwise dictate a column too tight for a long-labeled
    /// unaffixed sibling once that sibling's caption is pushed right to match.</summary>
    [Fact]
    public void MeasureOverride_WhenSharedStartAffixColumnWouldClipALongerSibling_WidensDesiredWidth()
    {
        // Arrange
        var shortWithAffix = new MenuItem { Text = "Go", StartAffix = new Affix(">") };
        var longWithoutAffix = new MenuItem { Text = "A very long label indeed" };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(shortWithAffix);
        menu.Items.Add(longWithoutAffix);

        // Act
        new LayoutEngine().Layout(menu, new Size(60, 3));

        // Assert - the negotiated column (labelWidth 24 + shared affix cells 2) widens Menu's own
        // desired width, and the long label's full text still fits, unclipped, after being pushed
        // right by the shared two-cell affix column.
        menu.DesiredSize.Width.ShouldBe(26);
        longWithoutAffix.TextControl!.DesiredSize.Width.ShouldBe(24);
        longWithoutAffix.TextControl!.Bounds.Width.ShouldBe(24);
    }

    /// <summary>Verifies check and named radio items commit state before menu-level invocation reporting.</summary>
    [Fact]
    public void PerformInvoke_WhenCheckAndRadioItemsActivate_CommitsStateBeforeEvent()
    {
        var menu = new Menu();
        var check = new MenuItem { Text = "Auto save", Kind = MenuItemKind.Check };
        var first = new MenuItem
        {
            Text = "Small",
            Kind = MenuItemKind.Radio,
            GroupName = "size",
            IsChecked = true
        };
        var second = new MenuItem
        {
            Text = "Large",
            Kind = MenuItemKind.Radio,
            GroupName = "size"
        };
        List<string> observed = [];
        menu.Items.Add(check);
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.ItemInvoked += (_, eventArgs) =>
            observed.Add($"{eventArgs.Item.Text}:{eventArgs.Item.IsChecked}");

        check.PerformInvoke();
        second.PerformInvoke();

        check.IsChecked.ShouldBeTrue();
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        observed.ShouldBe(["Auto save:True", "Large:True"]);
    }

    /// <summary>Verifies radio property observers see a complete group commit.</summary>
    [Fact]
    public void IsChecked_WhenRadioSelectionChanges_StagesEveryFieldBeforePropertyNotifications()
    {
        var menu = new Menu();
        var first = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        var second = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "size" };
        menu.Items.Add(first);
        menu.Items.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.IsChecked))
            {
                first.IsChecked.ShouldBeFalse();
                second.IsChecked.ShouldBeTrue();
                observed = true;
            }
        };

        second.IsChecked = true;

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies an item reports its invocation before the owning menu forwards it.</summary>
    [Fact]
    public void PerformInvoke_WhenItemActivates_RaisesItemBeforeMenuNotification()
    {
        var menu = new Menu();
        var item = new MenuItem();
        List<string> order = [];
        menu.Items.Add(item);
        item.Invoked += (_, _) => order.Add("item");
        menu.ItemInvoked += (_, _) => order.Add("menu");

        item.PerformInvoke();

        order.ShouldBe(["item", "menu"]);
    }

    /// <summary>Verifies item callbacks cannot forward invocation into an owner relationship they
    /// removed, moved, or disposed.</summary>
    [Theory]
    [InlineData("remove")]
    [InlineData("move")]
    [InlineData("dispose")]
    public void PerformInvoke_WhenItemCallbackInvalidatesOwner_SkipsOldMenuForwarding(string mutation)
    {
        var menu = new Menu();
        var destination = new Menu();
        var item = new MenuItem();
        var oldOwnerInvocations = 0;
        menu.Items.Add(item);
        menu.ItemInvoked += (_, _) => oldOwnerInvocations++;
        item.Invoked += (_, _) =>
        {
            if (mutation == "dispose")
            {
                menu.Dispose();
                return;
            }

            menu.Items.Remove(item).ShouldBeTrue();

            if (mutation == "move")
            {
                destination.Items.Add(item);
            }
        };

        item.PerformInvoke();

        oldOwnerInvocations.ShouldBe(0);
    }

    /// <summary>Verifies an earlier radio publication can remove or dispose the later staged
    /// member without publishing through that stale target.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsChecked_WhenEarlierRadioCallbackInvalidatesLaterMember_SkipsLaterPublication(bool dispose)
    {
        var menu = new Menu();
        var first = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "size", IsChecked = true };
        var second = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "size" };
        var published = 0;
        menu.Items.Add(first);
        menu.Items.Add(second);
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.IsChecked))
            {
                if (dispose)
                {
                    second.Dispose();
                }
                else
                {
                    menu.Items.Remove(second).ShouldBeTrue();
                }
            }
        };
        second.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.IsChecked))
            {
                published++;
            }
        };

        second.IsChecked = true;

        published.ShouldBe(0);
    }

    /// <summary>Verifies the bound command runs after Invoked and after the menu's own
    /// notification.</summary>
    [Fact]
    public void PerformInvoke_WhenCommandCanExecute_RunsAfterMenuNotification()
    {
        var menu = new Menu();
        var item = new MenuItem();
        List<string> order = [];
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        item.Command = command;
        menu.Items.Add(item);
        item.Invoked += (_, _) => order.Add("item");
        menu.ItemInvoked += (_, _) => order.Add("menu");

        item.PerformInvoke();

        order.ShouldBe(["item", "menu", "command"]);
    }

    /// <summary>Verifies activation retains the entry command binding across reentrant item callbacks.</summary>
    [Fact]
    public void PerformInvoke_WhenInvokedCallbackRebindsAndDisposes_ExecutesCapturedCommand()
    {
        var originalParameter = new object();
        var original = new ProbeCommand();
        var replacement = new ProbeCommand();
        var item = new MenuItem { Command = original, CommandParameter = originalParameter };
        item.Invoked += (_, _) =>
        {
            item.Command = replacement;
            item.CommandParameter = new object();
            item.Dispose();
        };

        item.PerformInvoke();

        original.Executions.ShouldBe([originalParameter]);
        replacement.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies an item with an open submenu toggles it instead of invoking, so neither
    /// Invoked nor the bound command ever fires.</summary>
    [Fact]
    public void PerformInvoke_WhenItemHasSubmenu_TogglesSubmenuWithoutInvokingOrExecutingCommand()
    {
        var submenu = new Menu();
        submenu.Items.Add(new MenuItem());
        var command = new ProbeCommand();
        var item = new MenuItem { Submenu = submenu, Command = command };
        var invoked = 0;
        item.Invoked += (_, _) => invoked++;

        item.PerformInvoke();

        invoked.ShouldBe(0);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies an unavailable item (disabled or hidden) rejects programmatic activation
    /// without raising Invoked, matching Button.PerformClick's own unavailable no-op contract.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void PerformInvoke_WhenItemIsUnavailable_DoesNothing(bool enabled, Visibility visibility)
    {
        var item = new MenuItem { Text = "Save", IsEnabled = enabled, Visibility = visibility };
        var invoked = 0;
        item.Invoked += (_, _) => invoked++;

        item.PerformInvoke();

        invoked.ShouldBe(0);
    }

    /// <summary>Verifies PerformInvoke rejects use after disposal.</summary>
    [Fact]
    public void PerformInvoke_WhenDisposed_Throws()
    {
        var item = new MenuItem();
        item.Dispose();

        _ = Should.Throw<ObjectDisposedException>(item.PerformInvoke);
    }

    /// <summary>Verifies InvokeAccessKey selects and activates an ordinary available item.</summary>
    [Fact]
    public void InvokeAccessKey_WhenItemIsAvailable_SelectsAndActivatesWithKeyboardCause()
    {
        var item = new MenuItem { Text = "Save" };
        var menu = new Menu();
        menu.Items.Add(item);
        var invocations = new List<ActivationCause>();
        item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

        var result = menu.InvokeAccessKey(item);

        result.ShouldBeTrue();
        menu.SelectedItem.ShouldBeSameAs(item);
        invocations.ShouldBe([ActivationCause.Keyboard]);
    }

    /// <summary>Verifies InvokeAccessKey re-validates its target after SelectFromInput runs
    /// application callbacks. SelectFromInput publishes SelectedIndex/SelectedItem before
    /// InvokeAccessKey activates the item, and a reentrant subscriber can disable the very item
    /// being invoked during that publish. Before this fix, the target was never re-checked after
    /// the callback ran, so a callback-disabled item was still activated.</summary>
    [Fact]
    public void InvokeAccessKey_WhenSelectionPublishDisablesTheTarget_ReturnsFalseWithoutActivating()
    {
        // A menu auto-selects the first item it gains while nothing is selected yet, so `other`
        // has to exist first: InvokeAccessKey(item) then drives a real 0->1 SelectedIndex
        // transition (and its notification) instead of a same-index no-op Select never publishes.
        var other = new MenuItem { Text = "Other" };
        var item = new MenuItem { Text = "Save" };
        var menu = new Menu();
        menu.Items.Add(other);
        menu.Items.Add(item);
        menu.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Menu.SelectedIndex))
            {
                item.IsEnabled = false;
            }
        };
        var invocations = new List<ActivationCause>();
        item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

        var result = menu.InvokeAccessKey(item);

        result.ShouldBeFalse();
        invocations.ShouldBeEmpty();
    }

    /// <summary>Verifies InvokeAccessKey re-validates that the target still occupies the selected
    /// slot after SelectFromInput runs callbacks, mirroring the enabled/visible re-check. A
    /// reentrant subscriber that moves selection to a different item during the publish must
    /// prevent the original target from activating.</summary>
    [Fact]
    public void InvokeAccessKey_WhenSelectionPublishMovesSelectionElsewhere_ReturnsFalseWithoutActivating()
    {
        // Same ordering requirement as the sibling disable test above: `other` has to be added
        // first so it - not `item` - claims the menu's auto-selected slot, leaving a real
        // transition for InvokeAccessKey(item) to publish and a reentrant handler to observe.
        var other = new MenuItem { Text = "Other" };
        var item = new MenuItem { Text = "Save" };
        var menu = new Menu();
        menu.Items.Add(other);
        menu.Items.Add(item);
        var reentered = false;
        menu.PropertyChanged += (_, eventArgs) =>
        {
            if (reentered || eventArgs.PropertyName != nameof(Menu.SelectedIndex))
            {
                return;
            }

            reentered = true;
            menu.SelectedIndex = 0;
        };
        var invocations = new List<ActivationCause>();
        item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

        var result = menu.InvokeAccessKey(item);

        result.ShouldBeFalse();
        invocations.ShouldBeEmpty();
        menu.SelectedItem.ShouldBeSameAs(other);
    }

    /// <summary>Verifies a separator is never focusable, hit-testable, selectable, or invokable.</summary>
    [Fact]
    public async Task MenuSeparator_WhenUsed_RemainsNonInteractiveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu();
            var item = new MenuItem { Text = "Open" };
            var separator = new MenuSeparator();
            menu.Items.Add(item);
            menu.Items.Add(separator);
            new LayoutEngine().Layout(menu, new Size(12, 1));
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);

            separator.CanFocus.ShouldBeFalse();
            separator.HitTest(new Point(separator.Bounds.X, separator.Bounds.Y)).ShouldBeNull();
            focus.Focus(separator).ShouldBeFalse();
            _ = Should.Throw<ArgumentException>(() => menu.SelectedIndex = 1);
            menu.SelectedIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MenuSeparator's local style overrides the code-owned default and
    /// clearing it restores the default, matching every other IStyled control's precedence
    /// contract - the same round-trip MenuItem's own Style property already proves.</summary>
    [Fact]
    public void MenuSeparatorStyle_WhenAssignedThenCleared_OverridesDefaultThenReturnsToIt()
    {
        var separator = new MenuSeparator();
        var defaultStyle = separator.ActualStyle;
        separator.Style.ShouldBeNull();
        var custom = defaultStyle with { Glyph = new Rune('=') };

        separator.Style = custom;

        separator.Style.ShouldBe(custom);
        separator.ActualStyle.ShouldBe(custom);

        separator.Style = null;

        separator.Style.ShouldBeNull();
        separator.ActualStyle.ShouldBe(defaultStyle);
    }

    /// <summary>Verifies a local Style's custom glyph actually renders in place of the theme
    /// default, proving the property is not merely stored but consumed by rendering.</summary>
    [Fact]
    public void MenuSeparatorStyle_WhenAssignedCustomGlyph_RendersThatGlyph()
    {
        var separator = new MenuSeparator
        {
            Style = MenuSeparatorStyle.Default with { Glyph = new Rune('=') },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var size = new Size(5, 1);
        new LayoutEngine().Layout(separator, size);
        using Frame frame = new(size);

        separator.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("=");
    }

    /// <summary>Verifies Tab and Shift+Tab move menu selection while private items remain outside traversal.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressed_MovesSelectionWithoutLeavingMenuAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var menu = new Menu { Orientation = Orientation.Vertical };
            var a = new MenuItem { Text = "A" };
            var b = new MenuItem { Text = "B" };
            var c = new MenuItem { Text = "C" };
            var outside = new ProbeControl { IsFocusable = true };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            root.Children.Add(menu);
            root.Children.Add(outside);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(menu).ShouldBeTrue();
            var next = Router.Route(menu, Events.Key, Tab());

            next.IsHandled.ShouldBeTrue();
            next.Command.ShouldBe(PostRouteCommand.None);
            menu.SelectedIndex.ShouldBe(1);
            focus.Focused.ShouldBeSameAs(menu);

            var previous = Router.Route(menu, Events.Key, Tab(Modifiers.Shift));

            previous.IsHandled.ShouldBeTrue();
            previous.Command.ShouldBe(PostRouteCommand.None);
            menu.SelectedIndex.ShouldBe(0);
            focus.Focused.ShouldBeSameAs(menu);
            outside.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Enter invokes the selected private item through the menu focus owner.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterIsPressed_InvokesSelectedItemWithKeyboardCauseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "First" };
            var second = new MenuItem { Text = "Second" };
            menu.Items.Add(first);
            menu.Items.Add(second);
            menu.SelectedIndex = 1;
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<(MenuItem Item, ActivationCause Cause)>();
            menu.ItemInvoked += (_, eventArgs) => invocations.Add((eventArgs.Item, eventArgs.Cause));

            // Act
            var result = Router.Route(menu, Events.Key, Key(Code.Enter));

            // Assert
            result.IsHandled.ShouldBeTrue();
            invocations.ShouldBe([(second, ActivationCause.Keyboard)]);
            focus.Focused.ShouldBeSameAs(menu);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Space holds and then invokes the selected private item exactly once.</summary>
    [Fact]
    public async Task Dispatch_WhenSpaceCompletes_InvokesSelectedItemOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            // Act and assert held state
            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();
            invocations.ShouldBeEmpty();

            // Act and assert completion
            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release));
            release.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeFalse();
            invocations.ShouldBe([ActivationCause.Keyboard]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not invoke the selected item,
    /// and leaves the stroke unhandled so a shortcut bound to the modified combination still sees it.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasControlModifier_DoesNotInvokeAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "First" };
            menu.Items.Add(item);
            menu.SelectedIndex = 0;
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<(MenuItem Item, ActivationCause Cause)>();
            menu.ItemInvoked += (_, eventArgs) => invocations.Add((eventArgs.Item, eventArgs.Cause));

            var result = Router.Route(menu, Events.Key, Key(Code.Enter, Modifiers.Control));

            result.IsHandled.ShouldBeFalse();
            invocations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still invokes.</summary>
    [Fact]
    public async Task Dispatch_WhenEnterHasShiftModifier_StillInvokesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "First" };
            menu.Items.Add(item);
            menu.SelectedIndex = 0;
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<(MenuItem Item, ActivationCause Cause)>();
            menu.ItemInvoked += (_, eventArgs) => invocations.Add((eventArgs.Item, eventArgs.Cause));

            var result = Router.Route(menu, Events.Key, Key(Code.Enter, Modifiers.Shift));

            result.IsHandled.ShouldBeTrue();
            invocations.ShouldBe([(item, ActivationCause.Keyboard)]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an incidental Control modifier on the arming Space press does not latch the
    /// pressed frame, and leaves the stroke unhandled so it bubbles.</summary>
    [Fact]
    public async Task Dispatch_WhenSpacePressHasControlModifier_DoesNotArmAndLeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press, Modifiers.Control));

            press.IsHandled.ShouldBeFalse();
            item.IsPressed.ShouldBeFalse();

            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release, Modifiers.Control));

            release.IsHandled.ShouldBeFalse();
            invocations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Space is left unhandled - not silently swallowed - when the selected item is
    /// disabled, so a shortcut bound to plain Space still sees it. Insertion no longer auto-selects a
    /// disabled item, but <see cref="Menu.SelectedIndex"/>'s own setter only validates range and
    /// separator shape - not availability - so a disabled item can still become selected explicitly.
    /// The unmatched release still consumes its own stroke as a no-op, the same as any other unarmed
    /// eligible release.</summary>
    [Fact]
    public async Task Dispatch_WhenSpacePressedWithSelectedItemDisabled_LeavesUnhandledAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run", IsEnabled = false };
            menu.Items.Add(item);
            menu.SelectedIndex = 0;
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));

            press.IsHandled.ShouldBeFalse();
            item.IsPressed.ShouldBeFalse();

            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release));

            release.IsHandled.ShouldBeTrue();
            invocations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the gate applies symmetrically to the release arm: an eligible press arms
    /// the item, but an incidental Control modifier on the paired release must not invoke it - the
    /// release still consumes the stroke and clears the pressed frame.</summary>
    [Fact]
    public async Task Dispatch_WhenSpaceReleaseHasControlModifierAfterEligiblePress_DoesNotInvokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();

            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release, Modifiers.Control));

            release.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeFalse();
            invocations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Shift-held Space still holds and invokes.</summary>
    [Fact]
    public async Task Dispatch_WhenSpaceHasShiftModifier_StillCompletesAndInvokesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press, Modifiers.Shift));
            press.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();

            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release, Modifiers.Shift));
            release.IsHandled.ShouldBeTrue();
            invocations.ShouldBe([ActivationCause.Keyboard]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the currently space-pressed item directly clears the held
    /// reference so the next Space release does not crash with ObjectDisposedException.</summary>
    [Fact]
    public async Task Dispatch_WhenSpacePressedItemDisposedDirectly_ReleaseDoesNotThrowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.IsHandled.ShouldBeTrue();

            item.Dispose();

            _ = Should.NotThrow(() => Router.Route(menu, Events.Key, Space(KeyAction.Release)));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing IsPressed subscriber on the space-pressed item cannot suppress the
    /// ItemInvoked unsubscription during disposal; the failure is aggregated and rethrown once
    /// cleanup completes.</summary>
    [Fact]
    public async Task Dispose_WhenSpacePressedItemPressedSubscriberThrows_SurfacesFailureAndUnsubscribesItemInvokedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "Run" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.ItemInvoked += (_, _) => { };

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();

            var expected = new InvalidOperationException("The pressed subscriber failed.");
            item.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.IsPressed))
                {
                    throw expected;
                }
            };

            var exception = Should.Throw<InvalidOperationException>(menu.Dispose);

            exception.ShouldBeSameAs(expected);
            menu.IsDisposed.ShouldBeTrue();
            var field = typeof(Menu).GetField(
                nameof(Menu.ItemInvoked),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            _ = field.ShouldNotBeNull();
            field.GetValue(menu).ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing IsPressed subscriber on a nested menu's space-pressed item cannot
    /// suppress its delegated submenu-chain close; the failure is aggregated and rethrown once the
    /// whole chain closes. The mutation targets a non-root nested menu, so closure can only come from
    /// this delegated call, not the modality root's own unavailable handling.</summary>
    [Fact]
    public async Task Visibility_WhenNestedMenuSpacePressedItemPressedSubscriberThrows_SurfacesFailureAndClosesChainAsync()
    {
        // Arrange
        var nestedMenu = new Menu { Orientation = Orientation.Vertical };
        var leaf = new MenuItem { Text = "Leaf" };
        var other = new MenuItem { Text = "Other" };
        nestedMenu.Items.Add(leaf);
        nestedMenu.Items.Add(other);
        var nested = new MenuItem { Text = "Nested", Submenu = nestedMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(
            () =>
            {
                nestedMenu.SelectedIndex = 1;
                Router.Route(nestedMenu, Events.Key, Space(KeyAction.Press)).IsHandled.ShouldBeTrue();
            },
            "arm space press on an item in the open nested menu");
        other.IsPressed.ShouldBeTrue();

        var expected = new InvalidOperationException("The pressed subscriber failed.");
        other.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.IsPressed))
            {
                throw expected;
            }
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(
                () => nestedMenu.Visibility = Visibility.Hidden).ShouldBeSameAs(expected),
            "hide the open nested menu with a throwing pressed subscriber");

        // Assert
        firstPopup.IsOpen.ShouldBeFalse();
        nestedPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies private menu items reject external focus.</summary>
    [Fact]
    public async Task Focus_WhenMenuItemReceivesExternalFocus_SyncsSelectedIndexAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var a = new MenuItem { Text = "A" };
            var b = new MenuItem { Text = "B" };
            var c = new MenuItem { Text = "C" };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(c).ShouldBeFalse();
            menu.SelectedIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies arrow navigation wraps current selection while focus remains on the menu.</summary>
    [Fact]
    public async Task Dispatch_WhenArrowAfterExternalFocus_NavigatesFromFocusedItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var a = new MenuItem { Text = "A" };
            var b = new MenuItem { Text = "B" };
            var c = new MenuItem { Text = "C" };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            menu.SelectedIndex = 2;
            focus.Focus(menu).ShouldBeTrue();

            _ = Router.Route(menu, Events.Key, new KeyEventArgs(new Stroke(
                Code.Down,
                default,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

            menu.SelectedIndex.ShouldBe(0);
            focus.Focused.ShouldBeSameAs(menu);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies submenu popup presentation follows the owning menu orientation.</summary>
    [Fact]
    public void PerformInvoke_WhenSubmenuOpens_UsesAttachedMenuSurfaceAndDirectionalPlacement()
    {
        // Arrange horizontal menu
        var horizontalSubmenu = new Menu { Orientation = Orientation.Vertical };
        horizontalSubmenu.Items.Add(new MenuItem { Text = "Open" });
        var horizontalItem = new MenuItem { Text = "File", Submenu = horizontalSubmenu };
        var horizontal = new Menu
        {
            Orientation = Orientation.Horizontal,
            Height = Length.Cells(1),
            VerticalAlignment = VerticalAlignment.Top
        };
        horizontal.Items.Add(horizontalItem);
        var horizontalRoot = new Overlay { Children = { horizontal } };
        var engine = new LayoutEngine();
        engine.Layout(horizontalRoot, new Size(30, 10));

        // Act horizontal
        horizontalItem.PerformInvoke();
        engine.Layout(horizontalRoot, new Size(30, 10));
        var horizontalPopup = OwnedTree.Find<Popup>(horizontalItem).ShouldNotBeNull();

        // Assert horizontal
        horizontalPopup.Placement.ShouldBe(PopupPlacement.Below);
        horizontalPopup.SurfaceBounds.Y.ShouldBe(horizontalItem.Bounds.Bottom);
        horizontalPopup.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
        horizontalPopup.Face.Background.ShouldBe(SemanticColor.Window);

        // Arrange vertical menu
        var verticalSubmenu = new Menu { Orientation = Orientation.Vertical };
        verticalSubmenu.Items.Add(new MenuItem { Text = "Recent" });
        var verticalItem = new MenuItem { Text = "Open", Submenu = verticalSubmenu };
        var vertical = new Menu { Orientation = Orientation.Vertical, Width = Length.Cells(8) };
        vertical.Items.Add(verticalItem);
        var verticalRoot = new Overlay { Children = { vertical } };
        engine.Layout(verticalRoot, new Size(30, 10));

        // Act vertical
        verticalItem.PerformInvoke();
        engine.Layout(verticalRoot, new Size(30, 10));
        var verticalPopup = OwnedTree.Find<Popup>(verticalItem).ShouldNotBeNull();

        // Assert vertical
        verticalPopup.Placement.ShouldBe(PopupPlacement.Right);
        verticalPopup.SurfaceBounds.X.ShouldBe(verticalItem.Bounds.Right);
    }

    /// <summary>Verifies a MenuItem submenu popup pins no presentation, so it resolves the same
    /// theme-role chrome as a standalone ContextMenu popup instead of a divergent local override.</summary>
    [Fact]
    public void Submenu_WhenOpened_RendersIdenticalChromeToContextMenu()
    {
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var item = new MenuItem { Text = "File", Submenu = submenu };
        var owner = new Menu { Orientation = Orientation.Horizontal };
        owner.Items.Add(item);
        _ = new Overlay { Children = { owner } };
        item.PerformInvoke();
        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(new MenuItem { Text = "Open" });
        var contextMenuPopup = (Popup) contextMenu.Presentation;

        submenuPopup.Border.GlyphStyle.ShouldBe(contextMenuPopup.Border.GlyphStyle);
        submenuPopup.Border.Sides.ShouldBe(contextMenuPopup.Border.Sides);
        submenuPopup.Face.Background.ShouldBe(contextMenuPopup.Face.Background);
    }

    /// <summary>Verifies SubmenuChrome applies to an already-open submenu's popup without leaking
    /// the private Popup itself.</summary>
    [Fact]
    public void SubmenuStyle_WhenSetOnAnOpenSubmenu_AppliesToItsPopup()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var item = new MenuItem { Text = "File", Submenu = submenu };
        var owner = new Menu { Orientation = Orientation.Horizontal };
        owner.Items.Add(item);
        _ = new Overlay { Children = { owner } };
        item.PerformInvoke();
        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        item.SubmenuChrome = new PopupChrome { Border = border };

        submenuPopup.Border.ShouldBe(border);
    }

    /// <summary>Verifies SubmenuChrome set before a submenu is ever assigned still applies once the
    /// popup is created for it.</summary>
    [Fact]
    public void SubmenuStyle_WhenSetBeforeSubmenuIsAssigned_AppliesToTheCreatedPopup()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var item = new MenuItem { Text = "File", SubmenuChrome = new PopupChrome { Border = border } };

        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        item.Submenu = submenu;

        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        submenuPopup.Border.ShouldBe(border);
    }

    /// <summary>Verifies SubmenuChrome survives a Submenu reassignment, which recreates the popup.</summary>
    [Fact]
    public void SubmenuStyle_WhenSubmenuIsReassigned_StillAppliesToTheNewPopup()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var first = new Menu { Orientation = Orientation.Vertical };
        first.Items.Add(new MenuItem { Text = "First" });
        var item = new MenuItem { Text = "File", Submenu = first, SubmenuChrome = new PopupChrome { Border = border } };

        var second = new Menu { Orientation = Orientation.Vertical };
        second.Items.Add(new MenuItem { Text = "Second" });
        item.Submenu = second;

        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        submenuPopup.Border.ShouldBe(border);
    }

    /// <summary>Verifies ResetSubmenuChrome returns an open submenu's popup to its PopupChrome
    /// appearance, matching an item that never authored a local override.</summary>
    [Fact]
    public void ResetSubmenuStyle_WhenPopupHasLocalOverride_ReturnsToThemeAppearance()
    {
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var item = new MenuItem { Text = "File", Submenu = submenu };
        var owner = new Menu { Orientation = Orientation.Horizontal };
        owner.Items.Add(item);
        _ = new Overlay { Children = { owner } };
        item.PerformInvoke();
        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        var themeRoleBorder = submenuPopup.Border;
        item.SubmenuChrome = new PopupChrome
        {
            Border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
        };

        item.ResetSubmenuChrome();

        item.SubmenuChrome.ShouldBe(default);
        submenuPopup.Border.ShouldBe(themeRoleBorder);
    }

    /// <summary>Verifies SubmenuChrome round-trips on a standalone item with no submenu, and has no
    /// popup to apply to until one exists.</summary>
    [Fact]
    public void SubmenuStyle_WhenNoSubmenuExists_RoundTripsWithoutThrowing()
    {
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);
        var item = new MenuItem { Text = "File", SubmenuChrome = new PopupChrome { Border = border } };

        item.SubmenuChrome.ShouldBe(new PopupChrome { Border = border });
        OwnedTree.Find<Popup>(item).ShouldBeNull();
    }

    /// <summary>Verifies replacing a standalone item's submenu detaches the previous menu without
    /// disposing it, while the framework-owned popup that hosted it is disposed.</summary>
    [Fact]
    public void Submenu_WhenReplacedOnStandaloneItem_DetachesPreviousMenuWithoutDisposingIt()
    {
        // Arrange
        var previous = new Menu { Orientation = Orientation.Vertical };
        previous.Items.Add(new MenuItem { Text = "First" });
        var item = new MenuItem { Text = "File", Submenu = previous };
        var previousPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        var replacement = new Menu { Orientation = Orientation.Vertical };
        replacement.Items.Add(new MenuItem { Text = "Second" });

        // Act
        item.Submenu = replacement;

        // Assert the previous menu survives detached, while its framework popup is disposed
        previous.IsDisposed.ShouldBeFalse();
        previousPopup.IsDisposed.ShouldBeTrue();
        item.Submenu.ShouldBeSameAs(replacement);

        // Assert the detached menu can still be mutated and reassigned elsewhere
        previous.Items.Add(new MenuItem { Text = "Reused" });
        var other = new MenuItem { Text = "Edit", Submenu = previous };
        other.Submenu.ShouldBeSameAs(previous);
    }

    /// <summary>Verifies clearing a standalone item's submenu detaches the previous menu without
    /// disposing it, while the framework-owned popup that hosted it is disposed.</summary>
    [Fact]
    public void Submenu_WhenClearedOnStandaloneItem_DetachesPreviousMenuWithoutDisposingIt()
    {
        // Arrange
        var previous = new Menu { Orientation = Orientation.Vertical };
        previous.Items.Add(new MenuItem { Text = "First" });
        var item = new MenuItem { Text = "File", Submenu = previous };
        var previousPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        // Act
        item.Submenu = null;

        // Assert
        previous.IsDisposed.ShouldBeFalse();
        previousPopup.IsDisposed.ShouldBeTrue();
        item.Submenu.ShouldBeNull();
    }

    /// <summary>Verifies directly disposing an adopted submenu clears its closed or open retained popup relationship.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Submenu_WhenDisposedDirectly_ClearsOwnedRelationship(bool open)
    {
        var submenu = new Menu();
        var item = new MenuItem { Submenu = submenu };
        var popup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        if (open)
        {
            item.PerformInvoke();
            popup.IsOpen.ShouldBeTrue();
        }

        submenu.Dispose();

        item.Submenu.ShouldBeNull();
        item.HasRetainedSubmenuSurface.ShouldBeFalse();
        popup.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies direct submenu disposal closes the active menu session that owned it.</summary>
    [Fact]
    public async Task Submenu_WhenDisposedInsideActiveMenuChain_ClosesTheSessionAsync()
    {
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var item = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        await surface.UpdateAsync(item.PerformInvoke, "open submenu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        await surface.UpdateAsync(submenu.Dispose, "dispose active submenu");

        item.Submenu.ShouldBeNull();
        popup.IsDisposed.ShouldBeTrue();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies assigning a menu already hosted as another item's submenu throws and
    /// leaves the target item's own submenu and popup untouched.</summary>
    [Fact]
    public void Submenu_WhenAlreadyHostedByAnotherItem_ThrowsAndLeavesTargetItemUnchanged()
    {
        // Arrange
        var shared = new Menu { Orientation = Orientation.Vertical };
        shared.Items.Add(new MenuItem { Text = "Shared" });
        var owner = new MenuItem { Text = "File", Submenu = shared };
        var existing = new Menu { Orientation = Orientation.Vertical };
        existing.Items.Add(new MenuItem { Text = "Existing" });
        var target = new MenuItem { Text = "Edit", Submenu = existing };
        var targetPopup = OwnedTree.Find<Popup>(target).ShouldNotBeNull();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => target.Submenu = shared);

        // Assert target's own submenu and popup stay exactly as they were
        target.Submenu.ShouldBeSameAs(existing);
        OwnedTree.Find<Popup>(target).ShouldBeSameAs(targetPopup);
        targetPopup.IsDisposed.ShouldBeFalse();

        // Assert owner's submenu is untouched by the rejected assignment
        owner.Submenu.ShouldBeSameAs(shared);
    }

    /// <summary>Verifies Shortcut derives ShortcutText's display text when no explicit text is set.</summary>
    [Fact]
    public void ShortcutText_WhenShortcutIsSetAndTextIsNot_DerivesDisplayText()
    {
        var item = new MenuItem { Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s')) };

        item.ShortcutText.ShouldBe("Ctrl+S");
    }

    /// <summary>Verifies an explicit ShortcutText assignment always wins over Shortcut's derived text.</summary>
    [Fact]
    public void ShortcutText_WhenExplicitlyAssigned_WinsOverShortcut()
    {
        var item = new MenuItem
        {
            Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s')),
            ShortcutText = "Custom"
        };

        item.ShortcutText.ShouldBe("Custom");

        item.Shortcut = new KeyGesture(Code.F5);

        item.ShortcutText.ShouldBe("Custom");
    }

    /// <summary>Verifies clearing Shortcut with no explicit text leaves ShortcutText null.</summary>
    [Fact]
    public void ShortcutText_WhenShortcutIsClearedAndTextIsUnset_IsNull()
    {
        var item = new MenuItem { Shortcut = new KeyGesture(Code.F5) };
        item.ShortcutText.ShouldBe("F5");

        item.Shortcut = null;

        item.ShortcutText.ShouldBeNull();
    }

    /// <summary>Verifies ShortcutText's derived text is memoized once on assignment, not recomputed per read.</summary>
    [Fact]
    public void ShortcutText_WhenReadRepeatedlyAfterShortcutIsSet_ReusesTheSameDerivedStringInstance()
    {
        var item = new MenuItem { Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s')) };

        var first = item.ShortcutText;
        var second = item.ShortcutText;

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies every vertical row shares one trailing shortcut edge.</summary>
    [Fact]
    public void Render_WhenVerticalItemsHaveDifferentShortcuts_RightAlignsEveryHint()
    {
        // Arrange
        var labelOnly = new MenuItem { Text = "Open Recent" };
        var shortHint = new MenuItem { Text = "Run", ShortcutText = "F5" };
        var longHint = new MenuItem { Text = "Save", ShortcutText = "Ctrl+S" };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(labelOnly);
        menu.Items.Add(shortHint);
        menu.Items.Add(longHint);
        var size = new Size(30, 3);
        new LayoutEngine().Layout(menu, size);
        using Frame frame = new(size);

        // Act
        menu.Render(frame.Canvas);

        // Assert
        menu.DesiredSize.Width.ShouldBe(19);
        labelOnly.Bounds.Width.ShouldBe(19);
        shortHint.Bounds.Right.ShouldBe(longHint.Bounds.Right);
        FrameOracle.Get(frame, new Point(11, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(12, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(shortHint.Bounds.Right - 2, 1)).ShouldBe("F");
        FrameOracle.Get(frame, new Point(longHint.Bounds.Right - 6, 2)).ShouldBe("C");
        FrameOracle.Get(frame, new Point(shortHint.Bounds.Right - 1, 1)).ShouldBe("5");
        FrameOracle.Get(frame, new Point(longHint.Bounds.Right - 1, 2)).ShouldBe("S");
    }

    private static KeyEventArgs Key(Code code, Modifiers modifiers = Modifiers.None) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static KeyEventArgs Space(KeyAction action, Modifiers modifiers = Modifiers.None) => new(new Stroke(
        Code.Character,
        new Rune(' '),
        nativeCode: 0,
        modifiers,
        action));

    private static KeyEventArgs Tab(Modifiers modifiers = Modifiers.None) => new(new Stroke(
        Code.Tab,
        default,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    /// <summary>Verifies changing a checked item to command clears checked state before observers.</summary>
    [Fact]
    public void Kind_WhenCheckedItemBecomesCommand_StagesUncheckedStateBeforeNotification()
    {
        var item = new MenuItem { Kind = MenuItemKind.Check, IsChecked = true };
        var observed = false;
        item.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.Kind))
            {
                item.Kind.ShouldBe(MenuItemKind.Command);
                item.IsChecked.ShouldBeFalse();
                observed = true;
            }
        };

        item.Kind = MenuItemKind.Command;

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies moving a checked radio item resolves its destination before GroupName publication.</summary>
    [Fact]
    public void GroupName_WhenCheckedRadioMoves_ResolvesDestinationBeforePropertyNotification()
    {
        var menu = new Menu();
        var first = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "a", IsChecked = true };
        var second = new MenuItem { Kind = MenuItemKind.Radio, GroupName = "b", IsChecked = true };
        menu.Items.Add(first);
        menu.Items.Add(second);
        var observed = false;
        first.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MenuItem.GroupName))
            {
                first.IsChecked.ShouldBeTrue();
                second.IsChecked.ShouldBeFalse();
                observed = true;
            }
        };

        first.GroupName = "b";

        observed.ShouldBeTrue();
    }

    /// <summary>Verifies a rejected insertion leaves the candidate's IsFocusable and IsTabStop unchanged.</summary>
    [Fact]
    public void Items_WhenMenuItemInsertionFails_LeavesCandidateFocusableAndTabStopUnchanged()
    {
        var menu = new Menu();
        var item = new MenuItem { Text = "Open" };
        item.Dispose();

        // A disposed candidate fails insertion before any of this menu's
        // private presentation policy applies.
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Add(item));

        item.IsFocusable.ShouldBeTrue();
        item.IsTabStop.ShouldBeTrue();
        menu.ItemCount.ShouldBe(0);
    }

    /// <summary>Verifies disposing an item before the selected index publishes SelectedItem, since
    /// the slot SelectedIndex still points at now holds a different sibling that shifted into it -
    /// not just SelectedIndex's own untouched numeric value. Before this fix, disposing
    /// an owned item directly repaired nothing but a visual repaint: SelectedItem's identity moved
    /// silently, with no notification a data-bound consumer could observe.</summary>
    [Fact]
    public void Dispose_WhenItemBeforeSelectedIndexIsDisposedDirectly_NotifiesSelectedItemIdentityChange()
    {
        var a = new MenuItem { Text = "A" };
        var b = new MenuItem { Text = "B" };
        var c = new MenuItem { Text = "C" };
        var menu = new Menu();
        menu.Items.Add(a);
        menu.Items.Add(b);
        menu.Items.Add(c);
        menu.SelectedIndex = 1;
        var notifications = new List<string>();
        menu.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName!);

        a.Dispose();

        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeSameAs(c);
        notifications.ShouldContain(nameof(Menu.SelectedItem));
    }

    /// <summary>Verifies disposing an item before the selected index, where the reclaimed slot
    /// now holds a MenuSeparator rather than a MenuItem, still publishes SelectedItem - the
    /// MenuItem pattern the visual-cursor repair alone uses would otherwise skip a separator
    /// entirely, leaving neither a notification nor a visible cursor.</summary>
    [Fact]
    public void Dispose_WhenReclaimedSelectedSlotHoldsASeparator_StillNotifiesSelectedItem()
    {
        var a = new MenuItem { Text = "A" };
        var b = new MenuItem { Text = "B" };
        var separator = new MenuSeparator();
        var menu = new Menu();
        menu.Items.Add(a);
        menu.Items.Add(b);
        menu.Items.Add(separator);
        menu.SelectedIndex = 1;
        var notifications = new List<string>();
        menu.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName!);

        a.Dispose();

        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeNull();
        notifications.ShouldContain(nameof(Menu.SelectedItem));
    }

    /// <summary>Verifies removing the selected entry, where a MenuSeparator slides into its vacated
    /// slot, does not throw - the same reclaimed-slot hazard as
    /// <see cref="Dispose_WhenReclaimedSelectedSlotHoldsASeparator_StillNotifiesSelectedItem"/>, but
    /// reached through the ordinary Items.Remove API instead of MenuItem.Dispose(), which exercises
    /// Select's own outgoing-slot read directly rather than OnItemControlsChanged's disposal branch.
    /// Before this fix, Select's hard cast of the outgoing slot threw InvalidCastException the
    /// moment a plain removal left a separator in the old selected index.</summary>
    [Fact]
    public void Remove_WhenSuccessorSeparatorSlidesIntoSelectedSlot_DoesNotThrowAndRepairsSelection()
    {
        var a = new MenuItem { Text = "A" };
        var separator = new MenuSeparator();
        var b = new MenuItem { Text = "B" };
        var menu = new Menu();
        menu.Items.Add(a);
        menu.Items.Add(separator);
        menu.Items.Add(b);
        menu.SelectedIndex = 0;

        _ = Should.NotThrow(() => menu.Items.Remove(a));

        menu.Items.ShouldBe([separator, b]);
        menu.SelectedItem.ShouldBeSameAs(b);
    }

    /// <summary>Verifies an authored Width survives attachment: only Height is a semantic requirement
    /// (menu rows are exactly one cell tall), so Width must never be clobbered to Auto.</summary>
    [Fact]
    public void Items_WhenMenuItemIsAdded_NeverMutatesAuthoredWidth()
    {
        var menu = new Menu();
        var item = new MenuItem
        {
            Text = "Open",
            Width = Length.Cells(30)
        };

        menu.Items.Add(item);

        item.Width.ShouldBe(Length.Cells(30));
        item.Height.ShouldBe(Length.Cells(1));
    }

    /// <summary>Verifies removal restores the item's authored IsFocusable, IsTabStop, Width, and Height.</summary>
    [Fact]
    public void Items_WhenMenuItemIsRemoved_RestoresAuthoredWidthHeightFocusableAndTabStop()
    {
        var menu = new Menu();
        var item = new MenuItem
        {
            Text = "Open",
            Width = Length.Cells(30),
            Height = Length.Cells(3),
            IsFocusable = false,
            IsTabStop = false
        };
        menu.Items.Add(item);

        _ = menu.Items.Remove(item);

        item.Width.ShouldBe(Length.Cells(30));
        item.Height.ShouldBe(Length.Cells(3));
        item.IsFocusable.ShouldBeFalse();
        item.IsTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies inserting before the selected entry shifts SelectedIndex without changing selection.</summary>
    [Fact]
    public void Insert_WhenIndexPrecedesSelection_ShiftsSelectedIndexPreservingIdentity()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.SelectedIndex = 1;
        var inserted = new MenuItem { Text = "Inserted" };

        menu.Items.Insert(0, inserted);

        menu.SelectedIndex.ShouldBe(2);
        menu.SelectedItem.ShouldBeSameAs(second);
        menu.Items.ShouldBe([inserted, first, second]);
    }

    /// <summary>Verifies Insert accepts a typed MenuSeparator at a position.</summary>
    [Fact]
    public void Insert_WhenGivenSeparator_PlacesItAtRequestedPosition()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        var separator = new MenuSeparator();

        menu.Items.Insert(1, separator);

        menu.Items.ShouldBe([first, separator, second]);
    }

    /// <summary>Verifies an out-of-range insertion index throws before mutating the collection.</summary>
    [Fact]
    public void Insert_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var menu = new Menu();
        var item = new MenuItem { Text = "First" };
        menu.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => menu.Items.Insert(2, new MenuItem { Text = "New" }));

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies RemoveAt detaches the entry at a position and repairs selection to the
    /// entry that slid into the vacated slot - the immediate successor, not the first entry in
    /// the menu. A 3-entry fixture cannot distinguish "successor" from "wrap to front" since they
    /// coincide at that size, so this uses four.</summary>
    [Fact]
    public void RemoveAt_WhenSelectedEntryIsRemoved_RepairsSelectionToNearestAvailable()
    {
        var first = new MenuItem { Text = "First" };
        var selected = new MenuItem { Text = "Selected" };
        var successor = new MenuItem { Text = "Successor" };
        var fourth = new MenuItem { Text = "Fourth" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(successor);
        menu.Items.Add(fourth);
        menu.SelectedIndex = 1;

        menu.Items.RemoveAt(1);

        menu.Items.ShouldBe([first, successor, fourth]);
        menu.SelectedItem.ShouldBeSameAs(successor);
    }

    /// <summary>Verifies that when a removed selected entry's successor slides into the exact same
    /// index - the collision FindNearest's inclusive scan produces whenever an available MenuItem
    /// takes the vacated slot - the identity change is still committed and published. Select's own
    /// "_selectedIndex == index" guard treats that collision as a no-op, so before this fix
    /// SelectedItem's identity moved from the removed entry to its successor with no
    /// PropertyChanged(SelectedItem), no CommitSelection(false) on the outgoing entry, and no
    /// CommitSelection(true) on the incoming one - only the co-located index-based SelectedItem
    /// assertion in RemoveAt_WhenSelectedEntryIsRemoved_RepairsSelectionToNearestAvailable passed,
    /// masking the gap.</summary>
    [Fact]
    public void RemoveAt_WhenSuccessorSlidesIntoSelectedSlot_PublishesIdentityChangeAndCommitsSelection()
    {
        var first = new MenuItem { Text = "First" };
        var selected = new MenuItem { Text = "Selected" };
        var successor = new MenuItem { Text = "Successor" };
        var fourth = new MenuItem { Text = "Fourth" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(successor);
        menu.Items.Add(fourth);
        menu.SelectedIndex = 1;
        var notifications = new List<string>();
        menu.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName!);

        menu.Items.RemoveAt(1);

        menu.Items.ShouldBe([first, successor, fourth]);
        menu.SelectedItem.ShouldBeSameAs(successor);
        notifications.ShouldContain(nameof(Menu.SelectedItem));
    }

    /// <summary>Verifies a reentrant SelectedItem subscriber that removes the very successor which
    /// just slid into the selected slot does not leave RemoveEntry's outer frame re-selecting that
    /// now-detached entry off a stale local reference. The nested RemoveEntry call correctly moves
    /// selection on to a third entry and decommits the successor; before this fix, the outer frame
    /// would resume afterward and unconditionally call CommitSelection(ContainsFocus) on its own
    /// stale `current` (the successor), re-marking a detached zombie entry as selected. Requires a
    /// focused menu - ContainsFocus is false for an unmounted one, which would make the incorrect
    /// re-commit indistinguishable from a correct no-op.</summary>
    [Fact]
    public async Task RemoveAt_WhenSelectedItemSubscriberReentrantlyRemovesTheSuccessor_DoesNotReselectTheDetachedEntryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var first = new MenuItem { Text = "First" };
            var selected = new MenuItem { Text = "Selected" };
            var successor = new MenuItem { Text = "Successor" };
            var fourth = new MenuItem { Text = "Fourth" };
            var menu = new Menu();
            menu.Items.Add(first);
            menu.Items.Add(selected);
            menu.Items.Add(successor);
            menu.Items.Add(fourth);
            menu.SelectedIndex = 1;
            new LayoutEngine().Layout(menu, new Size(12, 4));
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            var reentered = false;
            menu.PropertyChanged += (_, eventArgs) =>
            {
                if (reentered ||
                    eventArgs.PropertyName != nameof(Menu.SelectedItem) ||
                    !ReferenceEquals(menu.SelectedItem, successor))
                {
                    return;
                }

                reentered = true;
                _ = menu.Items.Remove(successor);
            };

            menu.Items.RemoveAt(1);

            reentered.ShouldBeTrue();
            menu.Items.ShouldBe([first, fourth]);
            menu.SelectedItem.ShouldBeSameAs(fourth);
            var isSelectedFact = typeof(ControlBase).GetProperty(
                "IsSelectedFact",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            _ = isSelectedFact.ShouldNotBeNull();
            ((bool) isSelectedFact.GetValue(successor)!).ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing the last entry while it is selected falls back to the nearest
    /// predecessor instead of wrapping to the first entry in the menu.</summary>
    [Fact]
    public void RemoveAt_WhenSelectedEntryIsLastAndRemoved_FallsBackToNearestPredecessor()
    {
        var first = new MenuItem { Text = "First" };
        var predecessor = new MenuItem { Text = "Predecessor" };
        var selected = new MenuItem { Text = "Selected" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(predecessor);
        menu.Items.Add(selected);
        menu.SelectedIndex = 2;

        menu.Items.RemoveAt(2);

        menu.Items.ShouldBe([first, predecessor]);
        menu.SelectedItem.ShouldBeSameAs(predecessor);
    }

    /// <summary>Verifies removing an entry after the selected one leaves the selection's identity
    /// and index untouched - the removal has nothing to do with the selection.</summary>
    [Fact]
    public void RemoveAt_WhenEntryAfterSelectionIsRemoved_LeavesSelectionUntouched()
    {
        var first = new MenuItem { Text = "First" };
        var selected = new MenuItem { Text = "Selected" };
        var third = new MenuItem { Text = "Third" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;
        var parentChanges = 0;
        selected.ParentChanged += (_, _) => parentChanges++;

        menu.Items.RemoveAt(2);

        menu.Items.ShouldBe([first, selected]);
        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeSameAs(selected);
        parentChanges.ShouldBe(0);
    }

    /// <summary>Verifies removing an entry before the selected one preserves the selection's
    /// identity, silently shifting only its numeric index - mirroring InsertItem's symmetric
    /// case.</summary>
    [Fact]
    public void RemoveAt_WhenEntryBeforeSelectionIsRemoved_PreservesIdentityAndShiftsIndex()
    {
        var first = new MenuItem { Text = "First" };
        var selected = new MenuItem { Text = "Selected" };
        var third = new MenuItem { Text = "Third" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;

        menu.Items.RemoveAt(0);

        menu.Items.ShouldBe([selected, third]);
        menu.SelectedIndex.ShouldBe(0);
        menu.SelectedItem.ShouldBeSameAs(selected);
    }

    /// <summary>Verifies removing a MenuSeparator - which can never be selected - never moves the
    /// highlight, the cleanest demonstration that the repair must key off what is selected rather
    /// than the removed index alone.</summary>
    [Fact]
    public void Remove_WhenSeparatorIsRemoved_NeverMovesSelection()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var separator = new MenuSeparator();
        var third = new MenuItem { Text = "Third" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.Items.Add(separator);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;

        _ = menu.Items.Remove(separator);

        menu.Items.ShouldBe([first, second, third]);
        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies an out-of-range removal index throws before mutating the collection.</summary>
    [Fact]
    public void RemoveAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var menu = new Menu();
        var item = new MenuItem { Text = "First" };
        menu.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.Items.RemoveAt(1));

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies the indexer replaces the selected entry, detaching the old one without disposal.</summary>
    [Fact]
    public void Indexer_WhenSelectedEntryIsReplaced_DetachesOldWithoutDisposalAndSelectsReplacement()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.SelectedIndex = 0;
        var replacement = new MenuItem { Text = "Replacement" };

        menu.Items[0] = replacement;

        menu.Items.ShouldBe([replacement, second]);
        menu.SelectedItem.ShouldBeSameAs(replacement);
        first.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeNull();
    }

    /// <summary>Verifies replacing the sole selected entry with a MenuSeparator - leaving no other
    /// selectable entry - still publishes the cleared selection. ReplaceEntry force-sets
    /// _selectedIndex to -1 before computing the repaired target; when that target is also -1,
    /// routing through Select's own "_selectedIndex == index" guard would silently return before
    /// its PropertyChanged notifications, and _selectedEntry would keep pointing at the just-detached
    /// old item.</summary>
    [Fact]
    public void Indexer_WhenSelectedItemIsReplacedBySeparatorWithNoOtherSelectableEntry_PublishesClearedSelection()
    {
        var selected = new MenuItem { Text = "Selected" };
        var menu = new Menu();
        menu.Items.Add(selected);
        menu.SelectedIndex = 0;
        var notifications = new List<string>();
        menu.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName!);
        var separator = new MenuSeparator();

        menu.Items[0] = separator;

        menu.Items.ShouldBe([separator]);
        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
        notifications.ShouldContain(nameof(Menu.SelectedIndex));
        notifications.ShouldContain(nameof(Menu.SelectedItem));
        selected.IsDisposed.ShouldBeFalse();
        selected.Parent.ShouldBeNull();
    }

    /// <summary>Verifies the indexer rejects a replacement that is not a MenuItem or MenuSeparator.</summary>
    [Fact]
    public void Indexer_WhenReplacementIsNotAnEntry_ThrowsAndLeavesCollectionUnchanged()
    {
        var menu = new Menu();
        var item = new MenuItem { Text = "First" };
        menu.Items.Add(item);

        _ = Should.Throw<InvalidOperationException>(() => menu.Items[0] = new Button());

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies assigning null through the indexer throws.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_Throws()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "First" });

        _ = Should.Throw<ArgumentNullException>(() => menu.Items[0] = null!);
    }

    /// <summary>Verifies Move repositions an owned entry while preserving the selected item's identity.</summary>
    [Fact]
    public void Move_WhenSelectedEntryMoves_PreservesIdentityAndUpdatesSelectedIndex()
    {
        var first = new MenuItem { Text = "First" };
        var selected = new MenuItem { Text = "Selected" };
        var third = new MenuItem { Text = "Third" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;

        menu.Items.Move(1, 2);

        menu.Items.ShouldBe([first, third, selected]);
        menu.SelectedIndex.ShouldBe(2);
        menu.SelectedItem.ShouldBeSameAs(selected);
    }

    /// <summary>Verifies an out-of-range move index throws before mutating the collection.</summary>
    [Fact]
    public void Move_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.Items.Move(0, 2));

        menu.Items.ShouldBe([first, second]);
    }

    /// <summary>Verifies IndexOf reports the current position of an owned entry and -1 for a foreign one.</summary>
    [Fact]
    public void IndexOf_WhenItemIsOwnedOrForeign_ReportsPositionOrNegativeOne()
    {
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        var foreign = new MenuItem { Text = "Foreign" };

        menu.Items.IndexOf(second).ShouldBe(1);
        menu.Items.IndexOf(foreign).ShouldBe(-1);
    }

    /// <summary>Verifies IndexOf rejects a null candidate.</summary>
    [Fact]
    public void IndexOf_WhenItemIsNull_ThrowsArgumentNullException()
    {
        var menu = new Menu();

        _ = Should.Throw<ArgumentNullException>(() => menu.Items.IndexOf(null!));
    }

    /// <summary>Verifies Add rejects a null MenuItem or MenuSeparator without mutating the collection.</summary>
    [Fact]
    public void Add_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        var menu = new Menu();

        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Add((MenuItem) null!));
        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Add((MenuSeparator) null!));
        menu.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies Insert rejects a null MenuItem or MenuSeparator without mutating the collection.</summary>
    [Fact]
    public void Insert_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        var menu = new Menu();
        var item = new MenuItem { Text = "First" };
        menu.Items.Add(item);

        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Insert(0, (MenuItem) null!));
        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Insert(0, (MenuSeparator) null!));
        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies Remove rejects a null MenuItem or MenuSeparator.</summary>
    [Fact]
    public void Remove_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        var menu = new Menu();

        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Remove((MenuItem) null!));
        _ = Should.Throw<ArgumentNullException>(() => menu.Items.Remove((MenuSeparator) null!));
    }

    /// <summary>Verifies Remove reports false and leaves the collection untouched for an item or
    /// separator this menu does not own, matching IndexOf's own foreign-candidate contract.</summary>
    [Fact]
    public void Remove_WhenEntryIsForeign_ReturnsFalseAndLeavesCollectionUnchanged()
    {
        var owned = new MenuItem { Text = "Owned" };
        var menu = new Menu();
        menu.Items.Add(owned);
        var foreignItem = new MenuItem { Text = "Foreign" };
        var foreignSeparator = new MenuSeparator();

        menu.Items.Remove(foreignItem).ShouldBeFalse();
        menu.Items.Remove(foreignSeparator).ShouldBeFalse();

        menu.Items.ShouldBe([owned]);
    }

    /// <summary>Verifies Clear removes every owned entry, restores each one's authored
    /// presentation, and resets selection to -1 - the same repair RemoveAt applies per entry.</summary>
    [Fact]
    public void Clear_WhenCalled_RemovesEveryEntryRestoresPresentationAndClearsSelection()
    {
        var first = new MenuItem { Text = "First", IsFocusable = false, IsTabStop = false };
        var separator = new MenuSeparator();
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(separator);
        menu.Items.Add(second);
        menu.SelectedIndex = 2;

        menu.Items.Clear();

        menu.Items.Count.ShouldBe(0);
        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
        first.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        first.IsFocusable.ShouldBeFalse();
        first.IsTabStop.ShouldBeFalse();
        second.Parent.ShouldBeNull();
        second.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies disposed collection mutations reject Insert, RemoveAt, indexer assignment, and Move.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsInsertRemoveAtIndexerAndMove()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "First" });
        menu.Items.Add(new MenuItem { Text = "Second" });
        menu.Dispose();

        _ = Should.Throw<ObjectDisposedException>(
            () => menu.Items.Insert(0, new MenuItem { Text = "New" }));
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(
            () => menu.Items[0] = new MenuItem { Text = "New" });
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Move(0, 1));
    }

    /// <summary>Verifies Clear and Remove on a disposed menu throw ObjectDisposedException like
    /// every other documented mutator, instead of Clear throwing InvalidOperationException from
    /// deep inside ItemsControl (the item presentation host is unavailable once disposed) and
    /// Remove doing the same - both skipped the VerifyMutable check every other mutator in this
    /// same test already exercises.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsClearAndRemove()
    {
        var first = new MenuItem { Text = "First" };
        var separator = new MenuSeparator();
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(separator);
        menu.Dispose();

        _ = Should.Throw<ObjectDisposedException>(menu.Items.Clear);
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Remove(first));
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Remove(separator));
    }

    #region Session entry and transitions

    /// <summary>Verifies an armed sibling switch preserves one menu-rooted dismissing scope.</summary>
    [Fact]
    public async Task Pointer_WhenSiblingMainMenuItemIsSelected_ReusesOneModalScopeAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        ModalScope? scopeAtPopupExposure = null;
        filePopup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && filePopup.IsOpen)
            {
                scopeAtPopupExposure = surface.Application.Modality.Active;
                scopeAtPopupExposure.ShouldNotBeNull().Root.ShouldBeSameAs(menu);
            }
        };

        // Act
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);

        // Assert
        scope.Root.ShouldBeSameAs(menu);
        scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);
        scopeAtPopupExposure.ShouldBeSameAs(scope);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a command row can temporarily own an armed menu without ending its scope.</summary>
    [Fact]
    public async Task Pointer_WhenArmedSelectionMovesThroughCommand_ReopensInSameModalScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var command = new MenuItem { Text = "Exit" };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(command);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act command row and back
        await surface.Pointer.MoveToAsync(command);

        // Assert armed command row
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        await surface.Pointer.MoveToAsync(file);

        // Assert reopened sibling
        popup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies activating the already-open top item ends both the visual and modal session.</summary>
    [Fact]
    public async Task PerformInvoke_WhenTopSubmenuIsAlreadyOpen_ClosesCompleteSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top submenu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "toggle top submenu closed");

        // Assert
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies activating an already-open nested item closes only its branch and retains the top scope.</summary>
    [Fact]
    public async Task PerformInvoke_WhenNestedSubmenuIsAlreadyOpen_ClosesBranchAndRetainsSessionAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Text = "Leaf" });
        var nested = new MenuItem { Text = "Nested", Submenu = deepestMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top submenu for nested toggle");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.UpdateAsync(nested.PerformInvoke, "open nested submenu");

        // Act
        await surface.UpdateAsync(nested.PerformInvoke, "toggle nested submenu closed");

        // Assert
        nestedPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies leaving a hover-opened top anchor makes a later click toggle the complete session closed.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedTopAnchorIsLeftAndClickedLater_ClosesCompleteSessionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var copy = new MenuItem { Text = "Copy" };
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(copy);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(copy);

        // Act
        await surface.Pointer.ClickAsync(edit);

        // Assert
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies leaving a hover-opened nested anchor makes a later click close only that branch.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedNestedAnchorIsLeftAndClickedLater_ClosesBranchOnlyAsync()
    {
        // Arrange
        var leaf = new MenuItem { Text = "Today" };
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(leaf);
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(recent);
        recentPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(leaf);

        // Act
        await surface.Pointer.ClickAsync(recent);

        // Assert
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies a consumed move outside the menu plane expires a top anchor's one-shot click.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedTopAnchorIsLeftOutsidePlaneAndClickedLater_ClosesCompleteSessionAsync()
    {
        // Arrange
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        Overlay.SetTop(background, Length.Cells(7));
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(background);
        edit.IsPointerOver.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act
        await surface.Pointer.ClickAsync(edit);

        // Assert
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a consumed move outside the menu plane expires a nested anchor's one-shot click.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedNestedAnchorIsLeftOutsidePlaneAndClickedLater_ClosesBranchOnlyAsync()
    {
        // Arrange
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        Overlay.SetTop(background, Length.Cells(8));
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "Today" });
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(recent);
        recentPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(background);
        recent.IsPointerOver.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act
        await surface.Pointer.ClickAsync(recent);

        // Assert
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies an unhandled wheel over a sibling top-level item completes menu dismissal.</summary>
    [Fact]
    public async Task Pointer_WhenWheelCannotScrollArmedMenu_ClosesSessionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        menu.SelectedIndex.ShouldBe(1);
        var wheelPoint = await surface.ResolvePointAsync(file);
        var wheelReport = Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<64;{wheelPoint.X + 1};{wheelPoint.Y + 1}M"));
        await surface.SendAsync(wheelReport, "wheel over the same menu bar's sibling top-level item");

        // Assert
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    #endregion

    #region Escape and outside dismissal

    /// <summary>Verifies a menu without an armed session leaves Escape for its containing Window.</summary>
    [Fact]
    public async Task Escape_WhenMenuHasNoOpenSession_BubblesToWindowCancelButtonAsync()
    {
        // Arrange
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(new MenuItem { Text = "File" });
        var cancel = new Button
        {
            Text = "Cancel",
            IsCancel = true,
        };
        var content = new Stack { Orientation = Orientation.Vertical };
        content.Children.Add(menu);
        content.Children.Add(cancel);
        var window = new Window { Content = content };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var cancellations = 0;
        cancel.Click += (_, _) => cancellations++;
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(menu).ShouldBeTrue(),
            "focus standalone menu in Window");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        cancellations.ShouldBe(1);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies Escape removes one deepest popup at a time before ending the root session.</summary>
    [Fact]
    public async Task Escape_WhenSubmenuDepthExceedsThree_ClosesOneLevelThenRootSessionAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Text = "Leaf" });
        var deepest = new MenuItem { Text = "Third", Submenu = deepestMenu };
        var secondMenu = new Menu { Orientation = Orientation.Vertical };
        secondMenu.Items.Add(deepest);
        var second = new MenuItem { Text = "Second", Submenu = secondMenu };
        var firstMenu = new Menu { Orientation = Orientation.Vertical };
        firstMenu.Items.Add(second);
        var first = new MenuItem { Text = "First", Submenu = firstMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(first);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(60, 15),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(first).ShouldNotBeNull();
        var secondPopup = OwnedTree.Find<Popup>(second).ShouldNotBeNull();
        var deepestPopup = OwnedTree.Find<Popup>(deepest).ShouldNotBeNull();

        // Act open every retained level
        await surface.Pointer.ClickAsync(first);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.MoveToAsync(deepest);

        // Assert complete plane
        firstPopup.IsOpen.ShouldBeTrue();
        secondPopup.IsOpen.ShouldBeTrue();
        deepestPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act and assert one level per Escape
        await surface.Keyboard.PressAsync(Code.Escape);
        deepestPopup.IsOpen.ShouldBeFalse();
        secondPopup.IsOpen.ShouldBeTrue();
        firstPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        secondPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        firstPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies outside press and wheel each dismiss without reaching the exposed background.</summary>
    [Fact]
    public async Task OutsideInput_WhenMenuSessionIsOpen_DismissesWithoutBackgroundInteractionAsync()
    {
        // Arrange
        var activations = 0;
        var wheelRoutes = 0;
        var background = new Button
        {
            Text = "Background",
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        background.Click += (_, _) => activations++;
        _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                wheelRoutes++;
            }
        });
        Overlay.SetTop(background, Length.Cells(6));
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus menu background");
        await surface.UpdateAsync(file.PerformInvoke, "open menu session for outside press");
        var pressScope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act outside press
        await surface.Pointer.ClickAsync(background);

        // Assert consumed dismissal
        pressScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        activations.ShouldBe(0);

        // Act outside wheel in a fresh session
        await surface.UpdateAsync(file.PerformInvoke, "open menu session for outside wheel");
        var wheelScope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.WheelAsync(background, default, wheelY: 1);

        // Assert consumed dismissal
        wheelScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        wheelRoutes.ShouldBe(0);
        activations.ShouldBe(0);
    }

    #endregion

    #region Failure recovery and reentrancy

    /// <summary>Verifies a submenu queued during entry replaces its sibling before its surface is exposed.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryCallbackQueuesSibling_ReplaysCompleteSiblingTransitionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var queued = false;
        var fileWasOpenWhenEditExposed = false;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (!queued && ReferenceEquals(eventArgs.Current, menu))
            {
                queued = true;
                edit.PerformInvoke();
            }
        };
        editPopup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && editPopup.IsOpen)
            {
                fileWasOpenWhenEditExposed = filePopup.IsOpen;
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "queue sibling during modal entry");

        // Assert
        queued.ShouldBeTrue();
        fileWasOpenWhenEditExposed.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(menu);
    }

    /// <summary>Verifies failed modal entry discards a submenu queued from the failing focus callback.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryCallbackQueuesSubmenuThenThrows_DiscardsQueuedOpenAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The modal-entry focus callback failed.");
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var callbackCalls = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (callbackCalls == 0 && ReferenceEquals(eventArgs.Current, menu))
            {
                callbackCalls++;
                edit.PerformInvoke();
                throw expected;
            }
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(file.PerformInvoke).ShouldBeSameAs(expected),
            "fail menu modal entry after queuing another submenu");

        // Assert
        callbackCalls.ShouldBe(1);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an entry callback cannot replay a submenu after invalidating its retained ownership.</summary>
    /// <param name="mutation">The structural or availability mutation applied after queuing the sibling.</param>
    [Theory]
    [InlineData("removed")]
    [InlineData("replaced-null")]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("menu-detached")]
    public async Task PerformInvoke_WhenQueuedSiblingBecomesInvalid_DiscardsTransitionAndSessionAsync(
        string mutation)
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        var root = new Overlay { Children = { menu } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var callbackCalls = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (callbackCalls != 0 || !ReferenceEquals(eventArgs.Current, menu))
            {
                return;
            }

            callbackCalls++;
            edit.PerformInvoke();

            switch (mutation)
            {
                case "removed":
                    _ = menu.Items.Remove(edit);
                    break;
                case "replaced-null":
                    edit.Submenu = null;
                    break;
                case "disabled":
                    edit.IsEnabled = false;
                    break;
                case "hidden":
                    edit.Visibility = Visibility.Hidden;
                    break;
                case "menu-detached":
                    _ = root.Children.Remove(menu);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown queued submenu mutation '{mutation}'.");
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "invalidate queued sibling during modal entry");

        // Assert
        callbackCalls.ShouldBe(1);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an early failing submenu observer cannot suppress parent propagation or cleanup.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEarlyNestedMenuSubscriberThrows_PropagatesAndClosesBeforeRethrowAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The early nested menu subscriber failed.");
        var leaf = new MenuItem { Text = "Leaf" };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(leaf);
        submenu.ItemInvoked += (_, _) => throw expected;
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var rootInvocations = 0;
        menu.ItemInvoked += (_, eventArgs) =>
        {
            eventArgs.Item.ShouldBeSameAs(leaf);
            rootInvocations++;
        };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open nested failure menu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(leaf.PerformInvoke).ShouldBeSameAs(expected),
            "invoke leaf through early failing nested subscriber");

        // Assert
        rootInvocations.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies late nested observers run before the outer invocation transaction closes the chain.</summary>
    [Fact]
    public async Task PerformInvoke_WhenLateNestedSubscriberThrows_ObservesOpenChainThenClosesAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The late nested menu subscriber failed.");
        var leaf = new MenuItem { Text = "Leaf" };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(leaf);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open menu for late invocation observer");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var observedOpen = false;
        submenu.ItemInvoked += (_, _) =>
        {
            observedOpen = popup.IsOpen && scope.IsActive;
            throw expected;
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(leaf.PerformInvoke).ShouldBeSameAs(expected),
            "invoke leaf through late failing nested observer");

        // Assert
        observedOpen.ShouldBeTrue();
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies external scope disposal closes visuals and leaves the menu reusable.</summary>
    [Fact]
    public async Task Dispose_WhenMenuScopeEndsExternally_ClosesVisualsAndAllowsLaterSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open externally ended menu session");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act external exit
        await surface.UpdateAsync(first.Dispose, "dispose menu scope externally");

        // Assert complete visual cleanup
        first.IsActive.ShouldBeFalse();
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act reopen
        await surface.UpdateAsync(file.PerformInvoke, "reopen menu after external scope exit");
        var second = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert fresh reusable session
        second.ShouldNotBeSameAs(first);
        second.IsActive.ShouldBeTrue();
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies deepest-first visual cleanup attempts every callback and preserves the first failure.</summary>
    [Fact]
    public async Task Dispose_WhenNestedCloseAndExitCallbacksThrow_CompletesCleanupAndPreservesEarliestFailureAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Text = "Leaf" });
        var nested = new MenuItem { Text = "Nested", Submenu = deepestMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var expected = new InvalidOperationException("The deepest close callback failed.");
        var order = new List<string>();
        nestedPopup.Closing += (_, _) =>
        {
            order.Add("deepest");
            throw expected;
        };
        firstPopup.Closing += (_, _) =>
        {
            order.Add("first");
            throw new InvalidOperationException("The first close callback failed.");
        };
        scope.Exited += (_, _) =>
        {
            order.Add("scope");
            throw new InvalidOperationException("The scope callback failed.");
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(scope.Dispose).ShouldBeSameAs(expected),
            "dispose menu scope with failing cleanup callbacks");

        // Assert
        order.ShouldBe(["deepest", "first", "scope"]);
        nestedPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a close requested from modal-entry focus waits until the scope handle is tracked.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryFocusCallbackClosesSession_DoesNotExposeOrStrandScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var command = new MenuItem { Text = "Close" };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(command);
        var invocations = 0;
        command.Invoked += (_, _) => invocations++;
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Current, menu))
            {
                command.PerformInvoke();
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "close menu from modal-entry focus callback");

        // Assert
        invocations.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an old scope exit callback may open a replacement without stale identity cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenExitedCallbackReopens_TracksReplacementByIdentityAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open first identity session");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();
        ModalScope? replacement = null;
        first.Exited += (_, _) =>
        {
            file.PerformInvoke();
            replacement = surface.Application.Modality.Active;
        };

        // Act
        await surface.UpdateAsync(first.Dispose, "replace session from old exit callback");

        // Assert
        first.IsActive.ShouldBeFalse();
        replacement.ShouldNotBeNull().IsActive.ShouldBeTrue();
        replacement.ShouldNotBeSameAs(first);
        surface.Application.Modality.Active.ShouldBeSameAs(replacement);
        popup.IsOpen.ShouldBeTrue();
    }

    #endregion

    #region Availability and parent modality

    /// <summary>Verifies submenu close completion releases its original owner after the anchor is reparented.</summary>
    [Fact]
    public async Task Escape_WhenClosingCallbackReparentsAnchor_ReleasesOriginalSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var other = new Menu { Orientation = Orientation.Horizontal };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open menu before close-time reparenting");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var reparented = false;
        popup.Closing += (_, _) =>
        {
            if (!reparented)
            {
                reparented = true;
                menu.Items.Remove(file).ShouldBeTrue();
                other.Items.Add(file);
            }
        };

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close submenu while its anchor is reparented");

        // Assert
        reparented.ShouldBeTrue();
        other.Items.ShouldContain(file);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies deep menu teardown uses bounded stack depth while closing every retained popup.</summary>
    [Fact]
    public void PerformInvoke_WhenMenuChainIsDeep_UsesBoundedTeardownStack()
    {
        // Arrange
        const int depth = 128;
        var leaf = new MenuItem { Text = "Leaf" };
        var child = new Menu { Orientation = Orientation.Vertical };
        child.Items.Add(leaf);
        var anchors = new List<MenuItem>(depth);
        var popups = new List<Popup>(depth);

        for (var index = 0; index < depth; index++)
        {
            var anchor = new MenuItem
            {
                Text = $"Level {index}",
                Submenu = child,
            };
            var parent = new Menu { Orientation = Orientation.Vertical };
            parent.Items.Add(anchor);
            anchors.Add(anchor);
            popups.Add(OwnedTree.Find<Popup>(anchor).ShouldNotBeNull());
            child = parent;
        }

        anchors.Reverse();
        foreach (var anchor in anchors)
        {
            anchor.PerformInvoke();
        }

        var recursiveFrames = -1;
        popups[0].Closing += (_, _) =>
        {
            recursiveFrames = new StackTrace().GetFrames().Count(
                frame => string.Equals(
                    frame.GetMethod()?.Name,
                    "CloseOpenSubmenus",
                    StringComparison.Ordinal));
        };

        // Act
        leaf.PerformInvoke();

        // Assert
        recursiveFrames.ShouldBeLessThanOrEqualTo(1);
        popups.ShouldAllBe(popup => !popup.IsOpen);
    }

    /// <summary>Verifies loss of the primary menu root closes every retained popup and the scope.</summary>
    [Fact]
    public async Task Visibility_WhenPrimaryMenuBecomesUnavailable_ClosesCompleteSessionAsync()
    {
        // Arrange
        var nestedMenu = new Menu { Orientation = Orientation.Vertical };
        nestedMenu.Items.Add(new MenuItem { Text = "Leaf" });
        var nested = new MenuItem { Text = "Nested", Submenu = nestedMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => menu.Visibility = Visibility.Hidden, "hide primary menu root");

        // Assert
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        firstPopup.IsOpen.ShouldBeFalse();
        nestedPopup.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies replacing an open submenu preserves its scope while removal ends the session.</summary>
    [Fact]
    public async Task Submenu_WhenOpenValueIsReplacedAndRemoved_DoesNotStrandModalSessionAsync()
    {
        // Arrange
        var firstSubmenu = new Menu { Orientation = Orientation.Vertical };
        firstSubmenu.Items.Add(new MenuItem { Text = "First" });
        var file = new MenuItem { Text = "File", Submenu = firstSubmenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var replacement = new Menu { Orientation = Orientation.Vertical };
        replacement.Items.Add(new MenuItem { Text = "Replacement" });

        // Act replace while open
        await surface.UpdateAsync(() => file.Submenu = replacement, "replace open submenu");
        var replacementPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();

        // Assert replacement keeps exact session
        firstPopup.IsDisposed.ShouldBeTrue();
        firstSubmenu.IsDisposed.ShouldBeFalse();
        replacementPopup.ShouldNotBeSameAs(firstPopup);
        replacementPopup.IsOpen.ShouldBeTrue();
        replacementPopup.Content.ShouldBeSameAs(replacement);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act remove while replacement remains open
        await surface.UpdateAsync(() => file.Submenu = null, "remove open submenu");

        // Assert complete teardown
        replacementPopup.IsDisposed.ShouldBeTrue();
        replacement.IsDisposed.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies unexpected nested-menu unavailability cannot leave an armed scope without a live chain.</summary>
    [Fact]
    public async Task Visibility_WhenOpenNestedMenuBecomesUnavailable_ClosesCompleteSessionAsync()
    {
        // Arrange
        var nestedMenu = new Menu { Orientation = Orientation.Vertical };
        nestedMenu.Items.Add(new MenuItem { Text = "Leaf" });
        var nested = new MenuItem { Text = "Nested", Submenu = nestedMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => nestedMenu.Visibility = Visibility.Hidden,
            "hide open nested menu content");

        // Assert
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        firstPopup.IsOpen.ShouldBeFalse();
        nestedPopup.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies every unavailable top anchor ends its complete session without later resurrection.</summary>
    /// <param name="mutation">The availability or ownership transition applied to the open anchor.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("removed")]
    [InlineData("cleared")]
    [InlineData("disposed")]
    public async Task Availability_WhenOpenTopAnchorBecomesUnavailable_ClosesSessionWithoutResurrectionAsync(
        string mutation)
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top anchor before making it unavailable");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => MakeAnchorUnavailable(menu, file, mutation),
            $"make open top anchor {mutation}");

        // Assert complete teardown
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            file.IsDisposed.ShouldBeTrue();
            popup.IsDisposed.ShouldBeTrue();
            menu.Items.ShouldNotContain(file);
        }
        else
        {
            popup.IsDisposed.ShouldBeFalse();

            // Act restore the same retained anchor
            await surface.UpdateAsync(
                () => RestoreAnchor(menu, file, mutation),
                $"restore unavailable top anchor after {mutation}");

            // Assert restoration does not resurrect stale popup or scope state
            menu.Items.ShouldContain(file);
            popup.IsOpen.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();

            // Act and assert an explicit later activation owns one fresh reusable session
            await surface.UpdateAsync(file.PerformInvoke, "explicitly reopen restored top anchor");
            var replacement = surface.Application.Modality.Active.ShouldNotBeNull();
            replacement.ShouldNotBeSameAs(scope);
            popup.IsOpen.ShouldBeTrue();
            await surface.UpdateAsync(file.PerformInvoke, "close replacement top session");
            replacement.IsActive.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();
        }
    }

    /// <summary>Verifies every unavailable nested anchor ends the exact complete top session.</summary>
    /// <param name="mutation">The availability or ownership transition applied to the open anchor.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("removed")]
    [InlineData("cleared")]
    [InlineData("disposed")]
    public async Task Availability_WhenOpenNestedAnchorBecomesUnavailable_ClosesCompleteSessionWithoutResurrectionAsync(
        string mutation)
    {
        // Arrange
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "Today" });
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent for unavailable nested anchor");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before making it unavailable");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => MakeAnchorUnavailable(fileMenu, recent, mutation),
            $"make open nested anchor {mutation}");

        // Assert complete top-session teardown
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            recent.IsDisposed.ShouldBeTrue();
            recentPopup.IsDisposed.ShouldBeTrue();
            fileMenu.Items.ShouldNotContain(recent);
        }
        else
        {
            recentPopup.IsDisposed.ShouldBeFalse();
            await surface.UpdateAsync(
                () => RestoreAnchor(fileMenu, recent, mutation),
                $"restore unavailable nested anchor after {mutation}");
            fileMenu.Items.ShouldContain(recent);
        }

        // Act reopen only the parent branch
        await surface.UpdateAsync(file.PerformInvoke, "explicitly reopen parent after nested unavailability");
        var replacement = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert stale nested state does not resurrect
        replacement.ShouldNotBeSameAs(scope);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeFalse();
        await surface.UpdateAsync(file.PerformInvoke, "close replacement parent session");
        replacement.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies press cleanup cannot redirect teardown away from the captured original session.</summary>
    [Fact]
    public async Task Visibility_WhenPressedOpenAnchorReparentsDuringCleanup_ClosesCapturedOriginalSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var other = new Menu { Orientation = Orientation.Horizontal };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open pressed anchor before cleanup reparenting");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(menu).ShouldBeTrue(),
            "return focus to top menu before holding its anchor");
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        file.IsPressed.ShouldBeTrue();
        var reparented = false;
        file.PropertyChanged += (_, eventArgs) =>
        {
            if (!reparented && eventArgs.PropertyName == nameof(ControlBase.IsPressed) && !file.IsPressed)
            {
                reparented = true;
                menu.Items.Remove(file).ShouldBeTrue();
                other.Items.Add(file);
            }
        };

        // Act
        await surface.UpdateAsync(() => file.Visibility = Visibility.Hidden, "hide pressed open anchor");

        // Assert
        reparented.ShouldBeTrue();
        other.Items.ShouldContain(file);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        await surface.UpdateAsync(() => file.Visibility = Visibility.Visible, "restore reparented anchor visibility");
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies close failures remain authoritative after complete nested-anchor cleanup.</summary>
    [Fact]
    public async Task IsEnabled_WhenOpenNestedAnchorCleanupCallbacksThrow_PreservesEarliestFailureAfterTeardownAsync()
    {
        // Arrange
        var closeFailure = new InvalidOperationException("The unavailable anchor close callback failed.");
        var propertyFailure = new InvalidOperationException("The unavailable anchor property callback failed.");
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "Today" });
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before failing nested unavailability");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before failing unavailability");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var closeCallbacks = 0;
        var propertyCallbacks = 0;
        recentPopup.Closing += (_, _) =>
        {
            closeCallbacks++;
            throw closeFailure;
        };
        recent.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.IsEnabled))
            {
                propertyCallbacks++;
                throw propertyFailure;
            }
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(() => recent.IsEnabled = false)
                .ShouldBeSameAs(closeFailure),
            "disable nested anchor with failing cleanup callbacks");

        // Assert
        closeCallbacks.ShouldBe(1);
        propertyCallbacks.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a top anchor becoming unavailable from its closing callback ends the captured session.</summary>
    /// <param name="mutation">The reentrant availability transition applied by the callback.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task IsOpen_WhenClosingCallbackMakesTopAnchorUnavailable_ClosesCapturedSessionAsync(
        string mutation)
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top anchor before reentrant closing mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var callbacks = 0;
        popup.Closing += (_, _) =>
        {
            callbacks++;
            MakeAnchorUnavailable(menu, file, mutation);
        };

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, $"close top popup while anchor becomes {mutation}");

        // Assert complete exact-owner cleanup
        callbacks.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            file.IsDisposed.ShouldBeTrue();
            popup.IsDisposed.ShouldBeTrue();
            menu.Items.ShouldNotContain(file);
        }
        else
        {
            await surface.UpdateAsync(
                () => RestoreAnchor(menu, file, mutation),
                $"restore top anchor after closing callback made it {mutation}");
            popup.IsOpen.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();
        }
    }

    /// <summary>Verifies a nested anchor becoming unavailable from its closing callback ends the top session.</summary>
    /// <param name="mutation">The reentrant availability transition applied by the callback.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task IsOpen_WhenClosingCallbackMakesNestedAnchorUnavailable_ClosesCompleteSessionAsync(
        string mutation)
    {
        // Arrange
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "Today" });
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before reentrant nested closing mutation");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before reentrant closing mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var callbacks = 0;
        recentPopup.Closing += (_, _) =>
        {
            callbacks++;
            MakeAnchorUnavailable(fileMenu, recent, mutation);
        };

        // Act
        await surface.UpdateAsync(
            () => recentPopup.IsOpen = false,
            $"close nested popup while anchor becomes {mutation}");

        // Assert complete top-session cleanup
        callbacks.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            recent.IsDisposed.ShouldBeTrue();
            recentPopup.IsDisposed.ShouldBeTrue();
            fileMenu.Items.ShouldNotContain(recent);
        }
        else
        {
            await surface.UpdateAsync(
                () => RestoreAnchor(fileMenu, recent, mutation),
                $"restore nested anchor after closing callback made it {mutation}");
            fileMenu.Items.ShouldContain(recent);
        }

        // Act reopen only the parent to prove stale nested state cannot resurrect
        await surface.UpdateAsync(file.PerformInvoke, "reopen parent after reentrant nested unavailability");
        var replacement = surface.Application.Modality.Active.ShouldNotBeNull();
        replacement.ShouldNotBeSameAs(scope);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeFalse();
        await surface.UpdateAsync(file.PerformInvoke, "close replacement parent session");
        replacement.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies focus-restoration mutation retains its first failure after complete session cleanup.</summary>
    [Fact]
    public async Task IsOpen_WhenFocusRestorationDisablesNestedAnchor_PreservesFirstFailureAfterTeardownAsync()
    {
        // Arrange
        var focusFailure = new InvalidOperationException("The focus-restoration callback failed.");
        var closedFailure = new InvalidOperationException("The later closed callback failed.");
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "Today" });
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before focus-restoration mutation");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before focus-restoration mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var focusCallbacks = 0;
        var closedCallbacks = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (focusCallbacks == 0 && ReferenceEquals(eventArgs.Current, fileMenu))
            {
                focusCallbacks++;
                recent.IsEnabled = false;
                throw focusFailure;
            }
        };
        recentPopup.Closed += (_, _) =>
        {
            closedCallbacks++;
            throw closedFailure;
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(() => recentPopup.IsOpen = false)
                .ShouldBeSameAs(focusFailure),
            "close nested popup through failing focus-restoration mutation");

        // Assert
        focusCallbacks.ShouldBe(1);
        closedCallbacks.ShouldBe(1);
        recent.IsEnabled.ShouldBeFalse();
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        await surface.UpdateAsync(() => recent.IsEnabled = true, "restore nested anchor after focus failure");
        recentPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a menu session nests under a modal Window and restores its parent scope.</summary>
    [Fact]
    public async Task CloseChain_WhenMenuIsInsideModalWindow_RestoresWindowScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var window = new Window
        {
            Content = menu,
            Visibility = Visibility.Collapsed,
            Width = Length.Cells(24),
            Height = Length.Cells(8),
        };
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        ModalScope? windowScope = null;
        await surface.UpdateAsync(
            () => windowScope = window.ShowModal(initialFocus: menu),
            "show modal Window containing menu");
        await surface.UpdateAsync(file.PerformInvoke, "open nested menu plane");
        var menuScope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert nested child scope
        menuScope.ShouldNotBeSameAs(windowScope);
        menuScope.Root.ShouldBeSameAs(menu);
        windowScope.ShouldNotBeNull().IsActive.ShouldBeTrue();

        // Act close popup level, then root session
        await surface.Keyboard.PressAsync(Code.Escape);
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert parent scope restored
        menuScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);
        windowScope.IsActive.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);
        surface.ShouldHaveFocus(menu);
    }

    private static void MakeAnchorUnavailable(Menu menu, MenuItem anchor, string mutation)
    {
        switch (mutation)
        {
            case "hidden":
                anchor.Visibility = Visibility.Hidden;
                break;
            case "disabled":
                anchor.IsEnabled = false;
                break;
            case "removed":
                menu.Items.Remove(anchor).ShouldBeTrue();
                break;
            case "cleared":
                menu.Items.Clear();
                break;
            case "disposed":
                anchor.Dispose();
                break;
            default:
                throw new InvalidOperationException($"Unknown menu-anchor mutation '{mutation}'.");
        }
    }

    private static void RestoreAnchor(Menu menu, MenuItem anchor, string mutation)
    {
        switch (mutation)
        {
            case "hidden":
                anchor.Visibility = Visibility.Visible;
                break;
            case "disabled":
                anchor.IsEnabled = true;
                break;
            case "removed":
            case "cleared":
                menu.Items.Add(anchor);
                break;
            default:
                throw new InvalidOperationException($"Unknown restorable menu-anchor mutation '{mutation}'.");
        }
    }

    #endregion
}
