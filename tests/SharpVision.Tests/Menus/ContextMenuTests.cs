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
    }

    /// <summary>Verifies items can be added and retrieved.</summary>
    [Fact]
    public void Items_WhenAdded_CanBeRetrieved()
    {
        using var menu = new ContextMenu();
        var item = new MenuItem { Content = new ControlText("Test") };
        menu.Items.Add(item);
        menu.Items.ShouldHaveSingleItem().ShouldBeSameAs(item);
    }

    /// <summary>Verifies item order is preserved.</summary>
    [Fact]
    public void Items_WhenMultipleAdded_PreservesOrder()
    {
        using var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Content = new ControlText("A") });
        menu.Items.Add(new MenuItem { Content = new ControlText("B") });
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(new MenuItem { Content = new ControlText("C") });
        menu.Items.Count.ShouldBe(4);
    }

    /// <summary>Verifies Opening event fires when attached and shown.</summary>
    [Fact]
    public void Show_WhenAttached_RaisesOpeningEvent()
    {
        using var button = new Button { Content = new ControlText("Host") };
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Content = new ControlText("Test") });
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
        menu.Items.Add(new MenuItem { Content = new ControlText("Test") });
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
        using var button = new Button { Content = new ControlText("Test") };
        using var menu = new ContextMenu();
        button.ContextMenu = menu;
        button.ContextMenu.ShouldBeSameAs(menu);
    }

    /// <summary>Verifies replacing the property detaches the previous popup.</summary>
    [Fact]
    public void ContextMenu_WhenReplaced_DetachesPreviousPopup()
    {
        using var button = new Button { Content = new ControlText("Test") };
        var first = new ContextMenu();
        var second = new ContextMenu();
        button.ContextMenu = first;
        first.Presentation.Parent.ShouldBeSameAs(button);

        button.ContextMenu = second;

        button.ContextMenu.ShouldBeSameAs(second);
        first.Presentation.Parent.ShouldBeNull();
        second.Presentation.Parent.ShouldBeSameAs(button);
    }

    /// <summary>Verifies clearing the property detaches the popup.</summary>
    [Fact]
    public void ContextMenu_WhenCleared_DetachesPopup()
    {
        using var button = new Button { Content = new ControlText("Test") };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        menu.Presentation.Parent.ShouldBeSameAs(button);

        button.ContextMenu = null;

        menu.Presentation.Parent.ShouldBeNull();
    }

    /// <summary>Verifies Close() after an indirect detach-driven close does not throw.</summary>
    [Fact]
    public void Close_WhenPopupAlreadyClosedIndirectly_DoesNotThrow()
    {
        using var button = new Button { Content = new ControlText("Test") };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        button.ContextMenu = null;

        Should.NotThrow(menu.Close);

        menu.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies setting the same instance is a no-op.</summary>
    [Fact]
    public void ContextMenu_WhenSetToSameInstance_DoesNothing()
    {
        using var button = new Button { Content = new ControlText("Test") };
        var menu = new ContextMenu();
        button.ContextMenu = menu;
        var presentation = menu.Presentation;

        button.ContextMenu = menu;

        menu.Presentation.ShouldBeSameAs(presentation);
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

    private static string ItemLabel(ContextMenu menu, int index) =>
        ((ControlText) ((MenuItem) menu.Items[index]).Content!).Content;
}
