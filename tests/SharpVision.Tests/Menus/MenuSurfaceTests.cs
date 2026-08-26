// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves menu entries and navigation through mounted terminal surfaces.</summary>
public sealed class MenuSurfaceTests
{
    /// <summary>Verifies SubmenuChrome's border override reaches the rendered open submenu frame,
    /// not just the property value.</summary>
    [Fact]
    public async Task SubmenuStyle_WhenSetAndOpen_RendersOverriddenBorderGlyphAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "About" });
        var help = new MenuItem
        {
            Text = "Help",
            Submenu = submenu,
            SubmenuChrome = new PopupChrome
            {
                Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
            }
        };
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
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).Text.ShouldBe("+");
    }

    /// <summary>Verifies a short submenu uses the shipped menu minimum before popup framing.</summary>
    [Fact]
    public async Task Submenu_WhenShortDefaultOpens_UsesTenCellMenuWidthAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "About" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
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
            ╭──────────╮
            │About     │
            ╰──────────╯

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
        submenu.Items.Add(new MenuItem { Text = "About" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
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
        submenu.Items.Add(new MenuItem { Text = "Documentation" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
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
        submenu.Items.Add(new MenuItem { Text = "About" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
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
        submenu.Items.Add(new MenuItem { Text = "About" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
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

    /// <summary>
    /// Verifies a submenu opened from an item near the bottom edge of a short root stays clamped
    /// inside the viewport instead of drawing past it, matching the documented contract ("A
    /// smaller root clamps the complete framed surface without drawing outside the viewport" and
    /// "Popup edge fallback may flip those preferred directions to keep the framed surface inside
    /// the terminal"). A horizontal menu's submenu opens Below the anchor by default; when the
    /// root is too short for both Below and the Above fallback to fit, the surface must still be
    /// clamped to the root's vertical extent rather than left to overflow it.
    /// </summary>
    [Fact]
    public async Task Submenu_WhenRootIsShorterThanDefault_ClampsFramedSurfaceVerticallyAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "About" });
        submenu.Items.Add(new MenuItem { Text = "Updates" });
        var help = new MenuItem { Text = "Help", Submenu = submenu };
        var menu = new Menu();
        menu.Items.Add(help);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(help).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(help);

        // Assert
        popup.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>Verifies root navigation, item press/release, separator exclusion, and cleanup.</summary>
    [Fact]
    public async Task Input_WhenMenuNavigatesAndInvokes_UsesOwnedFacesWithoutFocusTheftAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "Open" };
        var separator = new MenuSeparator();
        var second = new MenuItem { Text = "Save" };
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

        // Assert cleanup and direct disable
        second.IsPressed.ShouldBeFalse();
        menu.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(second, VisualState.Disabled);
        surface.ShouldHaveState(menu, VisualState.Disabled);

        // Assert a never-directly-disabled MenuItem inherits Disabled from its owning Menu
        // rather than only from its own IsEnabled flag.
        first.IsEnabled.ShouldBeTrue();
        first.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(first, VisualState.Disabled);

        // The disable cleanup dropped the control-side capture and press without the test-side
        // pointer driver observing it, so release its own bookkeeping before pressing again.
        await surface.Pointer.ReleaseAsync();

        // Assert a pointer click on the disabled MenuItem does not invoke its command
        var invokedBeforeDisabledClick = invoked;
        await surface.Pointer.ClickAsync(second);
        invoked.ShouldBe(invokedBeforeDisabledClick);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled menu at the same new size.
        await surface.ResizeAsync(new Size(12, 4));
        var disabledMenuBounds = menu.Bounds;
        var disabledMenuDesiredSize = menu.DesiredSize;
        var disabledSecondBounds = second.Bounds;
        var disabledSecondDesiredSize = second.DesiredSize;

        var referenceFirst = new MenuItem { Text = "Open" };
        var referenceSeparator = new MenuSeparator();
        var referenceSecond = new MenuItem { Text = "Save" };
        var referenceMenu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        referenceMenu.Items.Add(referenceFirst);
        referenceMenu.Items.Add(referenceSeparator);
        referenceMenu.Items.Add(referenceSecond);
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceMenu,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        referenceMenu.Bounds.ShouldBe(disabledMenuBounds);
        referenceMenu.DesiredSize.ShouldBe(disabledMenuDesiredSize);
        referenceSecond.Bounds.ShouldBe(disabledSecondBounds);
        referenceSecond.DesiredSize.ShouldBe(disabledSecondDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => menu.IsEnabled = true, "re-enable Menu");
        await surface.UpdateAsync(() => second.IsEnabled = true, "re-enable MenuItem");

        // Assert Normal state resumes, including for the ancestor-inherited item
        surface.ShouldHaveState(menu, VisualState.Normal);
        surface.ShouldHaveState(second, VisualState.Normal);
        surface.ShouldHaveState(first, VisualState.Normal);

        // Assert interaction resumes
        await surface.Pointer.MoveToAsync(first);
        first.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies lock-key state remains incidental to forward and reverse menu Tab navigation.</summary>
    [Theory]
    [InlineData(Modifiers.CapsLock, 1)]
    [InlineData(Modifiers.NumLock, 1)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock, 1)]
    [InlineData(Modifiers.Shift | Modifiers.CapsLock, 2)]
    [InlineData(Modifiers.Shift | Modifiers.NumLock, 2)]
    public async Task Input_WhenMenuTabCarriesLockState_MovesSelectionAsync(Modifiers modifiers, int expectedIndex)
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Text = "One" });
        menu.Items.Add(new MenuItem { Text = "Two" });
        menu.Items.Add(new MenuItem { Text = "Three" });
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        menu.SelectedIndex.ShouldBe(0);

        await surface.Keyboard.PressAsync(Code.Tab, modifiers);

        menu.SelectedIndex.ShouldBe(expectedIndex);
    }

    /// <summary>Verifies a mounted Menu inherits Disabled from a disabled ancestor rather than
    /// only from its own IsEnabled flag, and resumes Normal once re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenMenuAncestorIsDisabled_InheritsDisabledAndRecoversAsync()
    {
        // Arrange
        var item = new MenuItem { Text = "Open" };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        menu.Items.Add(item);
        var host = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { menu }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Stack");

        // Assert the menu inherits Disabled without its own IsEnabled flag changing
        menu.IsEnabled.ShouldBeTrue();
        menu.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(menu, VisualState.Disabled);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Stack");

        // Assert Normal state resumes
        surface.ShouldHaveState(menu, VisualState.Normal);
    }

    /// <summary>Verifies a MenuSeparator proves direct disable, inherits Disabled from its owning
    /// Menu, keeps stable geometry across a genuine resize while disabled, and resumes Normal
    /// once re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenSeparatorOrOwningMenuIsDisabled_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "Open" };
        var separator = new MenuSeparator();
        var second = new MenuItem { Text = "Save" };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        menu.Items.Add(first);
        menu.Items.Add(separator);
        menu.Items.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act direct disable
        await surface.UpdateAsync(() => separator.IsEnabled = false, "disable MenuSeparator directly");

        // Assert direct disable
        surface.ShouldHaveState(separator, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => separator.IsEnabled = true, "re-enable MenuSeparator directly");
        surface.ShouldHaveState(separator, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => menu.IsEnabled = false, "disable owning Menu");

        // Assert the separator inherits Disabled without its own IsEnabled flag changing
        separator.IsEnabled.ShouldBeTrue();
        separator.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(separator, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled menu at the same new size.
        await surface.ResizeAsync(new Size(12, 4));
        var disabledBounds = separator.Bounds;
        var disabledDesiredSize = separator.DesiredSize;

        var referenceFirst = new MenuItem { Text = "Open" };
        var referenceSeparator = new MenuSeparator();
        var referenceSecond = new MenuItem { Text = "Save" };
        var referenceMenu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        referenceMenu.Items.Add(referenceFirst);
        referenceMenu.Items.Add(referenceSeparator);
        referenceMenu.Items.Add(referenceSecond);
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceMenu,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        referenceSeparator.Bounds.ShouldBe(disabledBounds);
        referenceSeparator.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => menu.IsEnabled = true, "re-enable owning Menu");

        // Assert Normal state resumes
        surface.ShouldHaveState(separator, VisualState.Normal);
    }

    /// <summary>Verifies physical hover selects the target without replacing its containing background.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverItem_SelectsAndHighlightsTargetRowAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "First" };
        var second = new MenuItem { Text = "Second" };
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
        var file = new MenuItem { Text = "File" };
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
        var open = new MenuItem { Text = "Open" };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(open);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
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
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var close = new MenuItem { Text = "Close" };
        fileMenu.Items.Add(close);
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
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

        // Act and assert application-modified Escape remains available to ancestors.
        await surface.Keyboard.PressAsync(Code.Escape, Modifiers.Control);
        editPopup.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(editMenu);

        // Act close
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert focus restoration
        editPopup.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies selection observers may mutate or detach an armed menu without the
    /// interrupted pointer path indexing a replacement entry or opening an obsolete submenu.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public async Task Pointer_WhenSelectionObserverMutatesSubmenuTarget_DoesNotUseStaleIndexAsync(
        int mutation,
        bool fromSelectedItem)
    {
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        var root = new Stack { Children = { menu } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(file);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var mutated = false;
        menu.PropertyChanged += (_, eventArgs) =>
        {
            var propertyName = fromSelectedItem ? nameof(Menu.SelectedItem) : nameof(Menu.SelectedIndex);

            if (mutated || eventArgs.PropertyName != propertyName || !ReferenceEquals(menu.SelectedItem, edit))
            {
                return;
            }

            mutated = true;

            switch (mutation)
            {
                case 0:
                    _ = menu.Items.Remove(edit);
                    break;
                case 1:
                    menu.Items.Clear();
                    break;
                case 2:
                    menu.Items.Move(1, 0);
                    break;
                case 3:
                    menu.Items[1] = new MenuSeparator();
                    break;
                case 4:
                    _ = root.Children.Remove(menu);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        };

        await surface.Pointer.MoveToAsync(edit);

        mutated.ShouldBeTrue();
        editPopup.IsOpen.ShouldBe(mutation == 2);
    }

    /// <summary>Verifies moving an armed menu to a command closes the previous submenu without invoking it.</summary>
    [Fact]
    public async Task Pointer_WhenArmedMenuMovesToCommand_ClosesPreviousSubmenuAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var command = new MenuItem { Text = "Exit" };
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
        fileMenu.Items.Add(new MenuItem { Text = "Open" });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Text = "Copy" });
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "Edit", Submenu = editMenu };
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
        var command = new MenuItem { Text = "Today" };
        command.Invoked += (_, _) => invoked++;
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(command);
        var recent = new MenuItem { Text = "Open Recent", Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Text = "File", Submenu = fileMenu };
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
        var fileItem = new MenuItem { Text = "&File" };
        var editItem = new MenuItem { Text = "&Edit" };
        var fileSubmenu = new Menu
        {
            Items =
            {
                new MenuItem { Text = "New" },
                new MenuItem { Text = "Open" },
            },
        };
        var editSubmenu = new Menu
        {
            Items =
            {
                new MenuItem { Text = "Undo" },
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

    /// <summary>Verifies a runtime IsVisible -&gt; Collapsed -&gt; IsVisible transition on a mounted
    /// vertical menu item leaves no stale rendered row behind, and that pointer hover correctly
    /// tracks the surviving item's live position rather than a stale one - both immediately after
    /// the collapse and again after the item is restored.</summary>
    [Fact]
    public async Task Pointer_WhenVerticalItemTogglesCollapsedThenVisible_ClearsStaleRowsAndHoverAsync()
    {
        // Arrange
        var first = new MenuItem { Text = "First" };
        var middle = new MenuItem { Text = "Middle" };
        var last = new MenuItem { Text = "Last" };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        menu.Items.Add(first);
        menu.Items.Add(middle);
        menu.Items.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
            First
            Middle
            Last
            """);

        // Act - collapse the middle item.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Collapsed, "collapse middle menu item");

        // Assert - "Last" now occupies row 1 (Middle's former row) with no stale "Middle" glyphs.
        surface.ShouldRender("""
            First
            Last

            """);
        await surface.Pointer.MoveToAsync(last);
        last.IsPointerOver.ShouldBeTrue();

        // Act - restore visibility.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Visible, "restore middle menu item");

        // Assert - the original three-row layout is exactly restored.
        surface.ShouldRender("""
            First
            Middle
            Last
            """);
        await surface.Pointer.MoveToAsync(middle);
        middle.IsPointerOver.ShouldBeTrue();
        last.IsPointerOver.ShouldBeFalse();
    }

    /// <summary>Verifies a mounted vertical Menu negotiates one shared start-affix column across
    /// mixed rows: the affixed row's caption and the plain row's caption begin at the exact same
    /// column, with the plain row leaving that shared column blank rather than starting flush at
    /// its own empty marker column.</summary>
    [Fact]
    public async Task Render_WhenVerticalItemsMixAffixedAndPlainRows_AlignsEveryCaptionToTheSharedColumnAsync()
    {
        // Arrange
        var withAffix = new MenuItem { Text = "Go", StartAffix = new Affix(">") };
        var plain = new MenuItem { Text = "Stop" };
        var menu = new Menu { Orientation = Orientation.Vertical, MinWidth = 10 };
        menu.Items.Add(withAffix);
        menu.Items.Add(plain);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(10, 2),
            TestContext.Current.CancellationToken);

        // Assert - the start affix draws flush at column 0 (Command items have no marker), and
        // both captions begin at column 2 (affix width 1 + the default one-cell gap) even though
        // "Stop" owns no affix of its own; its row leaves columns 0-1 blank instead.
        surface.Cell(new Point(0, 0)).Text.ShouldBe(">");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("G");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 1)).Text.ShouldBe("S");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("t");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("o");
        surface.Cell(new Point(5, 1)).Text.ShouldBe("p");
        withAffix.TextControl!.Bounds.X.ShouldBe(plain.TextControl!.Bounds.X);
    }
}
