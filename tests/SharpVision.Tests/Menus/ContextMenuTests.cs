// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies context menu construction, ownership, and item management.</summary>
public sealed class ContextMenuTests
{
    /// <summary>Verifies defaults after construction.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasExpectedDefaults()
    {
        using var menu = new ContextMenu();
        menu.IsOpen.ShouldBeFalse();
        menu.Items.ShouldBeEmpty();
        menu.PopupChrome.ShouldBe(default);
    }

    /// <summary>Verifies assigning a ContextMenu to an already-laid-out, clean control dirties
    /// the owner - the None-impact context-menu slot must not leave the owner clean while its
    /// newly spliced presentation still owes a full layout pass.</summary>
    [Fact]
    public void ContextMenu_WhenAssignedToLaidOutControl_DirtiesOwner()
    {
        // Arrange
        var button = new Button { Text = "Target" };
        var stack = new Stack { Children = { button } };
        new LayoutEngine().Layout(stack, new Size(20, 4));
        button.Clear(Invalidation.All);
        stack.Clear(Invalidation.All);
        button.Pending.ShouldBe(Invalidation.None);

        // Act
        button.ContextMenu = new ContextMenu();

        // Assert
        button.Pending.ShouldNotBe(Invalidation.None);
    }

    /// <summary>Verifies PopupChrome applies to the owned popup without leaking it, and
    /// ResetPopupChrome returns it to the PopupChrome appearance.</summary>
    [Fact]
    public void PopupStyle_WhenSetAndReset_AppliesToOwnedPopupAndReturnsToThemeDefault()
    {
        // Arrange
        using var menu = new ContextMenu();
        var popup = (Popup) menu.Presentation;
        var themeRoleBorder = popup.Border;
        var border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None);

        // Act
        menu.PopupChrome = new PopupChrome { Border = border };

        // Assert
        menu.PopupChrome.ShouldBe(new PopupChrome { Border = border });
        popup.Border.ShouldBe(border);

        // Act
        menu.ResetPopupChrome();

        // Assert
        menu.PopupChrome.ShouldBe(default);
        popup.Border.ShouldBe(themeRoleBorder);
    }

    /// <summary>Verifies items can be added and retrieved.</summary>
    [Fact]
    public void Items_WhenAdded_CanBeRetrieved()
    {
        using var menu = new ContextMenu();
        var item = new MenuItem { Text = "Test" };
        menu.Items.Add(item);
        menu.Items.ShouldHaveSingleItem().ShouldBeSameAs(item);
    }

    /// <summary>Verifies item order is preserved.</summary>
    [Fact]
    public void Items_WhenMultipleAdded_PreservesOrder()
    {
        using var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "A" });
        menu.Items.Add(new MenuItem { Text = "B" });
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(new MenuItem { Text = "C" });
        menu.Items.Count.ShouldBe(4);
    }

    /// <summary>Verifies Opening event fires when attached and shown.</summary>
    [Fact]
    public void Show_WhenAttached_RaisesOpeningEvent()
    {
        using var button = new Button { Text = "Host" };
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });
        button.ContextMenu = menu;
        var openingRaised = false;
        menu.Opening += (_, _) => openingRaised = true;

        menu.Show(5, 10);

        openingRaised.ShouldBeTrue();
    }

    /// <summary>Verifies Show is a no-op when the menu has no popup.</summary>
    [Fact]
    public void Show_WhenNotAttached_DoesNotRaiseOpening()
    {
        using var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });
        var openingRaised = false;
        menu.Opening += (_, _) => openingRaised = true;

        menu.Show(5, 10);

        openingRaised.ShouldBeFalse();
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies an Opening callback that removes and reuses the menu supersedes the
    /// original show request instead of leaving a latent open popup on its new ownership cycle.</summary>
    [Fact]
    public void Show_WhenOpeningObserverReattachesMenu_DoesNotOpenSupersededPresentation()
    {
        using var button = new Button { Text = "Host" };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        menu.Opening += (_, _) =>
        {
            button.ContextMenu = null;
            button.ContextMenu = menu;
        };

        menu.Show(5, 10);

        button.ContextMenu.ShouldBeSameAs(menu);
        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies the ContextMenu property round-trips.</summary>
    [Fact]
    public void ContextMenu_WhenSetOnControl_CanBeRetrieved()
    {
        using var button = new Button { Text = "Test" };
        using var menu = new ContextMenu();
        button.ContextMenu = menu;
        button.ContextMenu.ShouldBeSameAs(menu);
    }

    /// <summary>Verifies replacing the property detaches the previous popup — observed through
    /// Show's documented unattached no-op rather than the internal Presentation surface.</summary>
    [Fact]
    public void ContextMenu_WhenReplaced_DetachesPreviousPopup()
    {
        using var button = new Button { Text = "Test" };
        var first = new ContextMenu();
        var second = new ContextMenu();
        button.ContextMenu = first;
        var firstOpened = false;
        first.Opening += (_, _) => firstOpened = true;
        first.Show(0, 0);
        firstOpened.ShouldBeTrue("the first menu is attached before replacement");
        first.Close();

        button.ContextMenu = second;

        button.ContextMenu.ShouldBeSameAs(second);
        firstOpened = false;
        first.Show(0, 0);
        firstOpened.ShouldBeFalse("the first menu is no longer attached after replacement");
        var secondOpened = false;
        second.Opening += (_, _) => secondOpened = true;
        second.Show(0, 0);
        secondOpened.ShouldBeTrue("the second menu is attached after replacement");
    }

    /// <summary>Verifies clearing the property detaches the popup — observed through Show's
    /// documented unattached no-op rather than the internal Presentation surface.</summary>
    [Fact]
    public void ContextMenu_WhenCleared_DetachesPopup()
    {
        using var button = new Button { Text = "Test" };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        var opened = false;
        menu.Opening += (_, _) => opened = true;
        menu.Show(0, 0);
        opened.ShouldBeTrue("the menu is attached before clearing");
        menu.Close();

        button.ContextMenu = null;

        opened = false;
        menu.Show(0, 0);
        opened.ShouldBeFalse("the menu is no longer attached after clearing");
    }

    /// <summary>Verifies Close() after an indirect detach-driven close does not throw.</summary>
    [Fact]
    public void Close_WhenPopupAlreadyClosedIndirectly_DoesNotThrow()
    {
        using var button = new Button { Text = "Test" };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        button.ContextMenu = null;

        Should.NotThrow(menu.Close);

        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies Dispose unsubscribes from the owned popup's Closing/Closed events, so a
    /// disposed ContextMenu no longer republishes its own Closing/Closed when the underlying popup
    /// it never released still transitions afterward.</summary>
    [Fact]
    public void Dispose_WhenCalled_UnsubscribesFromPopupEvents()
    {
        using var button = new Button { Text = "Host" };
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });
        button.ContextMenu = menu;
        menu.Show(0, 0);
        var popup = (Popup) menu.Presentation;
        var closingRaised = 0;
        var closedRaised = 0;
        menu.Closing += (_, _) => closingRaised++;
        menu.Closed += (_, _) => closedRaised++;

        menu.Dispose();
        popup.IsOpen = false;

        closingRaised.ShouldBe(0);
        closedRaised.ShouldBe(0);
    }

    /// <summary>Verifies disposing twice is a no-op, matching every other IDisposable in the library.</summary>
    [Fact]
    public void Dispose_WhenCalledTwice_IsNoOp()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Text = "Test" });

        menu.Dispose();

        Should.NotThrow(menu.Dispose);
    }

    /// <summary>Verifies setting the same instance is a no-op — reassigning an already-open menu
    /// must not force a detach/reattach that would have closed it.</summary>
    [Fact]
    public void ContextMenu_WhenSetToSameInstance_DoesNothing()
    {
        using var button = new Button { Text = "Test" };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        menu.Show(0, 0);
        menu.IsOpen.ShouldBeTrue();

        button.ContextMenu = menu;

        menu.IsOpen.ShouldBeTrue();
        button.ContextMenu.ShouldBeSameAs(menu);
    }

    /// <summary>Verifies assigning one menu instance to a second control while it is still owned
    /// by the first throws and leaves the second control's own state untouched.</summary>
    [Fact]
    public void ContextMenu_WhenAlreadyOwnedByAnotherControl_ThrowsAndLeavesSecondControlUnchanged()
    {
        // Arrange
        using var a = new Button { Text = "A" };
        using var b = new Button { Text = "B" };
        var shared = new ContextMenu();
        var existing = new ContextMenu();
        a.ContextMenu = shared;
        b.ContextMenu = existing;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => b.ContextMenu = shared);

        // Assert b's own menu stays attached and untouched
        b.ContextMenu.ShouldBeSameAs(existing);
        var existingOpened = false;
        existing.Opening += (_, _) => existingOpened = true;
        existing.Show(0, 0);
        existingOpened.ShouldBeTrue();

        // Assert a's menu is untouched by the rejected assignment
        a.ContextMenu.ShouldBeSameAs(shared);
        var sharedOpened = false;
        shared.Opening += (_, _) => sharedOpened = true;
        shared.Show(0, 0);
        sharedOpened.ShouldBeTrue();
    }

    /// <summary>Verifies TextInput default context menu type.</summary>
    [Fact]
    public void TextInput_WhenCreated_HasDefaultContextMenu()
    {
        using var input = new TextInput();
        _ = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
    }

    /// <summary>Verifies TextInputContextMenu has 8 items (Undo, Redo, sep, Cut, Copy, Paste, sep, SelectAll).</summary>
    [Fact]
    public void TextInputContextMenu_WhenCreated_HasExpectedItemCount()
    {
        using var input = new TextInput();
        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        menu.Items.Count.ShouldBe(8);
    }

    /// <summary>Verifies the exact item order and labels.</summary>
    [Fact]
    public void TextInputContextMenu_WhenCreated_HasExpectedItemOrder()
    {
        using var input = new TextInput();
        var menu = input.ContextMenu.ShouldBeOfType<TextInputContextMenu>();
        ItemLabel(menu, 0).ShouldBe("Undo");
        ItemLabel(menu, 1).ShouldBe("Redo");
        _ = menu.Items[2].ShouldBeOfType<MenuSeparator>();
        ItemLabel(menu, 3).ShouldBe("Cut");
        ItemLabel(menu, 4).ShouldBe("Copy");
        ItemLabel(menu, 5).ShouldBe("Paste");
        _ = menu.Items[6].ShouldBeOfType<MenuSeparator>();
        ItemLabel(menu, 7).ShouldBe("Select All");
    }

    /// <summary>Verifies the menu ctor overload adopts the given menu rather than building its own.</summary>
    [Fact]
    public void Constructor_WhenGivenMenu_AdoptsMenu()
    {
        var built = MenuBuilder.Vertical().Item("Test").Build();
        using var menu = new ContextMenu(built);
        menu.IsOpen.ShouldBeFalse();
        menu.Menu.ShouldBeSameAs(built);
    }

    /// <summary>Verifies Items reflects the adopted menu's own items, live.</summary>
    [Fact]
    public void Constructor_WhenGivenMenu_ItemsReflectsAdoptedMenusItems()
    {
        var built = MenuBuilder.Vertical().Item("A").Item("B").Build();
        using var menu = new ContextMenu(built);
        menu.Items.Count.ShouldBe(built.Items.Count);
        menu.Items[0].ShouldBeSameAs(built.Items[0]);

        built.Items.Add(new MenuItem { Text = "C" });

        menu.Items.Count.ShouldBe(3);
    }

    /// <summary>Verifies a null menu is rejected.</summary>
    [Fact]
    public void Constructor_WhenGivenNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new ContextMenu(null!));

    /// <summary>Verifies a menu already hosted as a submenu is rejected with the shared
    /// ownership-tree message.</summary>
    [Fact]
    public void Constructor_WhenMenuAlreadyParented_ThrowsArgumentException()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        using var owner = new MenuItem { Submenu = menu };

        var exception = Should.Throw<ArgumentException>(() => new ContextMenu(menu));

        exception.Message.ShouldStartWith("The control already belongs to a tree.");
    }

    /// <summary>Verifies adopting a disposed menu throws — the same rejection Popup.Content
    /// applies to any disposed content control.</summary>
    [Fact]
    public void Constructor_WhenMenuIsDisposed_Throws()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => new ContextMenu(menu));
    }

    /// <summary>Verifies the constructor uses the adopted menu's orientation as given, matching
    /// how <see cref="MenuItem.Submenu"/> never coerces its adopted menu.</summary>
    [Fact]
    public void Constructor_WhenGivenHorizontalMenu_DoesNotCoerceOrientation()
    {
        var built = MenuBuilder.Horizontal().Item("Test").Build();
        using var menu = new ContextMenu(built);
        menu.Menu.Orientation.ShouldBe(Orientation.Horizontal);
    }

    private static string ItemLabel(ContextMenu menu, int index) =>
        ((MenuItem) menu.Items[index]).Text;
}
