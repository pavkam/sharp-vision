// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies strict schema-two glyph parsing and diagnostics.</summary>
public sealed class ThemeGlyphLoaderTests
{
    /// <summary>Verifies a complete glyph document publishes typed values.</summary>
    [Fact]
    public void Parse_WhenGlyphsAreComplete_PublishesTypedPalette()
    {
        var theme = ThemeFile.Parse(ThemeJson.Create(
            "\"background\":\"bg\",\"foreground\":\"fg\""));

        theme.SchemaVersion.ShouldBe(2);
        theme.Glyphs.Progress.HorizontalFractions.Length.ShouldBe(9);
        theme.Glyphs.Separators.TableCross.Value.ShouldBe(new Rune('┼'));
    }

    /// <summary>Verifies a missing glyph object is rejected without a compatibility fallback.</summary>
    [Fact]
    public void Parse_WhenGlyphsAreMissing_Throws()
    {
        const string json = /*lang=json,strict*/ """
            { "version": 2, "roles": { "background": "#000000", "foreground": "#ffffff" } }
            """;

        Should.Throw<InvalidDataException>(() => ThemeFile.Parse(json))
            .Message.ShouldContain("required property 'glyphs'");
    }

    /// <summary>Verifies an invalid scalar diagnostic names the exact glyph member.</summary>
    [Fact]
    public void Parse_WhenGlyphIsWide_NamesExactPath()
    {
        var glyphs = ThemeJson.Glyphs.Replace(
            "\"collapsed\":{\"value\":\"▶\"",
            "\"collapsed\":{\"value\":\"界\"",
            StringComparison.Ordinal);
        var json = $$"""
            { "version": 2,
              "roles": { "background": "#000000", "foreground": "#ffffff" },
              "glyphs": {{glyphs}} }
            """;

        Should.Throw<InvalidDataException>(() => ThemeFile.Parse(json))
            .Message.ShouldContain("glyphs.disclosure.collapsed.value");
    }
}
