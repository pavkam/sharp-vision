// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies context menu behaviour with real pointer routing across control types and configurations.</summary>
public sealed class ContextMenuSurfaceTests
{
    /// <summary>Verifies PopupStyle's border override reaches the rendered open context menu frame,
    /// not just the property value (see #81).</summary>
    [Fact]
    public async Task PopupStyle_WhenSetAndOpen_RendersOverriddenBorderGlyphAsync()
    {
        // Arrange
        var menu = new ContextMenu
        {
            PopupStyle = new PopupStyle
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
    [ComponentBehaviorEvidence(
        typeof(ContextMenu),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
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

    /// <summary>Verifies right-click on a TextInput opens the default TextInputContextMenu.</summary>
    [ComponentBehaviorEvidence(
        typeof(TextInputContextMenu),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
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
