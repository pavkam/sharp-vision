// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies unmapped styles.* keys are either retained (namespaced third-party sections)
/// or rejected (an unqualified unknown key, very likely a typo of a well-known role name), and the
/// shared leaf-parsing helpers every control style's JSON section resolves through.</summary>
public sealed class ThemeStyleSectionTests
{
    /// <summary>Verifies an unqualified unknown styles key is rejected instead of silently
    /// retained, since it is very likely a typo of one of the six well-known role names.</summary>
    [Fact]
    public void Parse_WhenStylesKeyIsUnqualifiedAndUnknown_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","buton":{}""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));
    }

    /// <summary>Verifies ParseSectionGlyph's null passthrough, single-Rune success, and the
    /// multi-Rune/empty rejection message shape - the shared helper six controls' own ParseGlyph
    /// used to hand-copy verbatim now route through.</summary>
    [Fact]
    public void ParseSectionGlyph_WhenValueIsNullSingleOrMultiRune_MatchesTheDocumentedContract()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.ParseSectionGlyph(null, "styles.checkBox.glyphs.unchecked").ShouldBeNull();
        theme.ParseSectionGlyph("x", "styles.checkBox.glyphs.unchecked").ShouldBe(new Rune('x'));

        var exception = Should.Throw<InvalidDataException>(
            () => theme.ParseSectionGlyph("xy", "styles.checkBox.glyphs.unchecked"));
        exception.Message.ShouldBe(
            $"Theme '{theme.Slug}' styles.checkBox.glyphs.unchecked must contain one Rune.");
    }

    /// <summary>Verifies ParseSectionEnum's null passthrough, case-insensitive success, and the
    /// unknown-value rejection message shape - the shared helper four controls' own
    /// ParseMarkStyle/ParseChrome/ParseFill used to hand-copy verbatim now route through.</summary>
    [Fact]
    public void ParseSectionEnum_WhenValueIsNullKnownOrUnknown_MatchesTheDocumentedContract()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.ParseSectionEnum<CheckBoxMarkStyle>(null, "styles.checkBox.markStyle").ShouldBeNull();
        theme.ParseSectionEnum<CheckBoxMarkStyle>("BRACKETS", "styles.checkBox.markStyle")
            .ShouldBe(CheckBoxMarkStyle.Brackets);

        var exception = Should.Throw<InvalidDataException>(
            () => theme.ParseSectionEnum<CheckBoxMarkStyle>("bogus", "styles.checkBox.markStyle"));
        exception.Message.ShouldBe(
            $"Theme '{theme.Slug}' styles.checkBox.markStyle has unknown value 'bogus'.");
    }
}
