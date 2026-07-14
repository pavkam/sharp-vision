// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Menu control with command, check, radio, and separator specimens.</summary>
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

        Border framed = new()
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            Child = menu,
        };

        return Doc.Page(
            Title,
            "Arranges typed command, check, radio, and separator items with semantic selected state and keyboard navigation.",
            Doc.Example(
                "Command menu",
                "Arrow keys skip the separator while Enter or Space activates the selected item, or click one directly with the pointer. Check and radio state commits before the invocation message below updates.",
                Doc.Column(framed, status)));
    }
}
