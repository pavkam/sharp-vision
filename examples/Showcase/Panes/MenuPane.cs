// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Menu control with application-style menu bars, submenus, and context menus.</summary>
internal sealed class MenuPane: CompositeControlBase
{
    internal MenuPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var barStatus = new Text("Select a menu entry to see the invoked action.");

        // File menu with nested Open Recent submenu.
        var fileNew = new MenuItem { Text = "Ne&w" };
        var fileOpen = new MenuItem { Text = "&Open" };
        var fileRecentToday = new MenuItem { Text = "To&day" };
        var fileRecentArchive = new MenuItem { Text = "Ar&chive" };
        var fileRecentSubmenu = new Menu
        {
            Orientation = Orientation.Vertical,
            MinWidth = Length.Cells(15),
            MaxWidth = Length.Percent(75)
        };
        fileRecentSubmenu.Items.Add(fileRecentToday);
        fileRecentSubmenu.Items.Add(fileRecentArchive);
        var fileOpenRecent = new MenuItem { Text = "O&pen Recent", Submenu = fileRecentSubmenu };
        var fileSave = new MenuItem { Text = "&Save", ShortcutText = "Ctrl+S" };
        var fileSaveAs = new MenuItem { Text = "Save &As..." };
        var fileExit = new MenuItem { Text = "E&xit", ShortcutText = "Ctrl+Q" };

        var fileSubmenu = new Menu { Orientation = Orientation.Vertical };
        fileSubmenu.Items.Add(fileNew);
        fileSubmenu.Items.Add(fileOpen);
        fileSubmenu.Items.Add(fileOpenRecent);
        fileSubmenu.Items.Add(new MenuSeparator());
        fileSubmenu.Items.Add(fileSave);
        fileSubmenu.Items.Add(fileSaveAs);
        fileSubmenu.Items.Add(new MenuSeparator());
        fileSubmenu.Items.Add(fileExit);

        // Edit menu with shortcut hints.
        var editUndo = new MenuItem { Text = "Un&do", ShortcutText = "Ctrl+Z" };
        var editRedo = new MenuItem { Text = "Red&o", ShortcutText = "Ctrl+Y" };
        var editCut = new MenuItem { Text = "Cu&t" };
        var editCopy = new MenuItem { Text = "&Copy" };
        var editPaste = new MenuItem { Text = "&Paste" };

        var editSubmenu = new Menu { Orientation = Orientation.Vertical };
        editSubmenu.Items.Add(editUndo);
        editSubmenu.Items.Add(editRedo);
        editSubmenu.Items.Add(new MenuSeparator());
        editSubmenu.Items.Add(editCut);
        editSubmenu.Items.Add(editCopy);
        editSubmenu.Items.Add(editPaste);

        // View menu with check and radio items sharing one density group.
        var viewAutoSave =
            new MenuItem { Text = "&Auto save", Kind = MenuItemKind.Check, IsChecked = true };
        var viewCompact = new MenuItem
        {
            Text = "&Compact",
            Kind = MenuItemKind.Radio,
            GroupName = "density",
            IsChecked = true
        };
        var viewComfortable = new MenuItem
        {
            Text = "C&omfortable",
            Kind = MenuItemKind.Radio,
            GroupName = "density"
        };
        var viewSpacious = new MenuItem
        {
            Text = "&Spacious",
            Kind = MenuItemKind.Radio,
            GroupName = "density"
        };

        var viewSubmenu = new Menu { Orientation = Orientation.Vertical };
        viewSubmenu.Items.Add(viewAutoSave);
        viewSubmenu.Items.Add(new MenuSeparator());
        viewSubmenu.Items.Add(viewCompact);
        viewSubmenu.Items.Add(viewComfortable);
        viewSubmenu.Items.Add(viewSpacious);

        // Help menu completes the top-level bar.
        var helpAbout = new MenuItem { Text = "Ab&out" };
        var helpSubmenu = new Menu { Orientation = Orientation.Vertical };
        helpSubmenu.Items.Add(helpAbout);

        // SubmenuChrome demonstrates the owned-popup-presentation surface: a local override on
        // the owning MenuItem, with no private Popup access required.
        var fileItem = new MenuItem
        {
            Text = "&File",
            Submenu = fileSubmenu,
            SubmenuChrome = new PopupChrome
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Heavy,
                    Color.Rgb(0x77, 0xaa, 0xff),
                    Color.Transparent,
                    TerminalAttributes.None)
            }
        };
        var editItem = new MenuItem { Text = "&Edit", Submenu = editSubmenu };
        var viewItem = new MenuItem { Text = "&View", Submenu = viewSubmenu };
        var helpItem = new MenuItem { Text = "&Help", Submenu = helpSubmenu };

        var menuBar = new Menu { Orientation = Orientation.Horizontal, Spacing = 2 };
        menuBar.Items.Add(fileItem);
        menuBar.Items.Add(editItem);
        menuBar.Items.Add(viewItem);
        menuBar.Items.Add(new MenuSeparator());
        menuBar.Items.Add(helpItem);

        // PropertyChanged reports selection path; ItemInvoked reports committed activation.
        menuBar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Menu.SelectedIndex) &&
                menuBar.SelectedIndex >= 0 &&
                menuBar.Items[menuBar.SelectedIndex] is MenuItem item)
            {
                barStatus.Content = $"Menu path: {Label(item)}";
            }
        };
        fileSubmenu.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Menu.SelectedIndex) &&
                fileSubmenu.SelectedIndex >= 0 &&
                fileSubmenu.Items[fileSubmenu.SelectedIndex] is MenuItem item)
            {
                barStatus.Content = $"Menu path: File → {Label(item)}";
            }
        };

        void OnInvoked(object? sender, MenuItemInvokedEventArgs e) =>
            barStatus.Content = $"Invoked: {Label(e.Item)} ({e.Cause})";

        menuBar.ItemInvoked += OnInvoked;
        fileSubmenu.ItemInvoked += OnInvoked;
        fileRecentSubmenu.ItemInvoked += OnInvoked;
        editSubmenu.ItemInvoked += OnInvoked;
        viewSubmenu.ItemInvoked += OnInvoked;
        helpSubmenu.ItemInvoked += OnInvoked;

        var barFrame = new Dock
        {
            Height = Length.Cells(12),
            Border = new Border(
                BorderSide.Bottom,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { menuBar }
        };

        // MenuBuilder demonstrates the fluent construction API as an alternative to composing
        // Menu/MenuItem object graphs directly: Item, Check, Separator, Submenu, and Radio all
        // chain off the same builder instance and report through per-item onInvoke callbacks.
        var builderStatus = new Text("Select an entry to see the invoked action.");

        var builderMenu = MenuBuilder.Vertical()
            .Item("&Refresh", shortcut: "F5", onInvoke: () => builderStatus.Content = "Invoked: Refresh")
            .Item("Re&name", onInvoke: () => builderStatus.Content = "Invoked: Rename")
            .Check(
                "Show hidden fi&les",
                onInvoke: () => builderStatus.Content = "Invoked: Show hidden files")
            .Separator()
            .Submenu(
                "Sort b&y",
                sortBy => sortBy
                    .Radio(
                        "&Kind",
                        groupName: "sort",
                        isChecked: true,
                        onInvoke: () => builderStatus.Content = "Invoked: Sort by Kind")
                    .Radio(
                        "A&ge",
                        groupName: "sort",
                        onInvoke: () => builderStatus.Content = "Invoked: Sort by Age")
                    .Radio(
                        "Si&ze",
                        groupName: "sort",
                        onInvoke: () => builderStatus.Content = "Invoked: Sort by Size"))
            .Build();

        var builderFrame = AutoSizedFrame(builderMenu);

        // Standalone context menu with shortcuts and one disabled entry.
        var contextStatus = new Text("Use arrow keys to navigate. Enter or Space activates.");

        var contextMenu = new Menu { Orientation = Orientation.Vertical };
        contextMenu.Items.Add(new MenuItem { Text = "&Inspect" });
        contextMenu.Items.Add(new MenuItem { Text = "R&un", ShortcutText = "F5" });
        contextMenu.Items.Add(new MenuItem { Text = "De&bug", ShortcutText = "F9" });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Text = "De&ploy", IsEnabled = false });
        contextMenu.ItemInvoked += (_, e) =>
            contextStatus.Content = $"Context: {Label(e.Item)}";

        var contextFrame = AutoSizedFrame(contextMenu);

        // StartAffix reserves a fixed cell for an application-owned warning marker.
        var affixStatus = new Text("Use arrow keys to navigate. Enter or Space activates.");
        var affixMenu = new Menu { Orientation = Orientation.Vertical };
        affixMenu.Items.Add(new MenuItem { Text = "Rena&me" });
        affixMenu.Items.Add(new MenuItem
        {
            Text = "Delete pro&ject",
            StartAffix = new Affix("⚠", "!", SemanticColor.Error)
        });
        affixMenu.ItemInvoked += (_, e) => affixStatus.Content = $"Invoked: {Label(e.Item)}";

        var affixFrame = AutoSizedFrame(affixMenu);

        var roleStatus = new Text("Select an entry to see the invoked action.");
        var roleSubmenu = new Menu
        {
            Orientation = Orientation.Vertical,
            MaxWidth = Length.Cells(18)
        };
        roleSubmenu.Items.Add(new MenuItem { Text = "&Details" });
        roleSubmenu.Items.Add(new MenuItem { Text = "&About" });
        var roleMenu = new Menu
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        roleMenu.Items.Add(new MenuItem { Text = "&New" });
        roleMenu.Items.Add(new MenuItem { Text = "More &options", Submenu = roleSubmenu });
        roleMenu.Items.Add(new MenuSeparator());
        roleMenu.Items.Add(new MenuItem { Text = "&Save", ShortcutText = "Ctrl+S" });
        roleMenu.Items.Add(new MenuItem { Text = "&Undo", ShortcutText = "Ctrl+Z" });
        roleMenu.Items.Add(new MenuItem
        {
            Text = "Au&to save",
            Kind = MenuItemKind.Check,
            IsChecked = true
        });
        roleMenu.Items.Add(new MenuItem
        {
            Text = "&Compact",
            Kind = MenuItemKind.Radio,
            GroupName = "density",
            IsChecked = true
        });
        roleMenu.Items.Add(new MenuItem
        {
            Text = "Co&mfortable",
            Kind = MenuItemKind.Radio,
            GroupName = "density"
        });
        roleMenu.Items.Add(new MenuItem { Text = "&Deploy", IsEnabled = false });
        roleMenu.ItemInvoked += (_, eventArgs) =>
            roleStatus.Content = $"Invoked: {Label(eventArgs.Item)} ({eventArgs.Cause})";
        var roleFrame = new Dock
        {
            Width = Length.Cells(44),
            LastChildFills = false,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Padding = new Thickness(1),
            Children = { roleMenu }
        };

        return new DocPage(
            Title,
            "<info>Menu</info> arranges command, check, radio, and separator items with keyboard navigation, submenus, and selection tracking.",
            new DocSection(
                "📑",
                "Menu bar with submenus",
                "The top menu owns one <info>Dismiss</info> plane for its complete popup chain. Submenus default to a 15-cell minimum; Open Recent demonstrates direct <info>MinWidth</info>/<info>MaxWidth</info> bounds. Hover or arrows switch an open sibling inside that plane, and <reverse>Escape</reverse> restores the owning menu.",
                new DocExample(
                    "Application menu bar",
                    "Click or press <reverse>Enter</reverse> on File, Edit, View, or Help, then hover another heading to switch without leaving the menu plane. <reverse>Tab</reverse> and arrows move selection, the vertical divider is skipped, <reverse>Right</reverse> opens a row marked with ▶, and <reverse>Left</reverse> returns one submenu level.",
                    new DocColumn(barFrame, barStatus),
                    "var file = new MenuItem { Text = \"&File\" };\nvar openRecentMenu = new Menu\n{\n    Orientation = Orientation.Vertical,\n    MinWidth = Length.Cells(15),\n    MaxWidth = Length.Percent(75),\n};\nopenRecentMenu.Items.Add(new MenuItem { Text = \"To&day\" });\nvar openRecent = new MenuItem { Text = \"O&pen Recent\", Submenu = openRecentMenu };\nvar fileMenu = new Menu { Orientation = Orientation.Vertical };\nfileMenu.Items.Add(new MenuItem { Text = \"Ne&w\" });\nfileMenu.Items.Add(openRecent);\nfile.Submenu = fileMenu;")),
            new DocSection(
                "🧾",
                "Menu entry roles",
                "One deterministic roster keeps every MenuItem and MenuSeparator role on the primary Menu page while the public helper types remain independently documented.",
                new DocExample(
                    "Every item role",
                    "Open More options to inspect the submenu. The roster also includes a plain command, separator, aligned shortcuts, a checked item, a two-way radio group, and a disabled action.",
                    new DocColumn(roleFrame, roleStatus),
                    "menu.Items.Add(new MenuItem { Text = \"&New\" });\n" +
                    "menu.Items.Add(new MenuItem { Text = \"More &options\", Submenu = submenu });\n" +
                    "menu.Items.Add(new MenuSeparator());\n" +
                    "menu.Items.Add(new MenuItem { Text = \"&Save\", ShortcutText = \"Ctrl+S\" });\n" +
                    "menu.Items.Add(new MenuItem\n{\n    Text = \"Au&to save\",\n    Kind = MenuItemKind.Check,\n    IsChecked = true,\n});\n" +
                    "menu.Items.Add(new MenuItem { Text = \"&Deploy\", IsEnabled = false });")),
            new DocSection(
                "🧰",
                "Building a menu with MenuBuilder",
                "<info>MenuBuilder</info> composes a <info>Menu</info> fluently instead of assembling <info>MenuItem</info> objects and an <info>Items</info> collection by hand. <info>Item</info>, <info>Check</info>, and <info>Radio</info> each take an optional <info>onInvoke</info> callback, and <info>Submenu</info> nests a fresh builder for the popup that opens on activation.",
                new DocExample(
                    "Fluent builder chain",
                    "Navigate with <reverse>Up</reverse>/<reverse>Down</reverse> arrows and <reverse>Enter</reverse> to invoke. <reverse>Right</reverse> opens Sort by and <reverse>Left</reverse> returns; the three entries share one radio group.",
                    new DocColumn(builderFrame, builderStatus),
                    "var menu = MenuBuilder.Vertical()\n    .Item(\"&Refresh\", shortcut: \"F5\", onInvoke: Refresh)\n    .Item(\"Re&name\", onInvoke: Rename)\n    .Check(\"Show hidden fi&les\", onInvoke: ToggleHidden)\n    .Separator()\n    .Submenu(\"Sort b&y\", sortBy => sortBy\n        .Radio(\"&Kind\", groupName: \"sort\", isChecked: true, onInvoke: SortByKind)\n        .Radio(\"A&ge\", groupName: \"sort\", onInvoke: SortByAge)\n        .Radio(\"Si&ze\", groupName: \"sort\", onInvoke: SortBySize))\n    .Build();")),
            new DocSection(
                "📑",
                "Context menu",
                "A standalone vertical menu with shortcut hints and a disabled entry that is visible but skipped by keyboard navigation.",
                new DocExample(
                    "Action menu with shortcuts and disabled entry",
                    "Navigate with <reverse>Up</reverse>/<reverse>Down</reverse> arrows. <reverse>Enter</reverse> invokes. The Deploy entry is <warning>disabled</warning> and cannot be selected or invoked.",
                    new DocColumn(contextFrame, contextStatus))),
            new DocSection(
                "⚠️",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside an item's text for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Warning glyph before a destructive entry",
                    "The glyph marks a destructive action; it sits in its own reserved cell before the caption and never shares space with the shortcut column.",
                    new DocColumn(affixFrame, affixStatus),
                    "menu.Items.Add(new MenuItem\n{\n    Text = \"Delete pro&ject\",\n    StartAffix = new Affix(\"⚠\", \"!\", SemanticColor.Error)\n});")));
    }

    private static string Label(MenuItem item) => DocCaption.PlainCaption(item.Text);

    private static Dock AutoSizedFrame(ControlBase child) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        AutoSize = true,
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Children = { child }
    };
}
