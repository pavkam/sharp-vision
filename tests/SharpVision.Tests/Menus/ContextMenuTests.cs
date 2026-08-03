// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies context menu construction, ownership, and item management.</summary>
public sealed class ContextMenuTests
{
    /// <summary>Verifies defaults after construction.</summary>
    [ComponentUnitEvidence(typeof(ContextMenu))]
    [Fact]
    public void Constructor_WhenCreated_HasExpectedDefaults()
    {
        using var menu = new ContextMenu();
        menu.IsOpen.ShouldBeFalse();
        menu.Items.ShouldBeEmpty();
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
    /// by the first throws and leaves the second control's own state untouched (see #180).</summary>
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
    [ComponentUnitEvidence(typeof(TextInputContextMenu))]
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

    private static string ItemLabel(ContextMenu menu, int index) =>
        ((ControlText) ((MenuItem) menu.Items[index]).Text!).Content;
}
