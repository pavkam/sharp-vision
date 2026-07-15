// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Overlay control with layered, z-ordered, aligned, and clipped specimens.</summary>
internal sealed class OverlayPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Overlay";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Overlay zOrder = new()
        {
            Width = Length.Cells(32),
            Height = Length.Cells(7),
            ClipToBounds = true,
        };
        Text back = new("Background layer") { Padding = new Thickness(1) };
        Overlay.SetZIndex(back, -1);
        zOrder.Children.Add(back);

        Dock middle = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Heavy,
            Padding = new Thickness(1, 0),
            Margin = new Thickness(4, 2, 4, 2),
            Children = { new Text("Middle layer") },
        };
        zOrder.Children.Add(middle);

        Text front = new("Front layer")
        {
            Attributes = TerminalAttributes.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Overlay.SetZIndex(front, 10);
        zOrder.Children.Add(front);

        Overlay alignment = new()
        {
            Width = Length.Cells(32),
            Height = Length.Cells(7),
            ClipToBounds = true,
        };
        alignment.Children.Add(new Text("Top-left") { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
        alignment.Children.Add(new Text("Top-right") { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
        alignment.Children.Add(new Text("Center") { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        alignment.Children.Add(new Text("Bottom-left") { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom });
        alignment.Children.Add(new Text("Bottom-right") { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });

        Dock oversizedContent = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Margin = new Thickness(6, 1, 0, 0),
            Children = { new Text("Overflowing card") },
        };
        Overlay clipped = new()
        {
            Width = Length.Cells(16),
            Height = Length.Cells(4),
            ClipToBounds = true,
        };
        clipped.Children.Add(oversizedContent);

        Dock unclippedContent = new()
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            Margin = new Thickness(6, 1, 0, 0),
            Children = { new Text("Overflowing card") },
        };
        Overlay unclipped = new()
        {
            Width = Length.Cells(16),
            Height = Length.Cells(4),
            ClipToBounds = false,
        };
        unclipped.Children.Add(unclippedContent);

        return Doc.Page(
            Title,
            "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.",
            Doc.Example(
                "Layered z-order",
                "Three children share the same content box. The Overlay.SetZIndex attached property orders rendering and hit testing: the background layer sits at -1, the middle card renders at the default 0, and the front label sits at 10 so it always wins overlapping pointer hits.",
                zOrder),
            Doc.Example(
                "Alignment variants",
                "Every child arranges within the same shared box, so HorizontalAlignment and VerticalAlignment alone place five labels at the four corners and the center without any explicit position or z-index.",
                alignment),
            Doc.Example(
                "Clip to bounds",
                "ClipToBounds, true by default, cuts a child's overflow at the overlay's edge; setting it to false, as Popup specimens do, lets the same oversized card render past the edge instead.",
                Doc.Row(
                    Doc.Column(new Text("Clipped"), clipped),
                    Doc.Column(new Text("Unclipped"), unclipped))));
    }
}
