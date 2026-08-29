// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the MenuItem and MenuSeparator entry roles with one deterministic roster
/// that is not simply a whole-Menu specimen shared with the Menu page.</summary>
internal sealed class MenuItemPane: CompositeControlBase
{
    internal MenuItemPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "MenuItem";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var status = new Text("Select an entry to see the invoked action.");

        // A submenu owned by "More options" demonstrates the item's submenu affordance without
        // duplicating the application-menu-bar specimen the Menu page already captures.
        var infoSubmenu = new Menu { Orientation = Orientation.Vertical, MinWidth = Length.Cells(12), MaxWidth = Length.Cells(18) };
        infoSubmenu.Items.Add(new MenuItem { Text = "&Details" });
        infoSubmenu.Items.Add(new MenuItem { Text = "&About" });

        var newItem = new MenuItem { Text = "&New" };
        var moreItem = new MenuItem { Text = "More &options", Submenu = infoSubmenu };
        var saveItem = new MenuItem { Text = "&Save", ShortcutText = "Ctrl+S" };
        var undoItem = new MenuItem { Text = "&Undo", ShortcutText = "Ctrl+Z" };
        var autoSaveItem = new MenuItem { Text = "Au&to save", Kind = MenuItemKind.Check, IsChecked = true };
        var compactItem = new MenuItem
        {
            Text = "&Compact",
            Kind = MenuItemKind.Radio,
            GroupName = "density",
            IsChecked = true
        };
        var comfortableItem = new MenuItem
        {
            Text = "Co&mfortable",
            Kind = MenuItemKind.Radio,
            GroupName = "density"
        };
        var deployItem = new MenuItem { Text = "&Deploy", IsEnabled = false };

        // Left-aligned rather than the default stretch so the roster keeps its own compact
        // width inside the wider frame below, leaving room for the submenu to open beside it.
        var roster = new Menu { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
        roster.Items.Add(newItem);
        roster.Items.Add(moreItem);
        roster.Items.Add(new MenuSeparator());
        roster.Items.Add(saveItem);
        roster.Items.Add(undoItem);
        roster.Items.Add(autoSaveItem);
        roster.Items.Add(compactItem);
        roster.Items.Add(comfortableItem);
        roster.Items.Add(deployItem);
        roster.ItemInvoked += (_, e) => status.Content = $"Invoked: {Label(e.Item)} ({e.Cause})";

        Dock.SetSide(roster, DockSide.Left);

        // The submenu popup opens at root level, anchored to its owning item rather than laid
        // out inside this frame, so the frame's own fixed width only needs to be wide enough that
        // a captured "More options" submenu still renders inside this bordered surface: roster
        // plus submenu.
        var rosterFrame = new Dock
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
            Children = { roster }
        };

        return new DocPage(
            Title,
            "<info>MenuItem</info> and <info>MenuSeparator</info> supply the command, check, radio, shortcut, submenu, and separator entries a <info>Menu</info> composes.",
            new DocSection(
                "🧾",
                "Every item role in one deterministic roster",
                "One vertical list shows every entry role together: a plain command, an item that owns a submenu, a separator, two shortcut-aligned commands sharing one trailing column, a checked entry, a two-way radio group, and a disabled entry.",
                new DocExample(
                    "Command, submenu, separator, shortcut, check, radio, and disabled roles",
                    "Click or press <reverse>Enter</reverse> on More options to open its submenu. Arrow keys navigate; the Deploy entry is <warning>disabled</warning> and cannot be selected or invoked.",
                    new DocColumn(rosterFrame, status),
                    "menu.Items.Add(new MenuItem { Text = \"&New\" });\n" +
                        "menu.Items.Add(new MenuItem { Text = \"More &options\", Submenu = submenu });\n" +
                        "menu.Items.Add(new MenuSeparator());\n" +
                        "menu.Items.Add(new MenuItem { Text = \"&Save\", ShortcutText = \"Ctrl+S\" });\n" +
                        "menu.Items.Add(new MenuItem\n{\n    Text = \"Au&to save\",\n    Kind = MenuItemKind.Check,\n    IsChecked = true,\n});\n" +
                        "menu.Items.Add(new MenuItem { Text = \"&Deploy\", IsEnabled = false });")));
    }

    private static string Label(MenuItem item) => DocCaption.PlainCaption(item.Text);
}
