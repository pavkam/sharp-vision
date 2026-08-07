// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies semantic values, profile inheritance, and failure modes of the theme loader.</summary>
public sealed class ThemeCatalogParsingTests
{
    /// <summary>Verifies palette references and inline RGB literals both resolve.</summary>
    [Fact]
    public void Parse_WhenColorsUsePaletteAndHex_ResolvesConcreteValues()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(background: "bg", foreground: "fg", accent: "#ff8800"), "t");

        theme.ResolveColor(SemanticColor.Control).ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
        theme.ResolveColor(SemanticColor.Accent).ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
    }

    /// <summary>Verifies focused semantic members overlay normal members independently.</summary>
    [Fact]
    public void Parse_WhenFocusedValuesAreDefined_OverlaysNormalAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff8800"), "t");

        var normal = theme.Control.Resolve(VisualState.Normal);
        var focused = theme.Control.Resolve(VisualState.Focused);

        theme.Resolve(focused.Face.Foreground).ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        theme.Resolve(focused.Border.Foreground).ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.Border.Background.ShouldBe(normal.Border.Background);
    }

    /// <summary>Verifies role profiles inherit ControlBase appearance before applying role chrome.</summary>
    [Fact]
    public void Parse_WhenSectionNormalIsPartial_InheritsControlAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(), "t");

        theme.Window.Normal.Face.ShouldBe(theme.Control.Normal.Face);
        theme.Window.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Window.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
    }

    /// <summary>Verifies a cross-cutting meaning resolves from the colors section.</summary>
    [Fact]
    public void Parse_WhenCrossCuttingColorIsExplicit_ResolvesItFromTheColorsSection()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#fedcba"), "t");

        theme.Hotkey.ShouldBe(Color.Rgb(0xfe, 0xdc, 0xba));
    }

    /// <summary>The regression this test exists to pin. Error, Warning, Success, Info, Muted, and
    /// Hotkey used to be a second color table filled from a parallel "status" theme section, which
    /// all 15 bundled themes authored as a byte-identical duplicate of the same six colors.* keys.
    /// That section was deleted when SemanticColor subsumed the retired StatusColor enum, so these
    /// six are now named shortcuts onto the one table. A reintroduced second table would let them
    /// diverge from ResolveColor silently.</summary>
    [Fact]
    public void CrossCuttingColors_WhenResolved_ReadTheOneColorsTable()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(), "t");

        theme.Error.ShouldBe(theme.ResolveColor(SemanticColor.Error));
        theme.Warning.ShouldBe(theme.ResolveColor(SemanticColor.Warning));
        theme.Success.ShouldBe(theme.ResolveColor(SemanticColor.Success));
        theme.Info.ShouldBe(theme.ResolveColor(SemanticColor.Info));
        theme.Muted.ShouldBe(theme.ResolveColor(SemanticColor.Muted));
        theme.Hotkey.ShouldBe(theme.ResolveColor(SemanticColor.Hotkey));
    }

    /// <summary>Verifies unknown palette references are rejected.</summary>
    [Fact]
    public void Parse_WhenColorReferenceIsUnknown_Throws()
    {
        var json = ThemeJson.Create(background: "missing");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies malformed RGB text is rejected.</summary>
    [Fact]
    public void Parse_WhenColorLiteralIsMalformed_Throws()
    {
        var json = ThemeJson.Create(accent: "#zz");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies a null palette value is reported as theme data failure.</summary>
    [Fact]
    public void Parse_WhenPaletteValueIsNull_Throws()
    {
        var json = ThemeJson.Create(palette: "\"bg\":null,\"fg\":\"#e0e0e0\"");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies malformed JSON is reported as theme data failure.</summary>
    [Fact]
    public void Parse_WhenJsonIsMalformed_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse("{ not json", "t"));
}
