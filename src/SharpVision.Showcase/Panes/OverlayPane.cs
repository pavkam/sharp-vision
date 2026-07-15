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

        var equalTies = new Overlay { Width = Length.Cells(30), Height = Length.Cells(4) };
        var tieFirst = new Text("First at z=5") { HorizontalAlignment = HorizontalAlignment.Left };
        var tieSecond = new Text("Second at z=5") { HorizontalAlignment = HorizontalAlignment.Right };
        Overlay.SetZIndex(tieFirst, 5);
        Overlay.SetZIndex(tieSecond, 5);
        equalTies.Children.Add(tieFirst);
        equalTies.Children.Add(tieSecond);

        var pointerStatus = new Text("Pointer: waiting");
        var underlying = new Button { Content = new Text("Underlying action") };
        underlying.Click += (_, eventArgs) => pointerStatus.Content = $"Pointer: action received ({eventArgs.Cause})";
        var decoration = new Text("Decorative overlay")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Attributes = TerminalAttributes.Dim,
        };
        var transparent = new Overlay { Width = Length.Cells(32), Height = Length.Cells(5) };
        transparent.Children.Add(underlying);
        Overlay.SetZIndex(decoration, 10);
        transparent.Children.Add(decoration);

        var percent = new Overlay { Width = Length.Cells(32), Height = Length.Cells(6) };
        percent.Children.Add(new Dock
        {
            Width = Length.Percent(60),
            Height = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { new Text("60% × 50% centered") },
        });

        var notification = new Overlay { Width = Length.Cells(36), Height = Length.Cells(6) };
        notification.Children.Add(new Text("Editor content remains first in focus order."));
        var banner = Doc.Card(new Text("Saved successfully"));
        banner.HorizontalAlignment = HorizontalAlignment.Right;
        banner.VerticalAlignment = VerticalAlignment.Top;
        Overlay.SetZIndex(banner, 20);
        notification.Children.Add(banner);

        return Doc.Page(
            Title,
            "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.",
            Doc.Section(
                "🧩",
                "Layering",
                "All children share one content box while attached z-order controls paint and pointer priority.",
                Doc.Example(
                    "Negative, default, and high z",
                    "Background paints at -1, the framed middle at 0, and the front label at 10.",
                    zOrder,
                    "var overlay = new Overlay();\nOverlay.SetZIndex(status, 10);\noverlay.Children.Add(status);")),
            Doc.Section(
                "🧩",
                "Stable ties",
                "Equal z-index values preserve collection order so rendering remains deterministic.",
                Doc.Example(
                    "Two children at z=5",
                    "First remains before Second until their collection order or z-index changes.",
                    equalTies)),
            Doc.Section(
                "🧩",
                "Pointer transparency",
                "Decorative layers may render above interactive content without becoming pointer targets.",
                Doc.Example(
                    "Input passes through decoration",
                    "Click the visible overlap. Decorative overlay is not hit-test-visible, so the underlying Button receives activation.",
                    Doc.Column(transparent, pointerStatus),
                    "decoration.IsHitTestVisible = false;")),
            Doc.Section(
                "🧩",
                "Alignment and sizing",
                "Each child independently resolves length and alignment against the shared box.",
                Doc.Example(
                    "Five alignments",
                    "The labels occupy four corners and center without absolute coordinates.",
                    alignment),
                Doc.Example(
                    "Percentage card",
                    "The centered card resolves sixty percent width and half height whenever the host resizes.",
                    percent)),
            Doc.Section(
                "🧩",
                "Clipping",
                "ClipToBounds controls descendant drawing and hit testing while the Overlay itself remains safely clipped.",
                Doc.Example(
                    "Clipped and visible overflow",
                    "The same oversized card is cut by the first Overlay and allowed past the second Overlay's edge.",
                    Doc.Row(
                        Doc.Column(new Text("Clipped"), clipped),
                        Doc.Column(new Text("Unclipped"), unclipped)))),
            Doc.Section(
                "🧩",
                "Notification composition",
                "High visual priority does not rewrite collection-based focus traversal.",
                Doc.Example(
                    "Non-modal saved banner",
                    "The banner paints above editor content while ordinary ownership and focus order remain unchanged.",
                    notification)));
    }
}
