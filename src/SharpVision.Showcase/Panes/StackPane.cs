// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Stack control with orientation, spacing, and reverse-order specimens.</summary>
internal sealed class StackPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Stack";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Stack horizontal = new() { Orientation = Orientation.Horizontal, Spacing = 2, Width = Length.Cells(40) };
        var fixedCard = Card("Fixed 10", Glyphs.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = Card("35%", Glyphs.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = Card("1*", Glyphs.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);

        Stack reversed = new() { Orientation = Orientation.Horizontal, Spacing = 2, Reverse = true };
        reversed.Children.Add(Card("First", Glyphs.Light));
        reversed.Children.Add(Card("Second", Glyphs.Heavy));
        reversed.Children.Add(Card("Third", Glyphs.Paired));

        Stack vertical = new() { Spacing = 1 };
        vertical.Children.Add(Card("Top", Glyphs.Rounded));
        vertical.Children.Add(Card("Spacing = 1", Glyphs.Light));
        vertical.Children.Add(Card("Bottom", Glyphs.Heavy));

        return Doc.Page(
            Title,
            "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.",
            Doc.Example(
                "Mixed horizontal tracks",
                "Fixed cells, percentage sizing, and proportional remainder can coexist in one horizontal Stack.",
                horizontal),
            Doc.Example(
                "Reverse order",
                "Reverse changes visual and keyboard-navigation order without changing the source child collection.",
                reversed),
            Doc.Example(
                "Vertical spacing",
                "Vertical is the default orientation; explicit spacing is applied only between participating children.",
                vertical));
    }

    private static Dock Card(string text, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { new Text(text) },
    };
}
