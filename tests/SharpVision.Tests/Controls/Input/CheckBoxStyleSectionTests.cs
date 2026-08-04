// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies CheckBoxStyle resolves MarkStyle from the theme's registrable "checkBox"
/// style section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class CheckBoxStyleSectionTests
{
    /// <summary>Verifies an authored section's MarkStyle is applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsCheckBoxSection_ResolvesMarkStyle()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"tick"}""");
        var theme = Themes.Parse(json);

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
    }

    /// <summary>Verifies a theme that authors no "checkBox" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoCheckBoxSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.MarkStyle.ShouldBe(CheckBoxStyle.Default.MarkStyle);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"tick"}""");
        var theme = Themes.Parse(json);
        var local = CheckBoxStyle.Square;

        var style = CheckBoxStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an unrecognized MarkStyle value reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenMarkStyleValueIsUnrecognized_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"bogus"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => CheckBoxStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "checkBox" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenCheckBoxSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"markStyle":"square"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }

    /// <summary>Verifies an authored glyphs section is applied to the resolved glyph family.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsGlyphsSection_ResolvesGlyphs()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"glyphs":{"unchecked":"[","checked":"X","indeterminate":"~"}}""");
        var theme = Themes.Parse(json);

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.Glyphs.Unchecked.ShouldBe(new Rune('['));
        style.Glyphs.Checked.ShouldBe(new Rune('X'));
        style.Glyphs.Indeterminate.ShouldBe(new Rune('~'));
    }

    /// <summary>Verifies an unauthored member inside an authored glyphs section falls back to its
    /// own code default independently, rather than the whole family reverting.</summary>
    [Fact]
    public void Definition_WhenOneGlyphIsAuthored_LeavesOtherGlyphsAtDefaults()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"glyphs":{"checked":"X"}}""");
        var theme = Themes.Parse(json);

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.Glyphs.Checked.ShouldBe(new Rune('X'));
        style.Glyphs.Unchecked.ShouldBe(CheckBoxStyle.Default.Glyphs.Unchecked);
    }

    /// <summary>Verifies a theme that authors no glyphs section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoGlyphsSection_FallsBackToDefaultGlyphs()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = CheckBoxStyle.Definition.Resolve(null, theme);

        style.Glyphs.ShouldBe(CheckBoxStyle.Default.Glyphs);
    }

    /// <summary>Verifies a multi-Rune glyph entry reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenAGlyphHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"glyphs":{"checked":"ab"}}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => CheckBoxStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a wide glyph reports the same ArgumentException a hand-authored
    /// CheckBoxGlyphs would.</summary>
    [Fact]
    public void Definition_WhenAGlyphIsWide_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","checkBox":{"glyphs":{"checked":"界"}}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentException>(() => CheckBoxStyle.Definition.Resolve(null, theme));
    }
}
