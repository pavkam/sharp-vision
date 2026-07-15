// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Dock control with side-attached and fill layout specimens.</summary>
internal sealed class DockPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Dock";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Dock allSides = new()
        {
            Width = Length.Cells(38),
            Height = Length.Cells(9),
            LastChildFills = true,
            Spacing = 1,
        };
        var left = Card("Left", Glyphs.Light);
        left.Width = Length.Cells(7);
        Dock.SetSide(left, Side.Left);
        allSides.Children.Add(left);
        var top = Card("Top", Glyphs.Heavy);
        top.Height = Length.Cells(2);
        Dock.SetSide(top, Side.Top);
        allSides.Children.Add(top);
        var right = Card("Right", Glyphs.Paired);
        right.Width = Length.Cells(8);
        Dock.SetSide(right, Side.Right);
        allSides.Children.Add(right);
        var bottom = Card("Bottom", Glyphs.Ascii);
        bottom.Height = Length.Cells(2);
        Dock.SetSide(bottom, Side.Bottom);
        allSides.Children.Add(bottom);
        allSides.Children.Add(Card("Fill", Glyphs.Rounded));

        Dock order = new()
        {
            Width = Length.Cells(30),
            Height = Length.Cells(7),
            LastChildFills = true,
        };
        var first = Card("1: Left", Glyphs.Light);
        first.Width = Length.Cells(9);
        Dock.SetSide(first, Side.Left);
        order.Children.Add(first);
        var second = Card("2: Left", Glyphs.Light);
        second.Width = Length.Cells(9);
        Dock.SetSide(second, Side.Left);
        order.Children.Add(second);
        order.Children.Add(Card("3: Fill", Glyphs.Rounded));

        Dock noFill = new()
        {
            Width = Length.Cells(30),
            Height = Length.Cells(4),
            LastChildFills = false,
        };
        var onlyChild = Card("No remainder claimed", Glyphs.Heavy);
        onlyChild.Width = Length.Cells(20);
        Dock.SetSide(onlyChild, Side.Top);
        noFill.Children.Add(onlyChild);

        return Doc.Page(
            Title,
            "Consumes remaining physical edges in child order and optionally gives the final child all remaining space.",
            Doc.Example(
                "Four sides and a fill",
                "Each child attaches a Side via the Dock.SetSide attached property. Children consume the remaining rectangle in insertion order, and LastChildFills lets the final child claim whatever space is left.",
                allSides),
            Doc.Example(
                "Order matters",
                "Two children both attached to Side.Left stack left-to-right in insertion order: the first claims the outermost strip, the second claims what remains after it, and the fill takes the rest.",
                order),
            Doc.Example(
                "LastChildFills disabled",
                "With LastChildFills set to false, the final child docks to its own side like any other and the remaining rectangle is left empty rather than claimed.",
                noFill));
    }

    private static Dock Card(string label, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { new Text(label) },
    };
}
