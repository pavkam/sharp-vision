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
        Text status = new("Choose an action.");
        Menu menu = new()
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

        Dock framed = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { menu },
        };

        Text barStatus = new("Choose a top-level action.");
        Menu bar = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
        };
        bar.Items.Add(new MenuItem { Header = "File" });
        bar.Items.Add(new MenuItem { Header = "Edit" });
        bar.Items.Add(new MenuItem { Header = "View" });
        bar.Items.Add(new MenuItem { Header = "Help" });
        bar.ItemInvoked += (_, eventArgs) => barStatus.Content = $"Invoked {eventArgs.Item.Header}.";

        Dock framedBar = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { bar },
        };

        Menu withDisabled = new()
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        withDisabled.Items.Add(new MenuItem { Header = "Available action" });
        withDisabled.Items.Add(new MenuItem { Header = "Unavailable action", IsEnabled = false });
        withDisabled.Items.Add(new MenuItem { Header = "Another available action" });

        Dock framedDisabled = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { withDisabled },
        };

        var flyoutTrigger = new Button { Content = new Text("Project actions") };
        var flyoutMenu = new Menu { Orientation = Orientation.Vertical };
        flyoutMenu.Items.Add(new MenuItem { Header = "Build" });
        flyoutMenu.Items.Add(new MenuItem { Header = "Test" });
        flyoutMenu.Items.Add(new MenuItem { Header = "Publish" });
        var flyout = new Popup
        {
            Anchor = flyoutTrigger,
            Child = flyoutMenu,
            IsOpen = true,
            Placement = PopupPlacement.Below,
        };
        var flyoutStage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            ClipToBounds = false,
            Children = { flyoutTrigger, flyout },
        };

        var selectionStatus = new Text("Selected index: -1; invoked: none");
        var selectionMenu = new Menu { Orientation = Orientation.Vertical };
        selectionMenu.Items.Add(new MenuItem { Header = "Inspect" });
        selectionMenu.Items.Add(new MenuItem { Header = "Run" });
        selectionMenu.ItemInvoked += (_, eventArgs) =>
            selectionStatus.Content = $"Selected index: {selectionMenu.SelectedIndex}; invoked: {eventArgs.Item.Header}";

        var relaxed = new Menu { Orientation = Orientation.Horizontal, Spacing = 3 };
        relaxed.Items.Add(new MenuItem { Header = "Available" });
        relaxed.Items.Add(new MenuItem { Header = "Unavailable", IsEnabled = false });
        relaxed.Items.Add(new MenuItem { Header = "Next" });

        return Doc.Page(
            Title,
            "Arranges typed command, check, radio, and separator items with semantic selected state and keyboard navigation.",
            Doc.Section(
                "📑",
                "Command menu",
                "Combine command, separator, check, and radio items in one typed ownership collection.",
                Doc.Example(
                    "Commands with state",
                    "Arrows skip separators; Enter, Space, or a click commits check/radio state before ItemInvoked reports the action.",
                    Doc.Column(framed, status),
                    "var menu = new Menu();\nmenu.Items.Add(new MenuItem { Header = \"Open\" });\nmenu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });")),
            Doc.Section(
                "📑",
                "Menu bar",
                "Horizontal orientation switches traversal to Left and Right while preserving the same item semantics.",
                Doc.Example(
                    "Top-level application menu",
                    "Move across File, Edit, View, and Help, then activate the selected item through the same invocation event.",
                    Doc.Column(framedBar, barStatus))),
            Doc.Section(
                "📑",
                "Popup composition",
                "Menu remains an ordinary control; place it in Popup when an anchored flyout is required.",
                Doc.Example(
                    "Anchored project actions",
                    "The open Popup frames and promotes a vertical Menu above ordinary sibling content without inventing modal behavior.",
                    flyoutStage,
                    "var popup = new Popup { Anchor = trigger, Child = menu, IsOpen = true };")),
            Doc.Section(
                "📑",
                "Selection and invocation",
                "Keyboard navigation changes SelectedIndex; activation separately raises ItemInvoked after item state commits.",
                Doc.Example(
                    "Two observable states",
                    "Move without activating, then press Enter and compare the selected index with the invoked header.",
                    Doc.Column(selectionMenu, selectionStatus))),
            Doc.Section(
                "📑",
                "Spacing and unavailable items",
                "Spacing changes geometry only; unavailable entries remain visible and are skipped by focus and activation.",
                Doc.Example(
                    "Relaxed command strip",
                    "Move from Available to Next: the unavailable middle item never receives selection.",
                    Doc.Column(framedDisabled, relaxed))));
    }
}
