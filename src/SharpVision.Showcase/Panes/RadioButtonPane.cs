// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the RadioButton control with grouped, mutually exclusive selection specimens.</summary>
internal sealed class RadioButtonPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RadioButton";

    /// <inheritdoc/>
    protected override Control Build()
    {
        RadioButton fast = new() { Content = new Text("Fast"), GroupName = "quality", IsChecked = true };
        RadioButton balanced = new() { Content = new Text("Balanced"), GroupName = "quality" };
        RadioButton unavailable = new()
        {
            Content = new Text("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
        };
        Border group = new()
        {
            Child = Doc.Column(fast, balanced, unavailable),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
        };

        RadioButton independent = new()
        {
            Content = new Text("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
        };
        Border separate = new()
        {
            Child = independent,
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
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
