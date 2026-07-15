// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Menu control with command, check, radio, separator, orientation, and disabled-item specimens.</summary>
internal sealed class MenuPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var status = new Text("Choose an action.");
        var menu = new Menu()
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        menu.Items.Add(new MenuItem { Header = "New project" });
        menu.Items.Add(new MenuItem { Header = "Open recent" });
        menu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });
        menu.Items.Add(new MenuItem { Header = "Auto save", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Compact mode", Kind = MenuItemKind.Radio, GroupName = "density", IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Comfortable mode", Kind = MenuItemKind.Radio, GroupName = "density" });
        menu.ItemInvoked += (_, eventArgs) => status.Content = $"Invoked {eventArgs.Item.Header}.";

        var framed = new Dock()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { menu },
        };

        var barStatus = new Text("Choose a top-level action.");
        var bar = new Menu()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
        };
        bar.Items.Add(new MenuItem { Header = "File" });
        bar.Items.Add(new MenuItem { Header = "Edit" });
        bar.Items.Add(new MenuItem { Header = "View" });
        bar.Items.Add(new MenuItem { Header = "Help" });
        bar.ItemInvoked += (_, eventArgs) => barStatus.Content = $"Invoked {eventArgs.Item.Header}.";

        var framedBar = new Dock()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { bar },
        };

        var withDisabled = new Menu()
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        withDisabled.Items.Add(new MenuItem { Header = "Available action" });
        withDisabled.Items.Add(new MenuItem { Header = "Unavailable action", IsEnabled = false });
        withDisabled.Items.Add(new MenuItem { Header = "Another available action" });

        var framedDisabled = new Dock()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { withDisabled },
        };

        return Doc.Page(
            Title,
            "Arranges typed command, check, radio, and separator items with semantic selected state and keyboard navigation.",
            Doc.Example(
                "Command menu with state",
                "Arrow keys skip the separator while Enter or Space activates the selected item, or click one directly with the pointer. Check and radio state commits before the invocation message below updates.",
                Doc.Column(framed, status)),
            Doc.Example(
                "Horizontal menu bar",
                "Orientation.Horizontal lays items out left to right with Left and Right arrow navigation instead of Up and Down, the shape of a conventional top-level menu bar.",
                Doc.Column(framedBar, barStatus)),
            Doc.Example(
                "Disabled item",
                "A disabled MenuItem is skipped by arrow-key navigation and ignores activation, just like a disabled Button.",
                framedDisabled));
    }
}
