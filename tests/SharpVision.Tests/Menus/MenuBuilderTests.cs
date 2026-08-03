// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies the fluent MenuBuilder produces correctly configured Menu instances.</summary>
public sealed class MenuBuilderTests
{
    /// <summary>Verifies Vertical produces a vertical menu with zero spacing.</summary>
    [Fact]
    public void Build_WhenVertical_CreatesVerticalMenuWithZeroSpacing()
    {
        var menu = MenuBuilder.Vertical().Build();

        menu.Orientation.ShouldBe(Orientation.Vertical);
        menu.Spacing.ShouldBe(0);
        menu.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies Horizontal produces a horizontal menu with configurable spacing.</summary>
    [Fact]
    public void Build_WhenHorizontal_CreatesHorizontalMenuWithSpacing()
    {
        var menu = MenuBuilder.Horizontal(spacing: 3).Build();

        menu.Orientation.ShouldBe(Orientation.Horizontal);
        menu.Spacing.ShouldBe(3);
    }

    /// <summary>Verifies Item adds a command menu item with label and shortcut text.</summary>
    [Fact]
    public void Item_WhenAdded_CreatesCommandItemWithLabelAndShortcut()
    {
        var menu = MenuBuilder.Vertical()
            .Item("Save", shortcut: "Ctrl+S")
            .Build();

        menu.Items.Count.ShouldBe(1);
        var item = menu.Items[0].ShouldBeOfType<MenuItem>();
        item.Text.ShouldBeOfType<ControlText>().Content.ShouldBe("Save");
        item.ShortcutText.ShouldBe("Ctrl+S");
        item.Kind.ShouldBe(MenuItemKind.Command);
    }

    /// <summary>Verifies Item with onInvoke wires the handler.</summary>
    [Fact]
    public void Item_WhenOnInvokeProvided_RaisesCallbackOnActivation()
    {
        var invoked = false;
        var menu = MenuBuilder.Vertical()
            .Item("Run", onInvoke: () => invoked = true)
            .Build();

        menu.Items[0].ShouldBeOfType<MenuItem>().PerformInvoke();

        invoked.ShouldBeTrue();
    }

    /// <summary>Verifies Item with isEnabled=false creates a disabled item.</summary>
    [Fact]
    public void Item_WhenDisabled_CreatesDisabledItem()
    {
        var menu = MenuBuilder.Vertical()
            .Item("Locked", isEnabled: false)
            .Build();

        menu.Items[0].ShouldBeOfType<MenuItem>().IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies Separator adds a MenuSeparator.</summary>
    [Fact]
    public void Separator_WhenAdded_InsertsSeparator()
    {
        var menu = MenuBuilder.Vertical()
            .Item("Before")
            .Separator()
            .Item("After")
            .Build();

        menu.Items.Count.ShouldBe(3);
        _ = menu.Items[0].ShouldBeOfType<MenuItem>();
        _ = menu.Items[1].ShouldBeOfType<MenuSeparator>();
        _ = menu.Items[2].ShouldBeOfType<MenuItem>();
    }

    /// <summary>Verifies Check creates a check item with the correct kind and state.</summary>
    [Fact]
    public void Check_WhenAdded_CreatesCheckItemWithState()
    {
        var menu = MenuBuilder.Vertical()
            .Check("Auto save", isChecked: true)
            .Build();

        var item = menu.Items[0].ShouldBeOfType<MenuItem>();
        item.Kind.ShouldBe(MenuItemKind.Check);
        item.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies Radio creates a radio item with group name and selection.</summary>
    [Fact]
    public void Radio_WhenAdded_CreatesRadioItemInGroup()
    {
        var menu = MenuBuilder.Vertical()
            .Radio("Compact", "density", isChecked: true)
            .Radio("Spacious", "density")
            .Build();

        var first = menu.Items[0].ShouldBeOfType<MenuItem>();
        var second = menu.Items[1].ShouldBeOfType<MenuItem>();
        first.Kind.ShouldBe(MenuItemKind.Radio);
        first.GroupName.ShouldBe("density");
        first.IsChecked.ShouldBeTrue();
        second.Kind.ShouldBe(MenuItemKind.Radio);
        second.GroupName.ShouldBe("density");
        second.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies a complete chained builder produces the expected item sequence.</summary>
    [Fact]
    public void Build_WhenChained_ProducesFullMenu()
    {
        var menu = MenuBuilder.Vertical()
            .Item("New", shortcut: "Ctrl+N")
            .Item("Open", shortcut: "Ctrl+O")
            .Separator()
            .Check("Word wrap")
            .Separator()
            .Radio("Dark", "theme", isChecked: true)
            .Radio("Light", "theme")
            .Separator()
            .Item("Quit", shortcut: "Ctrl+Q")
            .Build();

        menu.Items.Count.ShouldBe(9);
        _ = menu.Items[0].ShouldBeOfType<MenuItem>();
        _ = menu.Items[1].ShouldBeOfType<MenuItem>();
        _ = menu.Items[2].ShouldBeOfType<MenuSeparator>();
        _ = menu.Items[3].ShouldBeOfType<MenuItem>();
        _ = menu.Items[4].ShouldBeOfType<MenuSeparator>();
        _ = menu.Items[5].ShouldBeOfType<MenuItem>();
        _ = menu.Items[6].ShouldBeOfType<MenuItem>();
        _ = menu.Items[7].ShouldBeOfType<MenuSeparator>();
        _ = menu.Items[8].ShouldBeOfType<MenuItem>();
    }

    /// <summary>Verifies Submenu creates a MenuItem with a nested Menu assigned to its Submenu property.</summary>
    [Fact]
    public void Submenu_WhenConfigured_CreatesItemWithSubmenuProperty()
    {
        var menu = MenuBuilder.Horizontal()
            .Submenu("File", file => file
                .Item("New")
                .Separator()
                .Item("Quit"))
            .Build();

        menu.Items.Count.ShouldBe(1);
        var item = menu.Items[0].ShouldBeOfType<MenuItem>();
        item.Text.ShouldBeOfType<ControlText>().Content.ShouldBe("File");
        var submenu = item.Submenu.ShouldNotBeNull();
        submenu.Items.Count.ShouldBe(3);
        _ = submenu.Items[0].ShouldBeOfType<MenuItem>();
        _ = submenu.Items[1].ShouldBeOfType<MenuSeparator>();
        _ = submenu.Items[2].ShouldBeOfType<MenuItem>();
    }

    /// <summary>Verifies a complete menu bar with multiple submenus builds correctly.</summary>
    [Fact]
    public void Build_WhenMenuBarWithSubmenus_ProducesHierarchicalStructure()
    {
        var menu = MenuBuilder.Horizontal(spacing: 2)
            .Submenu("File", f => f.Item("New").Item("Quit"))
            .Submenu("Edit", e => e.Item("Undo").Item("Redo"))
            .Build();

        menu.Orientation.ShouldBe(Orientation.Horizontal);
        menu.Items.Count.ShouldBe(2);

        var file = menu.Items[0].ShouldBeOfType<MenuItem>();
        file.Submenu.ShouldNotBeNull().Items.Count.ShouldBe(2);

        var edit = menu.Items[1].ShouldBeOfType<MenuItem>();
        edit.Submenu.ShouldNotBeNull().Items.Count.ShouldBe(2);
    }

    /// <summary>Verifies null label throws ArgumentNullException.</summary>
    [Fact]
    public void Item_WhenLabelIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => _ = MenuBuilder.Vertical().Item(null!));

    /// <summary>Verifies null label on Check throws ArgumentNullException.</summary>
    [Fact]
    public void Check_WhenLabelIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => _ = MenuBuilder.Vertical().Check(null!));

    /// <summary>Verifies null groupName on Radio throws ArgumentNullException.</summary>
    [Fact]
    public void Radio_WhenGroupNameIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => _ = MenuBuilder.Vertical().Radio("Label", null!));
}
