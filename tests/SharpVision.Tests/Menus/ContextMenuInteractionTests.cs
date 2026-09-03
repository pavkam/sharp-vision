// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves ContextMenu keyboard navigation, activation, Escape dismissal, focus
/// restoration, lifecycle event order, and disposed-instance rejection on mounted surfaces.</summary>
public sealed class ContextMenuInteractionTests
{
    /// <summary>Verifies arrow navigation inside an open context menu skips disabled items, Enter
    /// invokes the selected item, and the invocation closes the menu and restores focus to the
    /// control that owned it before the menu opened.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigatingAndActivating_InvokesClosesAndRestoresFocusAsync()
    {
        // Arrange
        var invoked = new List<string>();
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        menu.Items.Add(new MenuItem { Text = "Copy", IsEnabled = false });
        menu.Items.Add(new MenuItem { Text = "Paste" });
        menu.Menu.ItemInvoked += (_, args) => invoked.Add(args.Item.Text ?? string.Empty);
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        var other = new Button { Text = "Other", Width = Length.Cells(10), Height = Length.Cells(1) };
        Overlay.SetTop(other, Length.Cells(12));
        var root = new Overlay { Children = { button, other } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 16),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the target");

        // Act
        await surface.UpdateAsync(() => menu.Show(2, 2), "show the context menu");

        // Assert
        menu.IsOpen.ShouldBeTrue();
        menu.Menu.SelectedIndex.ShouldBe(0);
        menu.Menu.ContainsFocus.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert - the disabled Copy row is skipped
        menu.Menu.SelectedIndex.ShouldBe(2);

        // Act
        await surface.Keyboard.PressAsync(Code.Up);
        menu.Menu.SelectedIndex.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.End);
        menu.Menu.SelectedIndex.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBe(["Paste"]);
        menu.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(button);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies Escape closes an open context menu without invoking anything, restores
    /// focus to the owner, and does not reopen it on a second Escape.</summary>
    [Fact]
    public async Task Escape_WhenContextMenuIsOpen_ClosesWithoutInvokingAndRestoresFocusAsync()
    {
        // Arrange
        var invoked = 0;
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        menu.Menu.ItemInvoked += (_, _) => invoked++;
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        var root = new Overlay { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the target");
        await surface.Pointer.RightClickAsync(button);
        menu.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        menu.IsOpen.ShouldBeFalse();
        invoked.ShouldBe(0);
        surface.ShouldHaveFocus(button);
        surface.Application.Modality.Active.ShouldBeNull();

        // Arrange - the closed menu must not swallow the next Escape from its owner
        var escapedUnhandled = 0;
        var probe = await surface.Application.Dispatcher.InvokeAsync(
            () => root.AddHandler(Events.Key, (_, args) =>
            {
                if (args.Phase == RoutingPhase.Bubble && args.Stroke.Code == Code.Escape && !args.IsHandled)
                {
                    escapedUnhandled++;
                }
            }),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        menu.IsOpen.ShouldBeFalse();
        escapedUnhandled.ShouldBe(1);
        await surface.Application.Dispatcher.InvokeAsync(probe.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Opening, Closing, and Closed fire in order, each observing the open state
    /// documented for that moment, and that a Show while open raises Opening again.</summary>
    [Fact]
    public async Task Events_WhenShownClosedAndReshown_FireInOrderWithDocumentedStateAsync()
    {
        // Arrange
        var log = new List<string>();
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        menu.Opening += (_, _) => log.Add($"Opening:{menu.IsOpen}");
        menu.Closing += (_, _) => log.Add($"Closing:{menu.IsOpen}");
        menu.Closed += (_, _) => log.Add($"Closed:{menu.IsOpen}");
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(30, 12),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => menu.Show(1, 1), "show");
        await surface.UpdateAsync(() => menu.Show(3, 3), "show again while open");
        await surface.UpdateAsync(menu.Close, "close");

        // Assert
        log.ShouldBe(["Opening:False", "Opening:True", "Closing:False", "Closed:False"]);
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies an empty context menu still opens as a surface and closes on Escape.</summary>
    [Fact]
    public async Task Show_WhenMenuHasNoItems_OpensEmptySurfaceThatEscapeClosesAsync()
    {
        // Arrange
        var menu = new ContextMenu();
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        var popup = (Popup) menu.Presentation;

        // Act
        await surface.UpdateAsync(() => menu.Show(2, 2), "show the empty menu");

        // Assert
        menu.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        menu.Menu.SelectedIndex.ShouldBe(-1);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        menu.IsOpen.ShouldBeFalse();
        popup.SurfaceBounds.ShouldBe(default);
    }

    /// <summary>Verifies every mutating member of a disposed context menu is rejected.</summary>
    [Fact]
    public void Members_WhenContextMenuIsDisposed_Throw()
    {
        // Arrange
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        menu.Dispose();

        // Act / Assert
        _ = Should.Throw<ObjectDisposedException>(() => _ = menu.Items);
        _ = Should.Throw<ObjectDisposedException>(() => _ = menu.IsOpen);
        _ = Should.Throw<ObjectDisposedException>(() => _ = menu.PopupChrome);
        _ = Should.Throw<ObjectDisposedException>(() => menu.PopupChrome = new PopupChrome());
        _ = Should.Throw<ObjectDisposedException>(menu.ResetPopupChrome);
        _ = Should.Throw<ObjectDisposedException>(() => menu.Show(0, 0));
        _ = Should.Throw<ObjectDisposedException>(menu.Close);
    }

    /// <summary>Verifies Right on a submenu-bearing row of an open context menu opens the nested
    /// submenu inside the same modal session, Left closes it again and keeps the context menu
    /// open, and Left at the context menu's own level is left alone.</summary>
    [Fact]
    public async Task Keyboard_WhenLeftAndRightArePressed_OpenAndCloseNestedSubmenuInsideOneScopeAsync()
    {
        // Arrange
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Text = "One" });
        var menu = new ContextMenu();
        var recent = new MenuItem { Text = "Recent", Submenu = recentMenu };
        menu.Items.Add(recent);
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        var root = new Overlay { Children = { button } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => menu.Show(1, 1), "show the context menu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert - nothing to close at the root level
        menu.IsOpen.ShouldBeTrue();
        recent.IsSubmenuOpen.ShouldBeFalse();

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        recent.IsSubmenuOpen.ShouldBeTrue();
        recentMenu.ContainsFocus.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert
        recent.IsSubmenuOpen.ShouldBeFalse();
        menu.IsOpen.ShouldBeTrue();
        menu.Menu.ContainsFocus.ShouldBeTrue();
        menu.Menu.SelectedItem.ShouldBeSameAs(recent);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }


    /// <summary>Verifies a throwing Opening subscriber propagates out of Show and leaves the menu
    /// closed, so a caller never observes a half-presented surface.</summary>
    [Fact]
    public async Task Show_WhenOpeningSubscriberThrows_PropagatesAndStaysClosedAsync()
    {
        // Arrange
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Cut" });
        menu.Opening += (_, _) => throw new InvalidOperationException("veto");
        var button = new Button { Text = "Target", Width = Length.Cells(10), Height = Length.Cells(1), ContextMenu = menu };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(30, 12),
            TestContext.Current.CancellationToken);

        // Act
        var failure = await surface.Application.Dispatcher.InvokeAsync(
            () => Record.Exception(() => menu.Show(2, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        _ = failure.ShouldBeOfType<InvalidOperationException>();
        menu.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }
}
