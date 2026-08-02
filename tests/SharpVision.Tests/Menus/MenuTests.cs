// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies typed menu ownership, selection navigation, check states, and cells.</summary>
public sealed class MenuTests
{
    /// <summary>Verifies menus begin with a useful minimum while retaining inherited width configuration.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesConfigurableTenCellMinimumWidth()
    {
        // Arrange and act
        var menu = new Menu();

        // Assert default and validation-before-mutation
        menu.MinWidth.ShouldBe(10);
        menu.MaxWidth.ShouldBe(int.MaxValue);
        _ = Should.Throw<ArgumentException>(() => menu.MaxWidth = 9);
        menu.MaxWidth.ShouldBe(int.MaxValue);

        // Act and assert direct inherited configuration
        menu.MinWidth = 0;
        menu.MaxWidth = 24;
        menu.MinWidth.ShouldBe(0);
        menu.MaxWidth.ShouldBe(24);
    }

    /// <summary>Verifies typed collection ownership selects the first available item and renders compact shared-width rows.</summary>
    [ComponentUnitEvidence(typeof(Menu))]
    [ComponentUnitEvidence(typeof(MenuItem))]
    [ComponentUnitEvidence(typeof(MenuSeparator))]
    [Fact]
    public void Items_WhenAdded_UseTypedOwnershipSelectionAndVerticalCells()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        menu.Items.Add(
            new MenuItem { Content = new ControlText("Pinned"), Kind = MenuItemKind.Check, IsChecked = true });
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

    /// <summary>Verifies SelectedItem mirrors SelectedIndex and reports the selected item identity.</summary>
    [Fact]
    public void SelectedItem_WhenSet_UpdatesSelectedIndexAndReportsIdentity()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
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
        menu.Items.Add(new MenuItem { Content = new ControlText("First") });

        menu.SelectedItem = null;

        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies setting SelectedItem to an item this menu does not own clears selection.</summary>
    [Fact]
    public void SelectedItem_WhenItemIsNotOwned_ClearsSelection()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Content = new ControlText("First") });
        var foreign = new MenuItem { Content = new ControlText("Foreign") };

        menu.SelectedItem = foreign;

        menu.SelectedIndex.ShouldBe(-1);
        menu.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies directional keys skip separators while focus remains on the menu owner.</summary>
    [Fact]
    public async Task Dispatch_WhenDirectionalKeyArrives_SkipsSeparatorAndFocusesNextItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var first = new MenuItem { Content = new ControlText("First") };
            var separator = new MenuSeparator();
            var second = new MenuItem { Content = new ControlText("Second") };
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

    /// <summary>Verifies check and named radio items commit state before menu-level invocation reporting.</summary>
    [Fact]
    public void PerformInvoke_WhenCheckAndRadioItemsActivate_CommitsStateBeforeEvent()
    {
        var menu = new Menu();
        var check = new MenuItem { Content = new ControlText("Auto save"), Kind = MenuItemKind.Check };
        var first = new MenuItem
        {
            Content = new ControlText("Small"),
            Kind = MenuItemKind.Radio,
            GroupName = "size",
            IsChecked = true
        };
        var second = new MenuItem
        {
            Content = new ControlText("Large"),
            Kind = MenuItemKind.Radio,
            GroupName = "size"
        };
        List<string> observed = [];
        menu.Items.Add(check);
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.ItemInvoked += (_, eventArgs) =>
            observed.Add($"{eventArgs.Item.Content.ShouldBeOfType<ControlText>().Content}:{eventArgs.Item.IsChecked}");

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

    /// <summary>Verifies a separator is never focusable, hit-testable, selectable, or invokable.</summary>
    [Fact]
    public async Task MenuSeparator_WhenUsed_RemainsNonInteractiveAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu();
            var item = new MenuItem { Content = new ControlText("Open") };
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

    /// <summary>Verifies Tab and Shift+Tab move menu selection while private items remain outside traversal.</summary>
    [Fact]
    public async Task Dispatch_WhenTabPressed_MovesSelectionWithoutLeavingMenuAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var menu = new Menu { Orientation = Orientation.Vertical };
            var a = new MenuItem { Content = new ControlText("A") };
            var b = new MenuItem { Content = new ControlText("B") };
            var c = new MenuItem { Content = new ControlText("C") };
            var outside = new ProbeControl { Focusable = true };
            menu.Items.Add(a);
            menu.Items.Add(b);
            menu.Items.Add(c);
            root.Children.Add(menu);
            root.Children.Add(outside);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(menu).ShouldBeTrue();
            var next = Router.Route(menu, Events.Key, Tab());

            next.Handled.ShouldBeTrue();
            next.Command.ShouldBe(PostRouteCommand.None);
            menu.SelectedIndex.ShouldBe(1);
            focus.Focused.ShouldBeSameAs(menu);

            var previous = Router.Route(menu, Events.Key, Tab(Modifiers.Shift));

            previous.Handled.ShouldBeTrue();
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
            var first = new MenuItem { Content = new ControlText("First") };
            var second = new MenuItem { Content = new ControlText("Second") };
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
            result.Handled.ShouldBeTrue();
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
            var item = new MenuItem { Content = new ControlText("Run") };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();
            var invocations = new List<ActivationCause>();
            item.Invoked += (_, eventArgs) => invocations.Add(eventArgs.Cause);

            // Act and assert held state
            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.Handled.ShouldBeTrue();
            item.IsPressed.ShouldBeTrue();
            invocations.ShouldBeEmpty();

            // Act and assert completion
            var release = Router.Route(menu, Events.Key, Space(KeyAction.Release));
            release.Handled.ShouldBeTrue();
            item.IsPressed.ShouldBeFalse();
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
            var item = new MenuItem { Content = new ControlText("Run") };
            menu.Items.Add(item);
            menu.Attach(dispatcher);
            using var focus = new FocusManager(menu);
            focus.Focus(menu).ShouldBeTrue();

            var press = Router.Route(menu, Events.Key, Space(KeyAction.Press));
            press.Handled.ShouldBeTrue();

            item.Dispose();

            _ = Should.NotThrow(() => Router.Route(menu, Events.Key, Space(KeyAction.Release)));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies private menu items reject external focus.</summary>
    [Fact]
    public async Task Focus_WhenMenuItemReceivesExternalFocus_SyncsSelectedIndexAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var menu = new Menu { Orientation = Orientation.Vertical };
            var a = new MenuItem { Content = new ControlText("A") };
            var b = new MenuItem { Content = new ControlText("B") };
            var c = new MenuItem { Content = new ControlText("C") };
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
            var a = new MenuItem { Content = new ControlText("A") };
            var b = new MenuItem { Content = new ControlText("B") };
            var c = new MenuItem { Content = new ControlText("C") };
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
        horizontalSubmenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var horizontalItem = new MenuItem { Content = new ControlText("File"), Submenu = horizontalSubmenu };
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
        horizontalPopup.Face.Background.ShouldBe(ThemeColor.Surface);

        // Arrange vertical menu
        var verticalSubmenu = new Menu { Orientation = Orientation.Vertical };
        verticalSubmenu.Items.Add(new MenuItem { Content = new ControlText("Recent") });
        var verticalItem = new MenuItem { Content = new ControlText("Open"), Submenu = verticalSubmenu };
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
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var item = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var owner = new Menu { Orientation = Orientation.Horizontal };
        owner.Items.Add(item);
        _ = new Overlay { Children = { owner } };
        item.PerformInvoke();
        var submenuPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var contextMenuPopup = (Popup) contextMenu.Presentation;

        submenuPopup.Border.GlyphStyle.ShouldBe(contextMenuPopup.Border.GlyphStyle);
        submenuPopup.Border.Sides.ShouldBe(contextMenuPopup.Border.Sides);
        submenuPopup.Face.Background.ShouldBe(contextMenuPopup.Face.Background);
    }

    /// <summary>Verifies replacing a standalone item's submenu detaches the previous menu without
    /// disposing it, while the framework-owned popup that hosted it is disposed (see #181).</summary>
    [Fact]
    public void Submenu_WhenReplacedOnStandaloneItem_DetachesPreviousMenuWithoutDisposingIt()
    {
        // Arrange
        var previous = new Menu { Orientation = Orientation.Vertical };
        previous.Items.Add(new MenuItem { Content = new ControlText("First") });
        var item = new MenuItem { Content = new ControlText("File"), Submenu = previous };
        var previousPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();
        var replacement = new Menu { Orientation = Orientation.Vertical };
        replacement.Items.Add(new MenuItem { Content = new ControlText("Second") });

        // Act
        item.Submenu = replacement;

        // Assert the previous menu survives detached, while its framework popup is disposed
        previous.IsDisposed.ShouldBeFalse();
        previousPopup.IsDisposed.ShouldBeTrue();
        item.Submenu.ShouldBeSameAs(replacement);

        // Assert the detached menu can still be mutated and reassigned elsewhere
        previous.Items.Add(new MenuItem { Content = new ControlText("Reused") });
        var other = new MenuItem { Content = new ControlText("Edit"), Submenu = previous };
        other.Submenu.ShouldBeSameAs(previous);
    }

    /// <summary>Verifies clearing a standalone item's submenu detaches the previous menu without
    /// disposing it, while the framework-owned popup that hosted it is disposed (see #181).</summary>
    [Fact]
    public void Submenu_WhenClearedOnStandaloneItem_DetachesPreviousMenuWithoutDisposingIt()
    {
        // Arrange
        var previous = new Menu { Orientation = Orientation.Vertical };
        previous.Items.Add(new MenuItem { Content = new ControlText("First") });
        var item = new MenuItem { Content = new ControlText("File"), Submenu = previous };
        var previousPopup = OwnedTree.Find<Popup>(item).ShouldNotBeNull();

        // Act
        item.Submenu = null;

        // Assert
        previous.IsDisposed.ShouldBeFalse();
        previousPopup.IsDisposed.ShouldBeTrue();
        item.Submenu.ShouldBeNull();
    }

    /// <summary>Verifies assigning a menu already hosted as another item's submenu throws and
    /// leaves the target item's own submenu and popup untouched (see #180).</summary>
    [Fact]
    public void Submenu_WhenAlreadyHostedByAnotherItem_ThrowsAndLeavesTargetItemUnchanged()
    {
        // Arrange
        var shared = new Menu { Orientation = Orientation.Vertical };
        shared.Items.Add(new MenuItem { Content = new ControlText("Shared") });
        var owner = new MenuItem { Content = new ControlText("File"), Submenu = shared };
        var existing = new Menu { Orientation = Orientation.Vertical };
        existing.Items.Add(new MenuItem { Content = new ControlText("Existing") });
        var target = new MenuItem { Content = new ControlText("Edit"), Submenu = existing };
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
        var labelOnly = new MenuItem { Content = new ControlText("Open Recent") };
        var shortHint = new MenuItem { Content = new ControlText("Run"), ShortcutText = "F5" };
        var longHint = new MenuItem { Content = new ControlText("Save"), ShortcutText = "Ctrl+S" };
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

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static KeyEventArgs Space(KeyAction action) => new(new Stroke(
        Code.Character,
        new Rune(' '),
        nativeCode: 0,
        Modifiers.None,
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

    /// <summary>Verifies a rejected insertion leaves the candidate's Focusable and TabStop unchanged.</summary>
    [Fact]
    public void Items_WhenMenuItemInsertionFails_LeavesCandidateFocusableAndTabStopUnchanged()
    {
        var menu = new Menu();
        var item = new MenuItem { Content = new ControlText("Open") };
        item.Dispose();

        // A disposed candidate fails insertion before any of this menu's
        // private presentation policy applies.
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Add(item));

        item.Focusable.ShouldBeTrue();
        item.TabStop.ShouldBeTrue();
        menu.ItemCount.ShouldBe(0);
    }

    /// <summary>Verifies an authored Width survives attachment: only Height is a semantic requirement
    /// (menu rows are exactly one cell tall), so Width must never be clobbered to Auto.</summary>
    [Fact]
    public void Items_WhenMenuItemIsAdded_NeverMutatesAuthoredWidth()
    {
        var menu = new Menu();
        var item = new MenuItem
        {
            Content = new ControlText("Open"),
            Width = Length.Cells(30)
        };

        menu.Items.Add(item);

        item.Width.ShouldBe(Length.Cells(30));
        item.Height.ShouldBe(Length.Cells(1));
    }

    /// <summary>Verifies removal restores the item's authored Focusable, TabStop, Width, and Height.</summary>
    [Fact]
    public void Items_WhenMenuItemIsRemoved_RestoresAuthoredWidthHeightFocusableAndTabStop()
    {
        var menu = new Menu();
        var item = new MenuItem
        {
            Content = new ControlText("Open"),
            Width = Length.Cells(30),
            Height = Length.Cells(3),
            Focusable = false,
            TabStop = false
        };
        menu.Items.Add(item);

        _ = menu.Items.Remove(item);

        item.Width.ShouldBe(Length.Cells(30));
        item.Height.ShouldBe(Length.Cells(3));
        item.Focusable.ShouldBeFalse();
        item.TabStop.ShouldBeFalse();
    }

    /// <summary>Verifies inserting before the selected entry shifts SelectedIndex without changing selection.</summary>
    [Fact]
    public void Insert_WhenIndexPrecedesSelection_ShiftsSelectedIndexPreservingIdentity()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.SelectedIndex = 1;
        var inserted = new MenuItem { Content = new ControlText("Inserted") };

        menu.Items.Insert(0, inserted);

        menu.SelectedIndex.ShouldBe(2);
        menu.SelectedItem.ShouldBeSameAs(second);
        menu.Items.ShouldBe([inserted, first, second]);
    }

    /// <summary>Verifies Insert accepts a typed MenuSeparator at a position.</summary>
    [Fact]
    public void Insert_WhenGivenSeparator_PlacesItAtRequestedPosition()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
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
        var item = new MenuItem { Content = new ControlText("First") };
        menu.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => menu.Items.Insert(2, new MenuItem { Content = new ControlText("New") }));

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies RemoveAt detaches the entry at a position and repairs selection to the nearest available item.</summary>
    [Fact]
    public void RemoveAt_WhenSelectedEntryIsRemoved_RepairsSelectionToNearestAvailable()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var selected = new MenuItem { Content = new ControlText("Selected") };
        var third = new MenuItem { Content = new ControlText("Third") };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;

        menu.Items.RemoveAt(1);

        menu.Items.ShouldBe([first, third]);
        // FindAvailable searches forward from the removal point without including it, so
        // it wraps to the first item rather than landing on the immediate successor.
        menu.SelectedItem.ShouldBeSameAs(first);
    }

    /// <summary>Verifies removing an entry after the selected one leaves the selection's identity
    /// and index untouched - the removal has nothing to do with the selection (see #184).</summary>
    [Fact]
    public void RemoveAt_WhenEntryAfterSelectionIsRemoved_LeavesSelectionUntouched()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var selected = new MenuItem { Content = new ControlText("Selected") };
        var third = new MenuItem { Content = new ControlText("Third") };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(selected);
        menu.Items.Add(third);
        menu.SelectedIndex = 1;

        menu.Items.RemoveAt(2);

        menu.Items.ShouldBe([first, selected]);
        menu.SelectedIndex.ShouldBe(1);
        menu.SelectedItem.ShouldBeSameAs(selected);
    }

    /// <summary>Verifies removing an entry before the selected one preserves the selection's
    /// identity, silently shifting only its numeric index - mirroring InsertItem's symmetric
    /// case (see #184).</summary>
    [Fact]
    public void RemoveAt_WhenEntryBeforeSelectionIsRemoved_PreservesIdentityAndShiftsIndex()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var selected = new MenuItem { Content = new ControlText("Selected") };
        var third = new MenuItem { Content = new ControlText("Third") };
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
    /// than the removed index alone (see #184).</summary>
    [Fact]
    public void Remove_WhenSeparatorIsRemoved_NeverMovesSelection()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
        var separator = new MenuSeparator();
        var third = new MenuItem { Content = new ControlText("Third") };
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
        var item = new MenuItem { Content = new ControlText("First") };
        menu.Items.Add(item);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => menu.Items.RemoveAt(1));

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies the indexer replaces the selected entry, detaching the old one without disposal.</summary>
    [Fact]
    public void Indexer_WhenSelectedEntryIsReplaced_DetachesOldWithoutDisposalAndSelectsReplacement()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        menu.SelectedIndex = 0;
        var replacement = new MenuItem { Content = new ControlText("Replacement") };

        menu.Items[0] = replacement;

        menu.Items.ShouldBe([replacement, second]);
        menu.SelectedItem.ShouldBeSameAs(replacement);
        first.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeNull();
    }

    /// <summary>Verifies the indexer rejects a replacement that is not a MenuItem or MenuSeparator.</summary>
    [Fact]
    public void Indexer_WhenReplacementIsNotAnEntry_ThrowsAndLeavesCollectionUnchanged()
    {
        var menu = new Menu();
        var item = new MenuItem { Content = new ControlText("First") };
        menu.Items.Add(item);

        _ = Should.Throw<InvalidOperationException>(() => menu.Items[0] = new Button());

        menu.Items.ShouldBe([item]);
    }

    /// <summary>Verifies assigning null through the indexer throws.</summary>
    [Fact]
    public void Indexer_WhenAssignedNull_Throws()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Content = new ControlText("First") });

        _ = Should.Throw<ArgumentNullException>(() => menu.Items[0] = null!);
    }

    /// <summary>Verifies Move repositions an owned entry while preserving the selected item's identity.</summary>
    [Fact]
    public void Move_WhenSelectedEntryMoves_PreservesIdentityAndUpdatesSelectedIndex()
    {
        var first = new MenuItem { Content = new ControlText("First") };
        var selected = new MenuItem { Content = new ControlText("Selected") };
        var third = new MenuItem { Content = new ControlText("Third") };
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
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
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
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
        var menu = new Menu();
        menu.Items.Add(first);
        menu.Items.Add(second);
        var foreign = new MenuItem { Content = new ControlText("Foreign") };

        menu.Items.IndexOf(second).ShouldBe(1);
        menu.Items.IndexOf(foreign).ShouldBe(-1);
    }

    /// <summary>Verifies disposed collection mutations reject Insert, RemoveAt, indexer assignment, and Move.</summary>
    [Fact]
    public void Items_WhenOwnerIsDisposed_RejectsInsertRemoveAtIndexerAndMove()
    {
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Content = new ControlText("First") });
        menu.Items.Add(new MenuItem { Content = new ControlText("Second") });
        menu.Dispose();

        _ = Should.Throw<ObjectDisposedException>(
            () => menu.Items.Insert(0, new MenuItem { Content = new ControlText("New") }));
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.RemoveAt(0));
        _ = Should.Throw<ObjectDisposedException>(
            () => menu.Items[0] = new MenuItem { Content = new ControlText("New") });
        _ = Should.Throw<ObjectDisposedException>(() => menu.Items.Move(0, 1));
    }
}
