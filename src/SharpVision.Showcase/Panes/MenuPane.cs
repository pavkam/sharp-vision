// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Menu control with application-style menu bars, submenus, and context menus.</summary>
internal sealed class MenuPane: CompositeControl
{

    internal MenuPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var barStatus = new Text("Select a menu entry to see the invoked action.");

        var fileNew = new MenuItem { Content = new Text("New") };
        var fileOpen = new MenuItem { Content = new Text("Open") };
        var fileRecentToday = new MenuItem { Content = new Text("Today") };
        var fileRecentArchive = new MenuItem { Content = new Text("Archive") };
        var fileRecentSubmenu = new Menu { Orientation = Orientation.Vertical };
        fileRecentSubmenu.Items.Add(fileRecentToday);
        fileRecentSubmenu.Items.Add(fileRecentArchive);
        var fileOpenRecent = new MenuItem
        {
            Content = new Text("Open Recent"),
            Submenu = fileRecentSubmenu,
        };
        var fileSave = new MenuItem { Content = new Text("Save"), ShortcutText = "Ctrl+S" };
        var fileSaveAs = new MenuItem { Content = new Text("Save As...") };
        var fileExit = new MenuItem { Content = new Text("Exit"), ShortcutText = "Ctrl+Q" };

        var fileSubmenu = new Menu { Orientation = Orientation.Vertical };
        fileSubmenu.Items.Add(fileNew);
        fileSubmenu.Items.Add(fileOpen);
        fileSubmenu.Items.Add(fileOpenRecent);
        fileSubmenu.Items.Add(new MenuSeparator());
        fileSubmenu.Items.Add(fileSave);
        fileSubmenu.Items.Add(fileSaveAs);
        fileSubmenu.Items.Add(new MenuSeparator());
        fileSubmenu.Items.Add(fileExit);

        var editUndo = new MenuItem { Content = new Text("Undo"), ShortcutText = "Ctrl+Z" };
        var editRedo = new MenuItem { Content = new Text("Redo"), ShortcutText = "Ctrl+Y" };
        var editCut = new MenuItem { Content = new Text("Cut") };
        var editCopy = new MenuItem { Content = new Text("Copy") };
        var editPaste = new MenuItem { Content = new Text("Paste") };

        var editSubmenu = new Menu { Orientation = Orientation.Vertical };
        editSubmenu.Items.Add(editUndo);
        editSubmenu.Items.Add(editRedo);
        editSubmenu.Items.Add(new MenuSeparator());
        editSubmenu.Items.Add(editCut);
        editSubmenu.Items.Add(editCopy);
        editSubmenu.Items.Add(editPaste);

        var viewAutoSave = new MenuItem { Content = new Text("Auto save"), Kind = MenuItemKind.Check, IsChecked = true };
        var viewCompact = new MenuItem { Content = new Text("Compact"), Kind = MenuItemKind.Radio, GroupName = "density", IsChecked = true };
        var viewComfortable = new MenuItem { Content = new Text("Comfortable"), Kind = MenuItemKind.Radio, GroupName = "density" };
        var viewSpacious = new MenuItem { Content = new Text("Spacious"), Kind = MenuItemKind.Radio, GroupName = "density" };

        var viewSubmenu = new Menu { Orientation = Orientation.Vertical };
        viewSubmenu.Items.Add(viewAutoSave);
        viewSubmenu.Items.Add(new MenuSeparator());
        viewSubmenu.Items.Add(viewCompact);
        viewSubmenu.Items.Add(viewComfortable);
        viewSubmenu.Items.Add(viewSpacious);

        var helpAbout = new MenuItem { Content = new Text("About") };
        var helpSubmenu = new Menu { Orientation = Orientation.Vertical };
        helpSubmenu.Items.Add(helpAbout);

        var fileItem = new MenuItem { Content = new Text("File"), Submenu = fileSubmenu };
        var editItem = new MenuItem { Content = new Text("Edit"), Submenu = editSubmenu };
        var viewItem = new MenuItem { Content = new Text("View"), Submenu = viewSubmenu };
        var helpItem = new MenuItem { Content = new Text("Help"), Submenu = helpSubmenu };

        var menuBar = new Menu { Orientation = Orientation.Horizontal, Spacing = 2 };
        menuBar.Items.Add(fileItem);
        menuBar.Items.Add(editItem);
        menuBar.Items.Add(viewItem);
        menuBar.Items.Add(helpItem);

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
            Background = ColorRole.Surface,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderGlyphs = Glyphs.Light,
            Padding = new Thickness(1, 0),
            Children = { menuBar },
        };

        var contextStatus = new Text("Use arrow keys to navigate. Enter or Space activates.");

        var contextMenu = new Menu { Orientation = Orientation.Vertical };
        contextMenu.Items.Add(new MenuItem { Content = new Text("Inspect") });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Run"), ShortcutText = "F5" });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Debug"), ShortcutText = "F9" });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Content = new Text("Deploy"), IsEnabled = false });
        contextMenu.ItemInvoked += (_, e) =>
            contextStatus.Content = $"Context: {Label(e.Item)}";

        var contextFrame = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutoSize = true,
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { contextMenu },
        };

        return Doc.Page(
            Title,
            "Arranges command, check, radio, and separator items with keyboard navigation, submenus, and selection tracking.",
            Doc.Section(
                "📑",
                "Menu bar with submenus",
                "A horizontal menu bar where each top-level entry owns a compact vertical submenu. Hover switches an open sibling popup, Open Recent demonstrates right-side nesting, and Escape restores the owning menu.",
                Doc.Example(
                    "Application menu bar",
                    "Click or Enter on File, Edit, View, or Help, then hover another heading to switch. Tab and arrows move selection; Open Recent extends to the right. Shortcuts share one trailing edge and separators span the menu.",
                    Doc.Column(barFrame, barStatus),
                    "var file = new MenuItem { Content = new Text(\"File\") };\nvar fileMenu = new Menu { Orientation = Orientation.Vertical };\nfileMenu.Items.Add(new MenuItem { Content = new Text(\"New\") });\nfile.Submenu = fileMenu;")),
            Doc.Section(
                "📑",
                "Context menu",
                "A standalone vertical menu with shortcut hints and a disabled entry that is visible but skipped by keyboard navigation.",
                Doc.Example(
                    "Action menu with shortcuts and disabled entry",
                    "Navigate with Up/Down arrows. Enter invokes. The Deploy entry is disabled and cannot be selected or invoked.",
                    Doc.Column(contextFrame, contextStatus))));
    }

    private static string Label(MenuItem item) => ((Text) item.Content!).Content;
}
