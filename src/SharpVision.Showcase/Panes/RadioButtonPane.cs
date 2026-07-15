// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the RadioButton control with grouped, mutually exclusive selection specimens.</summary>
internal sealed class RadioButtonPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RadioButton";

    /// <summary>Initializes the retained RadioButton documentation page.</summary>
    internal RadioButtonPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var fast = new RadioButton() { Content = new Text("Fast"), GroupName = "quality", IsChecked = true };
        var balanced = new RadioButton() { Content = new Text("Balanced"), GroupName = "quality" };
        var unavailable = new RadioButton()
        {
            Content = new Text("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
        };
        var group = new Dock()
        {
            Children = { Doc.Column(fast, balanced, unavailable) },
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
        };

        var independent = new RadioButton()
        {
            Content = new Text("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
        };
        var separate = new Dock()
        {
            Children = { independent },
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Light,
            Padding = new Thickness(1, 0),
        };

        return Doc.Page(
            Title,
            "Selects one option from an ordinally named group scoped to the attached control root.",
            Doc.Example(
                "Named quality group",
                "Space, a pointer click, or arrow-key navigation picks one member of the quality group; the previously checked peer clears atomically. The disabled member stays visibly unavailable and is skipped by focus and keyboard navigation.",
                group),
            Doc.Example(
                "Separate group",
                "A different GroupName scopes selection independently, so checking this member never disturbs the quality group above.",
                separate));
    }
}
