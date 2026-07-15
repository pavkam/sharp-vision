// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Border control with framed glyph-family and color specimens.</summary>
internal sealed class BorderPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Border";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var light = Frame("Light", Glyphs.Light);
        var heavy = Frame("Heavy", Glyphs.Heavy);
        var paired = Frame("Paired", Glyphs.Paired);
        var rounded = Frame("Rounded", Glyphs.Rounded);
        var ascii = Frame("ASCII fallback", Glyphs.Ascii);
        var solid = Frame("Solid block", Glyphs.Solid);
        var lightShade = Frame("Light shade", Glyphs.LightShade);
        var mediumShade = Frame("Medium shade", Glyphs.MediumShade);
        var darkShade = Frame("Dark shade", Glyphs.DarkShade);

        var partial = new Border()
        {
            Child = new Text("Top and left edges only"),
            BorderThickness = new Thickness(1, 1, 0, 0),
            Glyphs = Glyphs.Heavy,
            Padding = new Thickness(1, 0),
        };

        var tintedChild = new Border()
        {
            Child = new Text("Owned child"),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
        };
        var tinted = new Border()
        {
            Child = tintedChild,
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            Padding = new Thickness(1),
        };

        return Doc.Page(
            Title,
            "Frames one owned child with independently enabled physical edges and terminal-safe glyph sets.",
            Doc.Example(
                "Glyph families",
                "Every family draws the same four edges with different Unicode or ASCII-fallback runes, so a Border always renders correctly regardless of terminal font support.",
                Doc.Row(light, heavy, paired, rounded)),
            Doc.Example(
                "Shaded and solid fills",
                "Shade and solid glyph families trade crisp corners for a denser, more opaque frame — useful for emphasis without changing layout.",
                Doc.Row(ascii, solid, lightShade, mediumShade, darkShade)),
            Doc.Example(
                "Partial edges",
                "BorderThickness enables each physical edge independently: zero suppresses an edge entirely, so a Border can frame only the sides that matter.",
                partial),
            Doc.Example(
                "Nested framing",
                "Borders own exactly one child, so nesting one Border inside another composes independent frames — an outer margin card around an inner content card.",
                tinted));
    }

    private static Border Frame(string label, Glyphs glyphs) => new()
    {
        Child = new Text(label),
        BorderThickness = new Thickness(1),
        Glyphs = glyphs,
        Padding = new Thickness(1, 0),
    };
}
