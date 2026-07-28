// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves menu entries and navigation through mounted terminal surfaces.</summary>
public sealed class MenuSurfaceTests
{
    /// <summary>Verifies a short submenu uses the shipped menu minimum before popup framing.</summary>
    [Fact]
    public async Task Submenu_WhenShortDefaultOpens_UsesTenCellMenuWidthAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("About") });
        var help = new MenuItem { Content = new ControlText("Help"), Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(help);

        // Assert
        submenu.MinWidth.ShouldBe(10);
        submenu.Bounds.Width.ShouldBe(10);
        popup.SurfaceBounds.Width.ShouldBe(12);
        surface.ShouldRender("""
            Help
            ┌──────────┐
            │About     │
            └──────────┘

            """);
    }

    /// <summary>Verifies an explicit submenu minimum controls the popup interior before framing.</summary>
    [Fact]
    public async Task Submenu_WhenMinimumWidthIsConfigured_UsesConfiguredInteriorWidthAsync()
    {
        // Arrange
        var submenu = new Menu
        {
            Orientation = Orientation.Vertical,
            MinWidth = 14,
            MaxWidth = 18
        };
        submenu.Items.Add(new MenuItem { Content = new ControlText("About") });
        var help = new MenuItem { Content = new ControlText("Help"), Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(24, 5),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(help);

        // Assert
        submenu.Bounds.Width.ShouldBe(14);
        popup.SurfaceBounds.Width.ShouldBe(16);
    }

    /// <summary>Verifies an explicit submenu maximum clips long content before popup framing.</summary>
    [Fact]
    public async Task Submenu_WhenMaximumWidthIsConfigured_ClampsLongContentAsync()
    {
        // Arrange
        var submenu = new Menu
        {
            Orientation = Orientation.Vertical,
            MinWidth = 0,
            MaxWidth = 6
        };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Documentation") });
        var help = new MenuItem { Content = new ControlText("Help"), Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(24, 5),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(help);

        // Assert
        submenu.Bounds.Width.ShouldBe(6);
        popup.SurfaceBounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies changing the inherited minimum remeasures an already-open submenu surface.</summary>
    [Fact]
    public async Task MinWidth_WhenOpenSubmenuChanges_RemeasuresRetainedPopupAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("About") });
        var help = new MenuItem { Content = new ControlText("Help"), Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(24, 5),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(help);
        popup.SurfaceBounds.Width.ShouldBe(12);

        // Act
        await surface.UpdateAsync(() => submenu.MinWidth = 14, "increase open submenu minimum width");

        // Assert
        submenu.Bounds.Width.ShouldBe(14);
        popup.SurfaceBounds.Width.ShouldBe(16);
    }

    /// <summary>Verifies a framed submenu clamps safely when its root is narrower than the default.</summary>
    [Fact]
    public async Task Submenu_WhenRootIsNarrowerThanDefault_ClampsFramedSurfaceAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("About") });
        var help = new MenuItem { Content = new ControlText("Help"), Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(8, 5),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(help);

        // Assert
        popup.SurfaceBounds.Width.ShouldBe(8);
        submenu.Bounds.Width.ShouldBe(6);
    }

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
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.RetainedPointerActivation |
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
        ComponentBehavior.PointerActivation |
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

        // Act keyboard activation
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert keyboard invocation
        invoked.ShouldBe(1);

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
        invoked.ShouldBe(2);
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

    /// <summary>Verifies physical hover selects the target without replacing its containing background.</summary>
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
        var containingBackground = surface.Cell(new Point(0, 1)).Style.Background;

        // Act
        await surface.Pointer.MoveToAsync(second);

        // Assert
        menu.SelectedIndex.ShouldBe(1);
        second.IsPointerOver.ShouldBeTrue();
        second.GetResolvedAppearance(second.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(containingBackground);
        surface.Cell(new Point(second.Bounds.Right - 1, 1)).Style.Background.ShouldBe(containingBackground);
    }

    /// <summary>Verifies a retained navigation cursor does not paint while application content owns focus.</summary>
    [Fact]
    public async Task Selection_WhenEditorOwnsFocus_DoesNotHighlightRetainedCursorAsync()
    {
        // Arrange
        var file = new MenuItem { Content = new ControlText("File") };
        var menu = new Menu();
        menu.Items.Add(file);
        var editor = new TextInput { Width = Length.Cells(8), Height = Length.Cells(1) };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(editor);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        var selectionBackground = ThemeColorHelper.SelectionBackground(menu.Theme.ShouldNotBeNull());

        // Act
        await surface.Pointer.ClickAsync(editor);

        // Assert
        menu.SelectedIndex.ShouldBe(0);
        surface.ShouldHaveFocus(editor);
        surface.Cell(new Point(file.Bounds.X, file.Bounds.Y)).Style.Background.ShouldNotBe(selectionBackground);
    }

    /// <summary>Verifies leaving a completed submenu interaction removes its owning item's selection paint.</summary>
    [Fact]
    public async Task Selection_WhenLeafInvocationCompletesAndEditorReceivesFocus_DoesNotRemainHighlightedAsync()
    {
        // Arrange
        var open = new MenuItem { Content = new ControlText("Open") };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(open);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu();
        menu.Items.Add(file);
        var editor = new TextInput { Width = Length.Cells(8), Height = Length.Cells(1) };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(editor);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var selectionBackground = ThemeColorHelper.SelectionBackground(menu.Theme.ShouldNotBeNull());
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();

        // Act and assert active menu selection
        await surface.Pointer.ClickAsync(file);
        filePopup.IsOpen.ShouldBeTrue();
        var activeBackground = surface.Cell(new Point(file.Bounds.X, file.Bounds.Y)).Style.Background;
        activeBackground.IsRgb.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(open);
        await surface.Pointer.ClickAsync(editor);

        // Assert
        filePopup.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(editor);
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
        var recent = new MenuItem { Content = new ControlText("Open Recent"), Submenu = recentMenu };
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
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.ClickAsync(recent);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(command);

        // Assert
        invoked.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies Alt+key mnemonic opens the corresponding menu and selects the first item.</summary>
    [Fact]
    public async Task Keyboard_WhenAltMnemonicIsPressed_OpensMenuAndEntersModalAsync()
    {
        // Arrange
        var fileItem = new MenuItem { Content = new ControlText("&File") };
        var editItem = new MenuItem { Content = new ControlText("&Edit") };
        var fileSubmenu = new Menu
        {
            Items =
            {
                new MenuItem { Content = new ControlText("New") },
                new MenuItem { Content = new ControlText("Open") },
            },
        };
        var editSubmenu = new Menu
        {
            Items =
            {
                new MenuItem { Content = new ControlText("Undo") },
            },
        };
        fileItem.Submenu = fileSubmenu;
        editItem.Submenu = editSubmenu;
        var menu = new Menu { Items = { fileItem, editItem } };

        await using var surface = await ComponentSurface.MountAsync(
            menu, new Size(40, 12), TestContext.Current.CancellationToken);

        // Act — send Alt+F via Kitty keyboard protocol (102='f', 3=1+Alt, 1=press)
        await surface.SendAsync("\x1b[102;3:1u"u8.ToArray(), "Alt+F");

        // Assert — modal scope is active and File submenu popup is open
        _ = surface.Application.Modality.Active.ShouldNotBeNull();
        OwnedTree.Find<Popup>(fileItem).ShouldNotBeNull().IsOpen.ShouldBeTrue();

        // Act — first Escape peels the open submenu
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert — submenu closes but menu session may still be active
        OwnedTree.Find<Popup>(fileItem)!.IsOpen.ShouldBeFalse();

        // Act — second Escape exits the menu session entirely
        if (surface.Application.Modality.Active is not null)
        {
            await surface.Keyboard.PressAsync(Code.Escape);
        }

        // Assert — modal scope fully exited
        surface.Application.Modality.Active.ShouldBeNull();
    }
}
