// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves Menu selection bookkeeping under collection reordering, keyboard edge cases,
/// dormant hover, radio adoption, builder replay, and style validation.</summary>
public sealed class MenuInteractionTests
{
    #region Collection reordering

    /// <summary>Verifies moving an entry across the selected item shifts the selected index in
    /// the right direction while preserving the selected item's identity.</summary>
    [Theory]
    [InlineData(2, 0, 3, 1)]
    [InlineData(1, 3, 0, 2)]
    [InlineData(2, 2, 2, 2)]
    [InlineData(0, 0, 3, 3)]
    public async Task Move_WhenEntriesReorderAroundSelection_PreservesSelectedItemIdentityAsync(
        int selectedIndex,
        int oldIndex,
        int newIndex,
        int expectedSelectedIndex)
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var items = new[]
            {
                new MenuItem { Text = "A" },
                new MenuItem { Text = "B" },
                new MenuItem { Text = "C" },
                new MenuItem { Text = "D" }
            };

            foreach (var item in items)
            {
                menu.Items.Add(item);
            }

            menu.Attach(dispatcher);
            menu.SelectedIndex = selectedIndex;
            var selected = menu.SelectedItem.ShouldNotBeNull();

            // Act
            menu.Items.Move(oldIndex, newIndex);

            // Assert
            menu.SelectedIndex.ShouldBe(expectedSelectedIndex);
            menu.SelectedItem.ShouldBeSameAs(selected);
            menu.Items[expectedSelectedIndex].ShouldBeSameAs(selected);
            menu.Items[newIndex].ShouldBeSameAs(items[oldIndex]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a move that does not cross the selection publishes no selection change,
    /// and a move onto the same index is a complete no-op.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public async Task Move_WhenSelectionIsNotCrossed_PublishesNoSelectionChangeAsync(int oldIndex, int newIndex)
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(new MenuItem { Text = "A" });
            menu.Items.Add(new MenuItem { Text = "B" });
            menu.Items.Add(new MenuItem { Text = "C" });
            menu.Items.Add(new MenuItem { Text = "D" });
            menu.Attach(dispatcher);
            menu.SelectedIndex = 3;
            var selected = menu.SelectedItem.ShouldNotBeNull();
            var notifications = new List<string?>();
            menu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            // Act
            menu.Items.Move(oldIndex, newIndex);

            // Assert
            menu.SelectedIndex.ShouldBe(3);
            menu.SelectedItem.ShouldBeSameAs(selected);
            notifications.ShouldNotContain(nameof(Menu.SelectedIndex));
            notifications.ShouldNotContain(nameof(Menu.SelectedItem));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies moving a separator across the selected item shifts the index but never
    /// changes which item is selected.</summary>
    [Fact]
    public async Task Move_WhenSeparatorCrossesSelection_ShiftsIndexOnlyAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var separator = new MenuSeparator();
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(separator);
            menu.Items.Add(new MenuItem { Text = "A" });
            menu.Items.Add(new MenuItem { Text = "B" });
            menu.Attach(dispatcher);
            menu.SelectedIndex = 1;
            var selected = menu.SelectedItem.ShouldNotBeNull();

            // Act
            menu.Items.Move(0, 2);

            // Assert
            menu.SelectedIndex.ShouldBe(0);
            menu.SelectedItem.ShouldBeSameAs(selected);
            menu.Items[2].ShouldBeSameAs(separator);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies replacing an entry that is not selected leaves the selection identity
    /// and index untouched and publishes no selection change.</summary>
    [Fact]
    public async Task Indexer_WhenUnselectedEntryIsReplaced_LeavesSelectionUntouchedAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var old = new MenuItem { Text = "A" };
            menu.Items.Add(old);
            menu.Items.Add(new MenuItem { Text = "B" });
            menu.Attach(dispatcher);
            menu.SelectedIndex = 1;
            var selected = menu.SelectedItem.ShouldNotBeNull();
            var notifications = new List<string?>();
            menu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            var replacement = new MenuItem { Text = "Z" };

            // Act
            menu.Items[0] = replacement;

            // Assert
            menu.Items[0].ShouldBeSameAs(replacement);
            old.Parent.ShouldBeNull();
            old.IsDisposed.ShouldBeFalse();
            menu.SelectedIndex.ShouldBe(1);
            menu.SelectedItem.ShouldBeSameAs(selected);
            notifications.ShouldNotContain(nameof(Menu.SelectedIndex));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies replacing the selected item with a separator moves the selection to the
    /// next available item, wrapping to the start when the replaced slot was last.</summary>
    [Fact]
    public async Task Indexer_WhenSelectedLastEntryBecomesSeparator_WrapsSelectionToFirstItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var first = new MenuItem { Text = "A" };
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(first);
            menu.Items.Add(new MenuItem { Text = "B" });
            menu.Items.Add(new MenuItem { Text = "C" });
            menu.Attach(dispatcher);
            menu.SelectedIndex = 2;

            // Act
            menu.Items[2] = new MenuSeparator();

            // Assert
            menu.SelectedIndex.ShouldBe(0);
            menu.SelectedItem.ShouldBeSameAs(first);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies replacing an entry with itself is a no-op: no detach, no notification,
    /// and the selection stays put.</summary>
    [Fact]
    public async Task Indexer_WhenEntryIsReplacedWithItself_IsNoOpAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "A" };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            var parentChanges = 0;
            item.ParentChanged += (_, _) => parentChanges++;
            var notifications = new List<string?>();
            menu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            // Act
            menu.Items[0] = item;

            // Assert
            parentChanges.ShouldBe(0);
            notifications.ShouldBeEmpty();
            menu.SelectedIndex.ShouldBe(0);
            menu.Items.IndexOf(item).ShouldBe(0);
            _ = item.Parent.ShouldNotBeNull();
            item.Dispatcher.ShouldBeSameAs(dispatcher);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies clearing an already empty menu publishes nothing and throws nothing.</summary>
    [Fact]
    public async Task Clear_WhenMenuIsEmpty_IsNoOpAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu();
            menu.Attach(dispatcher);
            var notifications = new List<string?>();
            menu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            // Act
            menu.Items.Clear();

            // Assert
            menu.Items.Count.ShouldBe(0);
            menu.SelectedIndex.ShouldBe(-1);
            notifications.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the collection indexer rejects an out-of-range read.</summary>
    [Fact]
    public void Indexer_WhenIndexIsOutOfRange_Throws()
    {
        // Arrange
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Text = "A" });

        // Act / Assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => _ = menu.Items[1]);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => _ = menu.Items[-1]);
    }

    #endregion

    #region Radio adoption

    /// <summary>Verifies adding an already-checked radio item into a group with a checked member
    /// clears the incumbent and publishes both changes.</summary>
    [Fact]
    public async Task Add_WhenCheckedRadioJoinsGroupWithCheckedMember_AdoptsTheNewcomerAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var incumbent = new MenuItem { Text = "One", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };
            menu.Items.Add(incumbent);
            menu.Attach(dispatcher);
            var published = new List<string>();
            incumbent.PropertyChanged += (_, args) => published.Add($"incumbent:{args.PropertyName}");
            var newcomer = new MenuItem { Text = "Two", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };
            newcomer.PropertyChanged += (_, args) => published.Add($"newcomer:{args.PropertyName}");

            // Act
            menu.Items.Add(newcomer);

            // Assert
            incumbent.IsChecked.ShouldBeFalse();
            newcomer.IsChecked.ShouldBeTrue();
            published.ShouldContain($"incumbent:{nameof(MenuItem.IsChecked)}");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies replacing an entry with an already-checked radio adopts it into the
    /// group the same way an insertion does.</summary>
    [Fact]
    public async Task Indexer_WhenCheckedRadioReplacesEntry_AdoptsTheReplacementAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            var incumbent = new MenuItem { Text = "One", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };
            menu.Items.Add(incumbent);
            menu.Items.Add(new MenuItem { Text = "Command" });
            menu.Attach(dispatcher);

            // Act
            menu.Items[1] = new MenuItem { Text = "Two", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };

            // Assert
            incumbent.IsChecked.ShouldBeFalse();
            ((MenuItem) menu.Items[1]).IsChecked.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unchecking a checked radio directly leaves its group with no checked
    /// member rather than re-checking a sibling.</summary>
    [Fact]
    public void IsChecked_WhenCheckedRadioIsUnchecked_LeavesGroupEmpty()
    {
        // Arrange
        var menu = new Menu { Orientation = Orientation.Vertical };
        var first = new MenuItem { Text = "One", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };
        var second = new MenuItem { Text = "Two", Kind = MenuItemKind.Radio, GroupName = "g" };
        menu.Items.Add(first);
        menu.Items.Add(second);

        // Act
        first.IsChecked = false;

        // Assert
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies converting a checked Check item to Radio joins the radio group and
    /// clears the group's previously checked member.</summary>
    [Fact]
    public void Kind_WhenCheckedCheckItemBecomesRadio_AdoptsIntoGroup()
    {
        // Arrange
        var menu = new Menu { Orientation = Orientation.Vertical };
        var radio = new MenuItem { Text = "Radio", Kind = MenuItemKind.Radio, GroupName = "g", IsChecked = true };
        var check = new MenuItem { Text = "Check", Kind = MenuItemKind.Check, GroupName = "g", IsChecked = true };
        menu.Items.Add(radio);
        menu.Items.Add(check);
        radio.IsChecked.ShouldBeTrue();

        // Act
        check.Kind = MenuItemKind.Radio;

        // Assert
        check.IsChecked.ShouldBeTrue();
        radio.IsChecked.ShouldBeFalse();
    }

    #endregion

    #region Keyboard edge cases

    /// <summary>Verifies Enter with no selection, or with a disabled selected item, is left
    /// unhandled and invokes nothing.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Enter_WhenNothingActivatable_IsUnhandledAsync(int selectedIndex)
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var invoked = 0;
            var menu = new Menu { Orientation = Orientation.Vertical };
            var disabled = new MenuItem { Text = "A", IsEnabled = false };
            disabled.Invoked += (_, _) => invoked++;
            menu.Items.Add(disabled);
            menu.ItemInvoked += (_, _) => invoked++;
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.SelectedIndex = selectedIndex;

            // Act
            var result = Router.Route(menu, Events.Key, Key(Code.Enter));

            // Assert
            result.IsHandled.ShouldBeFalse();
            invoked.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Space press and release with no selection are consumed without arming
    /// or invoking anything.</summary>
    [Fact]
    public async Task Space_WhenNothingIsSelected_IsConsumedWithoutInvokingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var invoked = 0;
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "A" };
            menu.Items.Add(item);
            menu.ItemInvoked += (_, _) => invoked++;
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.SelectedIndex = -1;

            // Act
            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release));

            // Assert
            press.IsHandled.ShouldBeTrue();
            release.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeFalse();
            invoked.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies moving the selection between a Space press and its release cancels the
    /// armed activation: the release is consumed, nothing is invoked, and no item stays pressed.</summary>
    [Fact]
    public async Task Space_WhenSelectionMovesBeforeRelease_CancelsArmedActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var invoked = new List<string>();
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Text = "A" };
            var second = new MenuItem { Text = "B" };
            menu.Items.Add(first);
            menu.Items.Add(second);
            menu.ItemInvoked += (_, args) => invoked.Add(args.Item.Text ?? string.Empty);
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            menu.SelectedIndex = 0;

            // Act
            _ = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            first.IsPressed.ShouldBeTrue();
            _ = Router.Route(menu, Events.Key, Key(Code.Down));
            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release));

            // Assert
            release.IsHandled.ShouldBeTrue();
            first.IsPressed.ShouldBeFalse();
            second.IsPressed.ShouldBeFalse();
            invoked.ShouldBeEmpty();
            menu.SelectedIndex.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an intermediate Space repeat is consumed without re-arming or invoking.</summary>
    [Fact]
    public async Task Space_WhenRepeatArrivesWhileHeld_IsConsumedWithoutInvokingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var invoked = 0;
            var menu = new Menu { Orientation = Orientation.Vertical };
            var item = new MenuItem { Text = "A" };
            menu.Items.Add(item);
            menu.ItemInvoked += (_, _) => invoked++;
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();

            // Act
            _ = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            var repeat = Router.Route(menu, Events.Key, Space(KeyAction.Repeat));

            // Assert
            repeat.IsHandled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();
            invoked.ShouldBe(0);

            // Act
            _ = Router.Route(menu, Events.Key, Space(KeyAction.Release));

            // Assert
            invoked.ShouldBe(1);
            item.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a repeated arrow keeps moving the selection while a repeated Enter never
    /// invokes: navigation repeats, activation does not.</summary>
    [Fact]
    public async Task Repeat_WhenNavigationAndEnterRepeat_MovesSelectionButDoesNotInvokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var invoked = 0;
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(new MenuItem { Text = "A" });
            menu.Items.Add(new MenuItem { Text = "B" });
            menu.Items.Add(new MenuItem { Text = "C" });
            menu.ItemInvoked += (_, _) => invoked++;
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();

            // Act
            var repeatDown = Router.Route(menu, Events.Key, Key(Code.Down, KeyAction.Repeat));
            menu.SelectedIndex.ShouldBe(1);
            _ = Router.Route(menu, Events.Key, Key(Code.Down, KeyAction.Repeat));
            var repeatEnter = Router.Route(menu, Events.Key, Key(Code.Enter, KeyAction.Repeat));

            // Assert
            repeatDown.IsHandled.ShouldBeTrue();
            menu.SelectedIndex.ShouldBe(2);
            repeatEnter.IsHandled.ShouldBeFalse();
            invoked.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Home and End with no available entry leave the selection untouched and
    /// the key unhandled.</summary>
    [Theory]
    [InlineData(Code.Home)]
    [InlineData(Code.End)]
    public async Task HomeEnd_WhenNoEntryIsAvailable_IsUnhandledAsync(Code code)
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var menu = new Menu { Orientation = Orientation.Vertical };
            menu.Items.Add(new MenuItem { Text = "A", IsEnabled = false });
            menu.Items.Add(new MenuSeparator());
            menu.Items.Add(new MenuItem { Text = "B", Visibility = Visibility.Collapsed });
            menu.Attach(dispatcher);
            using FocusManager focus = new(menu);
            focus.Focus(menu).ShouldBeTrue();
            var before = menu.SelectedIndex;

            // Act
            var result = Router.Route(menu, Events.Key, Key(code));

            // Assert
            result.IsHandled.ShouldBeFalse();
            menu.SelectedIndex.ShouldBe(before);
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Pointer and layout

    /// <summary>Verifies hovering a submenu item of a dormant menu selects it but neither opens
    /// the submenu nor enters modality; only after a click arms the session does hover switch.</summary>
    [Fact]
    public async Task Pointer_WhenHoveringDormantMenu_SelectsWithoutOpeningSubmenuAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "New" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Undo" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 1 };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(edit);

        // Assert
        menu.SelectedItem.ShouldBeSameAs(edit);
        edit.IsSubmenuOpen.ShouldBeFalse();
        file.IsSubmenuOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act
        await surface.Pointer.ClickAsync(edit);

        // Assert
        edit.IsSubmenuOpen.ShouldBeTrue();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(menu);

        // Act - the armed session now switches on hover
        await surface.Pointer.MoveToAsync(file);

        // Assert
        file.IsSubmenuOpen.ShouldBeTrue();
        edit.IsSubmenuOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies a wheel over another item never moves the selection.</summary>
    [Fact]
    public async Task Pointer_WhenWheelArrivesOverAnotherItem_LeavesSelectionUntouchedAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(first);
        menu.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        menu.SelectedItem.ShouldBeSameAs(first);
        var target = await surface.ResolvePointAsync(second, new Point(1, 0));

        // Act - a bare wheel record over the second item, with no preceding motion
        await surface.UpdateAsync(
            () => _ = surface.Application.Capture.Dispatch(new Pointer(
                target,
                pixels: null,
                Buttons.None,
                PointerAction.Wheel,
                wheelX: 0,
                wheelY: 1,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false)),
            "wheel over the second item");

        // Assert
        menu.SelectedItem.ShouldBeSameAs(first);
        second.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies vertical spacing separates rows by the configured number of cells.</summary>
    [Fact]
    public async Task Spacing_WhenVertical_SeparatesRowsAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
        var third = new MenuItem { Text = "Third" };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 2 };
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.Items.Add(third);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 9),
            TestContext.Current.CancellationToken);

        // Assert
        first.Bounds.Y.ShouldBe(0);
        second.Bounds.Y.ShouldBe(3);
        third.Bounds.Y.ShouldBe(6);
        surface.Cell(new Point(0, 3)).Text.ShouldBe("S");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies flipping the owning menu's orientation while a submenu is open moves the
    /// submenu to the edge that orientation documents: below a horizontal item, beside a vertical one.</summary>
    [Fact]
    public async Task Orientation_WhenChangedWhileSubmenuIsOpen_ReplacesSubmenuAgainstNewEdgeAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "New" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(new MenuItem { Text = "Edit" });
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        file.IsSubmenuOpen.ShouldBeTrue();
        popup.SurfaceBounds.Y.ShouldBe(file.Bounds.Bottom);

        // Act
        await surface.UpdateAsync(() => menu.Orientation = Orientation.Vertical, "flip to vertical while open");

        // Assert
        file.IsSubmenuOpen.ShouldBeTrue();
        popup.Placement.ShouldBe(PopupPlacement.Right);
        popup.SurfaceBounds.X.ShouldBe(file.Bounds.Right);
        popup.SurfaceBounds.Y.ShouldBe(file.Bounds.Y);
    }

    #endregion

    #region Events and builder

    /// <summary>Verifies a throwing Invoked subscriber does not stop the menu-level notification
    /// or the bound command, and its failure is rethrown once those stages completed.</summary>
    [Fact]
    public void PerformInvoke_WhenInvokedSubscriberThrows_StillNotifiesMenuAndRunsCommandThenRethrows()
    {
        // Arrange
        var order = new List<string>();
        var menu = new Menu { Orientation = Orientation.Vertical };
        var item = new MenuItem
        {
            Text = "A",
            Command = new ProbeCommand { Executing = _ => order.Add("command") }
        };
        item.Invoked += (_, _) => throw new InvalidOperationException("boom");
        menu.ItemInvoked += (_, _) => order.Add("menu");
        menu.Items.Add(item);

        // Act
        var failure = Should.Throw<InvalidOperationException>(item.PerformInvoke);

        // Assert
        failure.Message.ShouldBe("boom");
        order.ShouldBe(["menu", "command"]);
    }

    /// <summary>Verifies a builder replays into independent menus: two Build calls yield distinct
    /// item instances, and mutating one menu leaves the other untouched.</summary>
    [Fact]
    public void Build_WhenCalledTwice_ProducesIndependentMenus()
    {
        // Arrange
        var builder = MenuBuilder.Vertical()
            .Item("Open")
            .Separator()
            .Submenu("Recent", recent => recent.Item("One").Item("Two"));

        // Act
        var first = builder.Build();
        var second = builder.Build();

        // Assert
        first.ShouldNotBeSameAs(second);
        first.Items.Count.ShouldBe(3);
        second.Items.Count.ShouldBe(3);
        first.Items[0].ShouldNotBeSameAs(second.Items[0]);
        ((MenuItem) first.Items[2]).Submenu.ShouldNotBeNull().ShouldNotBeSameAs(((MenuItem) second.Items[2]).Submenu);

        first.Items.RemoveAt(0);

        first.Items.Count.ShouldBe(2);
        second.Items.Count.ShouldBe(3);
    }

    /// <summary>Verifies nested Submenu calls build a submenu inside a submenu.</summary>
    [Fact]
    public void Build_WhenSubmenusNest_BuildsNestedSurfaces()
    {
        // Act
        var menu = MenuBuilder.Vertical()
            .Submenu("Outer", outer => outer.Submenu("Inner", inner => inner.Item("Leaf")))
            .Build();

        // Assert
        var outerItem = (MenuItem) menu.Items[0];
        var outerMenu = outerItem.Submenu.ShouldNotBeNull();
        var innerItem = (MenuItem) outerMenu.Items[0];
        innerItem.Text.ShouldBe("Inner");
        var innerMenu = innerItem.Submenu.ShouldNotBeNull();
        ((MenuItem) innerMenu.Items[0]).Text.ShouldBe("Leaf");
        innerMenu.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies two radios built checked in one group leave only the last one checked.</summary>
    [Fact]
    public void Build_WhenTwoRadiosStartChecked_KeepsOnlyTheLastChecked()
    {
        // Act
        var menu = MenuBuilder.Vertical()
            .Radio("One", "g", isChecked: true)
            .Radio("Two", "g", isChecked: true)
            .Build();

        // Assert
        ((MenuItem) menu.Items[0]).IsChecked.ShouldBeFalse();
        ((MenuItem) menu.Items[1]).IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies a disabled built item never runs its callback on activation.</summary>
    [Fact]
    public void Build_WhenItemIsDisabled_NeverRunsCallback()
    {
        // Arrange
        var runs = 0;
        var menu = MenuBuilder.Vertical().Item("Locked", onInvoke: () => runs++, isEnabled: false).Build();
        var item = (MenuItem) menu.Items[0];

        // Act
        item.PerformInvoke();

        // Assert
        item.IsEnabled.ShouldBeFalse();
        runs.ShouldBe(0);
    }

    #endregion

    #region Style validation

    /// <summary>Verifies the affix gap accepts 0 through 4 and rejects anything outside.</summary>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void AffixGap_WhenSet_ValidatesRange(int gap, bool valid)
    {
        if (valid)
        {
            (MenuItemStyle.Default with { AffixGap = gap }).AffixGap.ShouldBe(gap);
        }
        else
        {
            _ = Should.Throw<ArgumentOutOfRangeException>(() => MenuItemStyle.Default with { AffixGap = gap });
        }

        MenuItemStyle.Default.AffixGap.ShouldBe(1);
    }

    /// <summary>Verifies every marker glyph and the separator glyph reject a wide rune.</summary>
    [Fact]
    public void Glyphs_WhenWide_Throw()
    {
        var wide = new Rune('界');

        _ = Should.Throw<ArgumentException>(() => MenuItemStyle.Default with { CheckedGlyph = wide });
        _ = Should.Throw<ArgumentException>(() => MenuItemStyle.Default with { UncheckedGlyph = wide });
        _ = Should.Throw<ArgumentException>(() => MenuItemStyle.Default with { RadioCheckedGlyph = wide });
        _ = Should.Throw<ArgumentException>(() => MenuItemStyle.Default with { RadioUncheckedGlyph = wide });
        _ = Should.Throw<ArgumentException>(() => MenuSeparatorStyle.Default with { Glyph = wide });
    }

    /// <summary>Verifies an empty shortcut text reserves no shortcut column, unlike a non-empty one.</summary>
    [Fact]
    public void ShortcutText_WhenEmpty_ReservesNoColumn()
    {
        // Arrange
        var plain = new MenuItem { Text = "Save" };
        var empty = new MenuItem { Text = "Save", ShortcutText = string.Empty };
        var shortcut = new MenuItem { Text = "Save", ShortcutText = "Ctrl+S" };
        var constraint = new Constraint(40, 1);

        // Act
        plain.Measure(constraint);
        empty.Measure(constraint);
        shortcut.Measure(constraint);

        // Assert
        empty.DesiredSize.Width.ShouldBe(plain.DesiredSize.Width);
        shortcut.DesiredSize.Width.ShouldBeGreaterThan(plain.DesiredSize.Width);
        empty.ShortcutColumnWidth.ShouldBe(0);
    }

    #endregion

    private static KeyEventArgs Key(Code code, KeyAction action = KeyAction.Press) =>
        new(new Stroke(code, default, nativeCode: 0, Modifiers.None, action));

    private static KeyEventArgs Space(KeyAction action) =>
        new(new Stroke(Code.Character, new Rune(' '), nativeCode: 0, Modifiers.None, action));
}
