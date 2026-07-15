// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Menu control with command, check, radio, separator, orientation, and disabled-item specimens.</summary>
internal sealed class MenuPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <summary>Initializes the retained Menu documentation page.</summary>
    internal MenuPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var status = new Text("Choose an action.");
        var menu = new Menu()
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        menu.Items.Add(new MenuItem { Content = new Text("New project") });
        menu.Items.Add(new MenuItem { Content = new Text("Open recent") });
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(new MenuItem { Content = new Text("Auto save"), Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Content = new Text("Compact mode"), Kind = MenuItemKind.Radio, GroupName = "density", IsChecked = true });
        menu.Items.Add(new MenuItem { Content = new Text("Comfortable mode"), Kind = MenuItemKind.Radio, GroupName = "density" });
        menu.ItemInvoked += (_, eventArgs) =>
            status.Content = $"Invoked {((Text) eventArgs.Item.Content!).Content}.";

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
        bar.Items.Add(new MenuItem { Content = new Text("File") });
        bar.Items.Add(new MenuItem { Content = new Text("Edit") });
        bar.Items.Add(new MenuItem { Content = new Text("View") });
        bar.Items.Add(new MenuItem { Content = new Text("Help") });
        bar.ItemInvoked += (_, eventArgs) =>
            barStatus.Content = $"Invoked {((Text) eventArgs.Item.Content!).Content}.";

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
        withDisabled.Items.Add(new MenuItem { Content = new Text("Available action") });
        withDisabled.Items.Add(new MenuItem { Content = new Text("Unavailable action"), IsEnabled = false });
        withDisabled.Items.Add(new MenuItem { Content = new Text("Another available action") });

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
