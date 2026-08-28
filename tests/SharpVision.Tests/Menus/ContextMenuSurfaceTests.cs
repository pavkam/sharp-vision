// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies context menu behaviour with real pointer routing across control types and configurations.</summary>
public sealed class ContextMenuSurfaceTests
{
    /// <summary>Verifies showing an already-open context menu at a new root-cell position
    /// immediately rearranges its retained popup.</summary>
    [Fact]
    public async Task Show_WhenCalledAgainWhileOpen_RepositionsPopupAsync()
    {
        // Arrange
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Inspect" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(30, 20),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => menu.Show(2, 2), "show context menu at first position");
        var popup = (Popup) menu.Presentation;
        popup.SurfaceBounds.X.ShouldBe(2);
        popup.SurfaceBounds.Y.ShouldBe(2);

        // Act
        await surface.UpdateAsync(() => menu.Show(10, 10), "reposition open context menu");

        // Assert
        popup.SurfaceBounds.X.ShouldBe(10);
        popup.SurfaceBounds.Y.ShouldBe(10);
    }

    /// <summary>Verifies a ContextMenu-owned Menu publishes leaf invocation while its sibling
    /// submenu is still open, matching the ordering of a directly mounted Menu session.</summary>
    [Fact]
    public async Task ItemInvoked_WhenSiblingSubmenuIsOpen_ObservesOpenChainBeforeClosureAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Text = "About" });
        var file = new MenuItem { Text = "File", Submenu = submenu };
        var save = new MenuItem { Text = "Save" };
        var innerMenu = new Menu { Orientation = Orientation.Vertical };
        innerMenu.Items.Add(file);
        innerMenu.Items.Add(save);
        var contextMenu = new ContextMenu(innerMenu);
        var target = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = contextMenu
        };
        await using var surface = await ComponentSurface.MountAsync(
            target,
            new Size(40, 20),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => contextMenu.Show(2, 2), "open context menu");
        await surface.Pointer.ClickAsync(file);
        file.IsSubmenuOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(contextMenu.Presentation);
        bool? observedSubmenuOpen = null;
        innerMenu.ItemInvoked += (_, args) =>
        {
            if (ReferenceEquals(args.Item, save))
            {
                observedSubmenuOpen = file.IsSubmenuOpen;
            }
        };

        // Act
        await surface.UpdateAsync(save.PerformInvoke, "invoke context-menu leaf");

        // Assert
        observedSubmenuOpen.ShouldBe(true);
        contextMenu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies PopupChrome's border override reaches the rendered open context menu frame,
    /// not just the property value.</summary>
    [Fact]
    public async Task PopupStyle_WhenSetAndOpen_RendersOverriddenBorderGlyphAsync()
    {
        // Arrange
        var menu = new ContextMenu
        {
            PopupChrome = new PopupChrome
            {
                Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
            }
        };
        menu.Items.Add(new MenuItem { Text = "Inspect" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };
        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);
        var popup = (Popup) menu.Presentation;

        // Act
        await surface.Pointer.RightClickAsync(button);

        // Assert
        menu.IsOpen.ShouldBeTrue();
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).Text.ShouldBe("+");
    }

    /// <summary>Verifies right-click on a button with a custom ContextMenu opens the popup.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOnButtonWithContextMenu_OpensMenuAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Inspect" });
        menu.Items.Add(new MenuItem { Text = "Run" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        menu.IsOpen.ShouldBeFalse();
        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a ContextMenu assigned and shown in the same dispatcher turn actually
    /// paints, rather than opening with Bounds stuck at default because the newly spliced,
    /// never-laid-out popup's own Pending == All request was swallowed by the owner's
    /// None-impact slot.</summary>
    [Fact]
    public async Task Show_WhenAssignedAndShownInTheSameTurn_PaintsAsync()
    {
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                var menu = new ContextMenu();
                menu.Items.Add(new MenuItem { Text = "Inspect" });
                button.ContextMenu = menu;
                menu.Show(2, 2);
            },
            "assign and show a context menu in one turn");

        var menu = button.ContextMenu.ShouldNotBeNull();
        var popup = (Popup) menu.Presentation;

        menu.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.ShouldNotBe(default);
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Y)).Text.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies a context menu shown near the bottom-right corner of a small root stays clamped
    /// inside the viewport rather than drawing past it. <see cref="ContextMenu.Show"/> positions
    /// the popup with a fixed origin, which bypasses the edge-flip fallback ordinary anchored
    /// popups use, but the popup's <c>ConstrainToRoot</c> clamp still applies independent of
    /// flipping.
    /// </summary>
    [Fact]
    public async Task Show_WhenPositionedNearBottomRightCorner_ClampsFramedSurfaceAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Inspect" });
        menu.Items.Add(new MenuItem { Text = "Run" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(12, 4), TestContext.Current.CancellationToken);

        await surface.UpdateAsync(
            () =>
            {
                button.ContextMenu = menu;
                menu.Show(11, 3);
            },
            "assign and show near the bottom-right corner");

        var popup = (Popup) menu.Presentation;
        popup.SurfaceBounds.X.ShouldBeGreaterThanOrEqualTo(0);
        popup.SurfaceBounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        popup.SurfaceBounds.Right.ShouldBeLessThanOrEqualTo(12);
        popup.SurfaceBounds.Bottom.ShouldBeLessThanOrEqualTo(4);
    }

    /// <summary>Verifies detaching an open ContextMenu's owner repaints the popup's vacated
    /// cells, rather than leaving them stale under the None-impact slot's missing Render floor
    /// on removal.</summary>
    [Fact]
    public async Task ContextMenu_WhenOwnerDetachesWhileOpen_RepaintsVacatedCellsAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Inspect" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };
        var host = new Overlay { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            host, new Size(30, 10), TestContext.Current.CancellationToken);
        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();
        var popup = (Popup) menu.Presentation;
        var openBounds = popup.SurfaceBounds;
        var openText = surface.Cell(new Point(openBounds.X, openBounds.Y)).Text;
        openText.ShouldNotBeNullOrEmpty();

        await surface.UpdateAsync(() => host.Children.Remove(button), "detach the context menu's owner while open");

        surface.Cell(new Point(openBounds.X, openBounds.Y)).Text.ShouldNotBe(openText);
    }

    /// <summary>Verifies right-click on a TextInput opens the default TextInputContextMenu.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOnTextInput_OpensDefaultContextMenuAsync()
    {
        var input = new TextInput
        {
            Text = "Hello world",
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 12), TestContext.Current.CancellationToken);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.IsOpen.ShouldBeFalse();
        await surface.Pointer.RightClickAsync(input);
        menu.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a control without a ContextMenu ignores secondary press.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOnControlWithoutMenu_DoesNothingAsync()
    {
        var button = new Button
        {
            Text = "Plain",
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(20, 5), TestContext.Current.CancellationToken);

        button.ContextMenu.ShouldBeNull();
        await surface.Pointer.RightClickAsync(button);
        button.ContextMenu.ShouldBeNull();
    }

    /// <summary>Verifies a disabled control does not show the context menu.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOnDisabledControl_DoesNotOpenMenuAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Action" });
        var button = new Button
        {
            Text = "Disabled",
            Width = Length.Cells(12),
            Height = Length.Cells(1),
            IsEnabled = false,
            ContextMenu = menu
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(20, 5), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies clicking outside an open context menu closes it.</summary>
    [Fact]
    public async Task Pointer_WhenClickOutsideOpenMenu_ClosesMenuAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Action" });
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };
        var filler = new ControlText("filler") { Width = Length.Cells(10) };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { button, filler }
        };

        await using var surface = await ComponentSurface.MountAsync(
            row, new Size(30, 15), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();

        await surface.Pointer.ClickAsync(filler);
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Opening event disables Cut and Copy when there is no selection.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputHasNoSelection_DisablesCutAndCopyAsync()
    {
        var input = new TextInput
        {
            Text = "hello",
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        await surface.Pointer.RightClickAsync(input);

        ((MenuItem) menu.Items[3]).IsEnabled.ShouldBeFalse();
        ((MenuItem) menu.Items[4]).IsEnabled.ShouldBeFalse();
        ((MenuItem) menu.Items[7]).IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies Undo is disabled on a fresh empty TextInput.</summary>
    [Fact]
    public async Task Pointer_WhenEmptyTextInputIsUnedited_DisablesUndoAsync()
    {
        var input = new TextInput
        {
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        await surface.Pointer.RightClickAsync(input);

        ((MenuItem) menu.Items[0]).IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies read-only TextInput disables Cut and Paste but allows Copy.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputIsReadOnly_DisablesCutAndPasteAsync()
    {
        var input = new TextInput
        {
            Text = "read only text",
            IsReadOnly = true,
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(input);
        await surface.UpdateAsync(() => input.Select(0, 4), "select text");
        await surface.Pointer.RightClickAsync(input);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ((MenuItem) menu.Items[3]).IsEnabled.ShouldBeFalse();
        ((MenuItem) menu.Items[4]).IsEnabled.ShouldBeTrue();
        ((MenuItem) menu.Items[5]).IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies password TextInput disables both Cut and Copy.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputIsPassword_DisablesCutAndCopyAsync()
    {
        var input = new TextInput
        {
            Text = "secret",
            PasswordCharacter = new Rune('*'),
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(input);
        await surface.UpdateAsync(() => input.Select(0, 6), "select all");
        await surface.Pointer.RightClickAsync(input);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ((MenuItem) menu.Items[3]).IsEnabled.ShouldBeFalse();
        ((MenuItem) menu.Items[4]).IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies empty TextInput disables Select All.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputIsEmpty_DisablesSelectAllAsync()
    {
        var input = new TextInput
        {
            Text = "",
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(input);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ((MenuItem) menu.Items[7]).IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies TextInput with selection enables Cut and Copy.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputHasSelection_EnablesCutAndCopyAsync()
    {
        var input = new TextInput
        {
            Text = "hello world",
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(input);
        await surface.UpdateAsync(() => input.Select(0, 5), "select hello");
        await surface.Pointer.RightClickAsync(input);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ((MenuItem) menu.Items[3]).IsEnabled.ShouldBeTrue();
        ((MenuItem) menu.Items[4]).IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies TextInput with undo history enables Undo.</summary>
    [Fact]
    public async Task Pointer_WhenTextInputHasUndoHistory_EnablesUndoAsync()
    {
        var input = new TextInput
        {
            Text = "original",
            Width = Length.Cells(20),
            Height = Length.Cells(3)
        };

        await using var surface = await ComponentSurface.MountAsync(
            input, new Size(30, 14), TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(input);
        await surface.Keyboard.TypeAsync("extra");
        await surface.Pointer.RightClickAsync(input);

        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ((MenuItem) menu.Items[0]).IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies right-click works on a CheckBox with a context menu.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryPressOnCheckBox_OpensAttachedMenuAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Info" });
        var check = new CheckBox
        {
            Text = "Option",
            Width = Length.Cells(12),
            ContextMenu = menu
        };

        await using var surface = await ComponentSurface.MountAsync(
            check, new Size(20, 5), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(check);
        menu.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies replacing a ContextMenu while open closes the old one.</summary>
    [Fact]
    public async Task Pointer_WhenMenuReplacedWhileOpen_ClosesOldMenuAsync()
    {
        var first = new ContextMenu();
        first.Items.Add(new MenuItem { Text = "First" });
        var second = new ContextMenu();
        second.Items.Add(new MenuItem { Text = "Second" });
        var button = new Button
        {
            Text = "Host",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = first
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        first.IsOpen.ShouldBeTrue();

        await surface.UpdateAsync(() => button.ContextMenu = second, "replace context menu");

        first.IsOpen.ShouldBeFalse();
        second.IsOpen.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies replacing an open ContextMenu disposes its light-dismiss
    /// registration rather than leaving a stale handler retained on the root:
    /// a pointer press that would have dismissed the old (now replaced) menu
    /// must not throw and must leave the new menu's own state untouched.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenMenuReplacedWhileOpen_DisposesOldLightDismissWithoutThrowingAsync()
    {
        var first = new ContextMenu();
        first.Items.Add(new MenuItem { Text = "First" });
        var second = new ContextMenu();
        second.Items.Add(new MenuItem { Text = "Second" });
        var button = new Button
        {
            Text = "Host",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = first
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        first.IsOpen.ShouldBeTrue();

        await surface.UpdateAsync(() => button.ContextMenu = second, "replace context menu");

        first.IsOpen.ShouldBeFalse();

        // A press anywhere on the surface would have run the old menu's
        // now-disposed light-dismiss registration had it survived detachment.
        await surface.Pointer.ClickAsync(button);

        first.IsOpen.ShouldBeFalse();
        second.IsOpen.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies disposing the owning control while its ContextMenu is open
    /// still closes the popup and disposes the light-dismiss registration,
    /// rather than leaving it open against a control the owner no longer has.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenOwnerIsDisposedWhileMenuIsOpen_ClosesTheMenuAsync()
    {
        var stack = new Stack();

        await using var surface = await ComponentSurface.MountAsync(
            stack, new Size(30, 10), TestContext.Current.CancellationToken);

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Item" });
        var button = new Button
        {
            Text = "Host",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };

        await surface.UpdateAsync(() => stack.Children.Add(button), "attach button");
        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();

        await surface.UpdateAsync(
            () =>
            {
                _ = stack.Children.Remove(button);
                button.Dispose();
            },
            "remove and dispose owner");

        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Opening fires before IsOpen becomes true.</summary>
    [Fact]
    public async Task Pointer_WhenOpeningEventFires_FiresBeforeIsOpenChangesAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });
        var wasOpenDuringOpening = true;
        menu.Opening += (_, _) => wasOpenDuringOpening = menu.IsOpen;
        var button = new Button
        {
            Text = "Host",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        wasOpenDuringOpening.ShouldBeFalse();
        menu.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a ContextMenu built through MenuBuilder opens on right-click and closes
    /// on item invocation, exercising the menu-adopting constructor end to end.</summary>
    [Fact]
    public async Task Pointer_WhenBuilderComposedMenuInvoked_ClosesTheMenuAsync()
    {
        var invoked = false;
        var menu = new ContextMenu(
            MenuBuilder.Vertical()
                .Item("Inspect", onInvoke: () => invoked = true)
                .Build());
        var button = new Button
        {
            Text = "Target",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };

        await using var surface = await ComponentSurface.MountAsync(
            button, new Size(30, 10), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();

        var item = (MenuItem) menu.Items[0];
        await surface.Pointer.ClickAsync(item);

        invoked.ShouldBeTrue();
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Closed event fires after light dismiss.</summary>
    [Fact]
    public async Task Pointer_WhenClosedEventFires_ReportsClosedStateAsync()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });
        var closedRaised = false;
        menu.Closed += (_, _) => closedRaised = true;
        var button = new Button
        {
            Text = "Host",
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            ContextMenu = menu
        };
        var filler = new ControlText("filler") { Width = Length.Cells(10) };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { button, filler }
        };

        await using var surface = await ComponentSurface.MountAsync(
            row, new Size(30, 15), TestContext.Current.CancellationToken);

        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();

        await surface.Pointer.ClickAsync(filler);

        closedRaised.ShouldBeTrue();
        menu.IsOpen.ShouldBeFalse();
    }
}
