// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Menu control with application-style menu bars, submenus, and context menus.</summary>
internal sealed class MenuPane: CompositeControl
{
    internal MenuPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var barStatus = new Text("Select a menu entry to see the invoked action.");

        // File menu with nested Open Recent submenu.
        var fileNew = new MenuItem { Content = new Text("&New") };
        var fileOpen = new MenuItem { Content = new Text("&Open") };
        var fileRecentToday = new MenuItem { Content = new Text("&Today") };
        var fileRecentArchive = new MenuItem { Content = new Text("Ar&chive") };
        var fileRecentSubmenu = new Menu
        {
            Orientation = Orientation.Vertical,
            MinWidth = 14,
            MaxWidth = 24
        };
        fileRecentSubmenu.Items.Add(fileRecentToday);
        fileRecentSubmenu.Items.Add(fileRecentArchive);
        var fileOpenRecent = new MenuItem { Content = new Text("Open &Recent"), Submenu = fileRecentSubmenu };
        var fileSave = new MenuItem { Content = new Text("&Save"), ShortcutText = "Ctrl+S" };
        var fileSaveAs = new MenuItem { Content = new Text("Save &As...") };
        var fileExit = new MenuItem { Content = new Text("E&xit"), ShortcutText = "Ctrl+Q" };

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
        var editUndo = new MenuItem { Content = new Text("&Undo"), ShortcutText = "Ctrl+Z" };
        var editRedo = new MenuItem { Content = new Text("&Redo"), ShortcutText = "Ctrl+Y" };
        var editCut = new MenuItem { Content = new Text("Cu&t") };
        var editCopy = new MenuItem { Content = new Text("&Copy") };
        var editPaste = new MenuItem { Content = new Text("&Paste") };

        var editSubmenu = new Menu { Orientation = Orientation.Vertical };
        editSubmenu.Items.Add(editUndo);
        editSubmenu.Items.Add(editRedo);
        editSubmenu.Items.Add(new MenuSeparator());
        editSubmenu.Items.Add(editCut);
        editSubmenu.Items.Add(editCopy);
        editSubmenu.Items.Add(editPaste);

        // View menu with check and radio items sharing one density group.
        var viewAutoSave =
            new MenuItem { Content = new Text("&Auto save"), Kind = MenuItemKind.Check, IsChecked = true };
        var viewCompact = new MenuItem
        {
            Content = new Text("&Compact"),
            Kind = MenuItemKind.Radio,
            GroupName = "density",
            IsChecked = true
        };
        var viewComfortable = new MenuItem
        {
            Content = new Text("C&omfortable"),
            Kind = MenuItemKind.Radio,
            GroupName = "density"
        };
        var viewSpacious = new MenuItem
        {
            Content = new Text("&Spacious"),
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
        var helpAbout = new MenuItem { Content = new Text("&About") };
        var helpSubmenu = new Menu { Orientation = Orientation.Vertical };
        helpSubmenu.Items.Add(helpAbout);

        var fileItem = new MenuItem { Content = new Text("&File"), Submenu = fileSubmenu };
        var editItem = new MenuItem { Content = new Text("&Edit"), Submenu = editSubmenu };
        var viewItem = new MenuItem { Content = new Text("&View"), Submenu = viewSubmenu };
        var helpItem = new MenuItem { Content = new Text("&Help"), Submenu = helpSubmenu };

        var menuBar = new Menu { Orientation = Orientation.Horizontal, Spacing = 2 };
        menuBar.Items.Add(fileItem);
        menuBar.Items.Add(editItem);
        menuBar.Items.Add(viewItem);
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
            Border = new Border(
                BorderSide.Bottom,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { menuBar }
        };

        // Standalone context menu with shortcuts and one disabled entry.
        var contextStatus = new Text("Use arrow keys to navigate. Enter or Space activates.");

        var contextMenu = new Menu { Orientation = Orientation.Vertical };
        contextMenu.Items.Add(new MenuItem { Content = new Text("&Inspect") });
        contextMenu.Items.Add(new MenuItem { Content = new Text("&Run"), ShortcutText = "F5" });
        contextMenu.Items.Add(new MenuItem { Content = new Text("&Debug"), ShortcutText = "F9" });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Content = new Text("De&ploy"), IsEnabled = false });
        contextMenu.ItemInvoked += (_, e) =>
            contextStatus.Content = $"Context: {Label(e.Item)}";

        var contextFrame = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutoSize = true,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { contextMenu }
        };

        return new DocPage(
            Title,
            "<info>Menu</info> arranges command, check, radio, and separator items with keyboard navigation, submenus, and selection tracking.",
            new DocSection(
                "📑",
                "Menu bar with submenus",
                "The top menu owns one <info>Dismiss</info> plane for its complete popup chain. Submenus default to a 10-cell minimum; Open Recent demonstrates direct <info>MinWidth</info>/<info>MaxWidth</info> bounds. Hover or arrows switch an open sibling inside that plane, and <reverse>Escape</reverse> restores the owning menu.",
                new DocExample(
                    "Application menu bar",
                    "Click or press <reverse>Enter</reverse> on File, Edit, View, or Help, then hover another heading to switch without leaving the menu plane. <reverse>Tab</reverse> and arrows move selection; Open Recent extends the same plane to the right.",
                    new DocColumn(barFrame, barStatus),
                    "var file = new MenuItem { Content = new Text(\"&File\") };\nvar fileMenu = new Menu\n{\n    Orientation = Orientation.Vertical,\n    MinWidth = 14,\n    MaxWidth = 24,\n};\nfileMenu.Items.Add(new MenuItem { Content = new Text(\"&New\") });\nfile.Submenu = fileMenu;")),
            new DocSection(
                "📑",
                "Context menu",
                "A standalone vertical menu with shortcut hints and a disabled entry that is visible but skipped by keyboard navigation.",
                new DocExample(
                    "Action menu with shortcuts and disabled entry",
                    "Navigate with <reverse>Up</reverse>/<reverse>Down</reverse> arrows. <reverse>Enter</reverse> invokes. The Deploy entry is <warning>disabled</warning> and cannot be selected or invoked.",
                    new DocColumn(contextFrame, contextStatus))));
    }

    private static string Label(MenuItem item) => DocCaption.PlainCaption(((Text) item.Content!).Content);
}
