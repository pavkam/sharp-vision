// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents application theming, style cascades, and third-party style properties.</summary>
internal sealed class ThemingPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Theming";

    /// <inheritdoc/>
    protected override Control Build()
    {
        ShowcasePanel panel = new();

        Button left = new() { Content = new Text("Left") };
        Button right = new() { Content = new Text("Right") };
        left.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Left;
        right.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Right;

        Stack placement = Doc.Column(
            new Text("Label placement"),
            Doc.Row(left, right));

        return Doc.Page(
            Title,
            "Demonstrates application themes, type-keyed styles, local overrides, and third-party style properties.",
            Doc.Example(
                "Application theme",
                "Use the theme picker in the sidebar footer. Application.Theme publishes a frozen snapshot to every attached control without ancestor-style inheritance.",
                panel),
            Doc.Example(
                "Third-party style property",
                "ShowcasePanel registers LabelPlacement through StyleProperty metadata. Themes and local values resolve it with the same cascade as built-in chrome.",
                placement));
    }
}
