// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates intrinsic border edges, glyph families, and appearance options.</summary>
internal sealed class BorderPane: CompositeControlBase
{
    /// <summary>Initializes the retained Border concept page.</summary>
    internal BorderPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Border";

    private static DocPage CreateContent()
    {
        var custom = new BorderGlyphStyle(
            new Rune('╓'),
            new Rune('─'),
            new Rune('╖'),
            new Rune('║'),
            new Rune('╜'),
            new Rune('─'),
            new Rune('╙'),
            new Rune('║'));
        var families = new DocColumn(
            CreateFamilySample("Light", BorderGlyphStyle.Light),
            CreateFamilySample("Rounded", BorderGlyphStyle.Rounded),
            CreateFamilySample("Heavy", BorderGlyphStyle.Heavy),
            CreateFamilySample("Paired", BorderGlyphStyle.Paired),
            CreateFamilySample("ASCII", BorderGlyphStyle.Ascii),
            CreateFamilySample("Solid", BorderGlyphStyle.Solid),
            CreateFamilySample("Half block", BorderGlyphStyle.HalfBlock),
            CreateFamilySample("Light shade", BorderGlyphStyle.LightShade),
            CreateFamilySample("Medium shade", BorderGlyphStyle.MediumShade),
            CreateFamilySample("Dark shade", BorderGlyphStyle.DarkShade),
            CreateFamilySample("Custom mixed family", custom));

        var edges = new DocColumn(
            CreateEdgeSample("Left", BorderSide.Left),
            CreateEdgeSample("Top", BorderSide.Top),
            CreateEdgeSample("Right", BorderSide.Right),
            CreateEdgeSample("Bottom", BorderSide.Bottom),
            CreateEdgeSample("Left + right", BorderSide.Left | BorderSide.Right),
            CreateEdgeSample("Top + bottom", BorderSide.Top | BorderSide.Bottom),
            CreateEdgeSample("All four", BorderSide.All));

        var appearance = new DocColumn(
            new Dock
            {
                Width = Length.Cells(38),
                Height = Length.Cells(4),
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Heavy,
                    Color.Rgb(0x7A, 0xA2, 0xF7),
                    Color.Transparent,
                    TerminalAttributes.Bold),
                Padding = new Thickness(1, 0),
                Children = { new Text("Accent border with bold attributes") }
            },
            new Dock
            {
                Width = Length.Cells(38),
                Height = Length.Cells(4),
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Rounded,
                    Color.Rgb(205, 90, 120),
                    Color.Transparent,
                    SemanticDecoration.Border),
                Padding = new Thickness(1, 0),
                Children = { new Text("Concrete RGB border override") }
            });
        var relief = new DocColumn(
            CreateReliefSample(
                "Raised: light top/left",
                BorderRelief.Raised),
            CreateReliefSample(
                "Sunken: dark top/left",
                BorderRelief.Sunken));

        return new DocPage(
            Title,
            "<info>Border</info>, <info>BorderGlyphStyle</info>, and border appearance belong directly to every Control; there is no Border wrapper component.",
            new DocSection(
                "🖼️",
                "Glyph families",
                "Every code-owned frame preset is shown below, followed by a caller-authored eight-glyph family.",
                new DocExample(
                    "All built-in and custom families",
                    "ThemeCatalog change colors, never glyph geometry. ASCII remains the portable fallback; block and shade families are deliberately decorative.",
                    families,
                    "control.Border = new Border(BorderSide.All, BorderGlyphStyle.Rounded, SemanticColor.ControlBorder, Color.Transparent, SemanticDecoration.Border);")),
            new DocSection(
                "📏",
                "Independent edges",
                "Each physical edge is either zero or one terminal cell. Corners appear only where their adjacent edges meet.",
                new DocExample(
                    "Physical edge combinations",
                    "Left, top, right, bottom, opposing pairs, and the complete frame use the same intrinsic property.",
                    edges,
                    "control.Border = new Border(BorderSide.Left | BorderSide.Bottom, BorderGlyphStyle.Light, SemanticColor.ControlBorder, Color.Transparent, SemanticDecoration.Border);")),
            new DocSection(
                "🎨",
                "Border appearance",
                "Border groups sides, glyph style, foreground, background, and terminal attributes into one complete value.",
                new DocExample(
                    "Semantic and concrete overrides",
                    "The first frame follows the active Accent semantic; the second keeps an explicit RGB value across theme changes.",
                    appearance)),
            new DocSection(
                "◩",
                "Raised and sunken edges",
                "Application-authored BorderRelief preserves semantic depth while the active theme owns the highlight and shade colors.",
                new DocExample(
                    "Explicit container relief",
                    "Raised highlights top and left; Sunken reverses the active theme's highlight and shade pair. Turbo Vision is the only bundled theme that applies relief automatically, and only to containers.",
                    relief,
                    "control.Border = new Border(BorderSide.All, BorderGlyphStyle.Light, SemanticColor.ControlBorder, BorderRelief.Sunken, Color.Transparent, SemanticDecoration.Border);")));
    }

    private static Dock CreateFamilySample(string caption, BorderGlyphStyle glyphs) => new()
    {
        Width = Length.Cells(32),
        Height = Length.Cells(3),
        Border = new Border(
            BorderSide.All,
            glyphs,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Dock CreateEdgeSample(string caption, BorderSide border) => new()
    {
        Width = Length.Cells(32),
        Height = Length.Cells(3),
        Border = new Border(
            border,
            BorderGlyphStyle.Light,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Dock CreateReliefSample(string caption, BorderRelief relief) => new()
    {
        Width = Length.Cells(38),
        Height = Length.Cells(4),
        Face = new Face(
            Color.Rgb(0x00, 0x00, 0x00),
            Color.Rgb(0xaa, 0xaa, 0xaa),
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            SemanticColor.ControlBorder,
            relief,
            Color.Rgb(0xaa, 0xaa, 0xaa),
            TerminalAttributes.None),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };
}
