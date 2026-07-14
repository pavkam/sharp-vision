// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using TerminalAttributes = Terminal.Rendering.Attributes;
using Text = SharpVision.Controls.Text;

/// <summary>Documents the Overlay control with layered, z-ordered specimens.</summary>
internal sealed class OverlayPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Overlay";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Overlay overlay = new()
        {
            Width = Length.Cells(32),
            Height = Length.Cells(7),
            ClipToBounds = true,
        };
        Text back = new("Background layer") { Padding = new Thickness(1) };
        Overlay.SetZIndex(back, -1);
        overlay.Children.Add(back);

        Border middle = new()
        {
            Child = new Text("Middle layer"),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Heavy,
            Padding = new Thickness(1, 0),
            Margin = new Thickness(4, 2, 4, 2),
        };
        overlay.Children.Add(middle);

        Text front = new("Front layer")
        {
            Attributes = TerminalAttributes.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Overlay.SetZIndex(front, 10);
        overlay.Children.Add(front);

        return Doc.Page(
            Title,
            "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.",
            Doc.Example(
                "Layered z-order",
                "Three children share the same content box. The Overlay.SetZIndex attached property orders rendering and hit testing: the background layer sits at -1, the middle card renders at the default 0, and the front label sits at 10 so it always wins overlapping pointer hits.",
                overlay));
    }
}
