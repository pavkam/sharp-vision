// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves menu entries and navigation through mounted terminal surfaces.</summary>
public sealed class MenuSurfaceTests
{
    /// <summary>Verifies root navigation, item press/release, separator exclusion, and cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(Menu),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(MenuItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup)]
    [ComponentBehaviorEvidence(
        typeof(MenuSeparator),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Input_WhenMenuNavigatesAndInvokes_UsesOwnedFacesWithoutFocusTheftAsync()
    {
        // Arrange
        var first = new MenuItem { Content = new ControlText("Open") };
        var separator = new MenuSeparator();
        var second = new MenuItem { Content = new ControlText("Save") };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        menu.Items.Add(first);
        menu.Items.Add(separator);
        menu.Items.Add(second);
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act and assert excluded separator hover
        await surface.Pointer.MoveToAsync(separator);
        separator.IsPointerOver.ShouldBeFalse();
        separator.IsFocused.ShouldBeFalse();

        // Act root focus and directional selection
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        menu.SelectedIndex.ShouldBe(2);
        surface.ShouldHaveFocus(menu);

        // Act held item face
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.PressAsync();
        second.IsPressed.ShouldBeTrue();
        second.IsFocused.ShouldBeFalse();
        menu.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(second);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert invocation
        invoked.ShouldBe(1);
        second.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(menu);

        // Act unavailable while held
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => second.IsEnabled = false, "disable held MenuItem");
        await surface.UpdateAsync(() => menu.IsEnabled = false, "disable focused Menu");

        // Assert cleanup
        second.IsPressed.ShouldBeFalse();
        menu.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies physical hover selects and paints the complete targeted menu row.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverItem_SelectsAndHighlightsTargetRowAsync()
    {
        // Arrange
        var first = new MenuItem { Content = new ControlText("First") };
        var second = new MenuItem { Content = new ControlText("Second") };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(first);
        menu.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var selectionBackground = menu.Theme.ShouldNotBeNull().Resolve(
            ThemeColor.From(ColorRole.SelectionBackground));

        // Act
        await surface.Pointer.MoveToAsync(second);

        // Assert
        menu.SelectedIndex.ShouldBe(1);
        second.IsPointerOver.ShouldBeTrue();
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(selectionBackground);
        surface.Cell(new Point(second.Bounds.Right - 1, 1)).Style.Background.ShouldBe(selectionBackground);
    }

    /// <summary>Verifies an armed menu switches sibling submenus on hover and Escape restores owner focus.</summary>
    [Fact]
    public async Task PointerAndEscape_WhenMenuChainIsOpen_SwitchesSiblingAndRestoresOwnerFocusAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var close = new MenuItem { Content = new ControlText("Close") };
        fileMenu.Items.Add(close);
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 1 };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();

        // Act open and hover-switch
        await surface.Pointer.ClickAsync(file);
        filePopup.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(fileMenu);

        // Act and assert submenu-item hover
        await surface.Pointer.MoveToAsync(close);
        close.IsPointerOver.ShouldBeTrue();
        fileMenu.SelectedIndex.ShouldBe(1);

        // Act switch top-level sibling
        await surface.Pointer.MoveToAsync(edit);

        // Assert switched chain
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
        menu.SelectedIndex.ShouldBe(1);
        surface.ShouldHaveFocus(editMenu);

        // Act close
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert focus restoration
        editPopup.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies moving an armed menu to a command closes the previous submenu without invoking it.</summary>
    [Fact]
    public async Task Pointer_WhenArmedMenuMovesToCommand_ClosesPreviousSubmenuAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var command = new MenuItem { Content = new ControlText("Exit") };
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 1 };
        menu.Items.Add(file);
        menu.Items.Add(command);
        var invocations = 0;
        command.Invoked += (_, _) => invocations++;
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        popup.IsOpen.ShouldBeTrue();

        // Act
        await surface.Pointer.MoveToAsync(command);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        menu.SelectedIndex.ShouldBe(1);
        invocations.ShouldBe(0);
    }

    /// <summary>Verifies horizontal keyboard navigation switches an already-open sibling submenu.</summary>
    [Fact]
    public async Task Keyboard_WhenSiblingSubmenuIsOpen_SwitchesPopupWithDirectionalSelectionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 1 };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        menu.SelectedIndex.ShouldBe(1);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(editMenu);
    }

    /// <summary>Verifies a descendant submenu press remains inside the complete popup chain until invocation.</summary>
    [Fact]
    public async Task Pointer_WhenNestedSubmenuCommandIsClicked_InvokesBeforeClosingPopupChainAsync()
    {
        // Arrange
        var invoked = 0;
        var command = new MenuItem { Content = new ControlText("Today") };
        command.Invoked += (_, _) => invoked++;
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(command);
        var recent = new MenuItem
        {
            Content = new ControlText("Open Recent"),
            Submenu = recentMenu,
        };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.ClickAsync(recent);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(command);

        // Assert
        invoked.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(menu);
    }
}
