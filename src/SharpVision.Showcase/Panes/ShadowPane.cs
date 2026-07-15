// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the Shadow control with composite darkening and block-glyph overflow specimens.</summary>
internal sealed class ShadowPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Shadow";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var composite = new Shadow()
        {
            Child = Card("Composite", Glyphs.Rounded),
            Offset = new Point(2, 1),
        };

        var blockGlyph = new Shadow()
        {
            Child = Card("Block glyph", Glyphs.Paired),
            Mode = ShadowMode.BlockGlyph,
            Glyph = new Rune('░'),
            Offset = new Point(2, 1),
        };

        var deepOffset = new Shadow()
        {
            Child = Card("Offset 4,2", Glyphs.Light),
            Offset = new Point(4, 2),
        };

        return Doc.Page(
            Title,
            "Decorates one owned child with a Turbo Vision-style composite darkening or an explicit block-glyph overflow silhouette.",
            Doc.Example(
                "Composite stage",
                "The default Composite mode darkens existing cells behind the shadow's footprint instead of overwriting them, so whatever was already rendered there keeps showing through, dimmed.",
                Stage(composite)),
            Doc.Example(
                "Block glyph stage",
                "BlockGlyph mode draws an explicit printable Rune, such as the classic ░ shade glyph, into every exposed shadow cell instead of darkening the existing rendition.",
                Stage(blockGlyph)),
            Doc.Example(
                "Custom offset",
                "Offset moves the shadow by signed horizontal and vertical terminal-cell counts independently of the child's own position, so a deeper offset reads as a taller, more pronounced drop.",
                Stage(deepOffset)));
    }

    private static Border Card(string content, Glyphs glyphs) => new()
    {
        Child = new Text(content),
        BorderThickness = new Thickness(1),
        Glyphs = glyphs,
        Padding = new Thickness(1, 0),
    };

    private static Border Stage(Shadow shadow)
    {
        var canvas = new Canvas()
        {
            Width = Length.Cells(28),
            Height = Length.Cells(5),
            ClipToBounds = true,
        };
        Canvas.SetLeft(shadow, Length.Cells(2));
        Canvas.SetTop(shadow, Length.Cells(1));
        canvas.Children.Add(shadow);

        return new Border
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            Child = canvas,
        };
    }
}
