// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

using SharpVision.Tests.Styling;

/// <summary>Verifies ScrollBarStyle resolves Chrome/Fill from the theme's registrable "scrollBar"
/// style section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class ScrollBarStyleSectionTests
{
    /// <summary>Verifies an authored section's Chrome and Fill are applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsScrollBarSection_ResolvesChromeAndFill()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","scrollBar":{"chrome":"thin","fill":"line"}""");
        var theme = Themes.Parse(json);

        var style = ScrollBarStyle.Definition.Resolve(null, theme);

        style.Chrome.ShouldBe(ScrollBarChrome.Thin);
        style.Fill.ShouldBe(ScrollBarFill.Line);
    }

    /// <summary>Verifies a theme that authors no "scrollBar" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoScrollBarSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = ScrollBarStyle.Definition.Resolve(null, theme);

        style.Chrome.ShouldBe(ScrollBarStyle.Default.Chrome);
        style.Fill.ShouldBe(ScrollBarStyle.Default.Fill);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","scrollBar":{"chrome":"thin","fill":"line"}""");
        var theme = Themes.Parse(json);
        var local = ScrollBarStyle.FullBlock;

        var style = ScrollBarStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an unrecognized Chrome value reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenChromeValueIsUnrecognized_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"chrome":"bogus"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ScrollBarStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies an unrecognized Fill value reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenFillValueIsUnrecognized_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"fill":"bogus"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ScrollBarStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "scrollBar" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenScrollBarSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"chrome":"full"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }

    /// <summary>Verifies an authored glyphs section is applied to the resolved glyph family.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsGlyphsSection_ResolvesGlyphs()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"glyphs":{"verticalDecrement":"^","verticalIncrement":"v","horizontalDecrement":"<","horizontalIncrement":">","blockTrack":".","blockThumb":"#","horizontalLineTrack":"-","horizontalLineThumb":"=","verticalLineTrack":"|","verticalLineThumb":"#"}}""");
        var theme = Themes.Parse(json);

        var style = ScrollBarStyle.Definition.Resolve(null, theme);

        style.Glyphs.VerticalDecrement.ShouldBe(new Rune('^'));
        style.Glyphs.VerticalIncrement.ShouldBe(new Rune('v'));
        style.Glyphs.HorizontalDecrement.ShouldBe(new Rune('<'));
        style.Glyphs.HorizontalIncrement.ShouldBe(new Rune('>'));
        style.Glyphs.BlockTrack.ShouldBe(new Rune('.'));
        style.Glyphs.BlockThumb.ShouldBe(new Rune('#'));
        style.Glyphs.HorizontalLineTrack.ShouldBe(new Rune('-'));
        style.Glyphs.HorizontalLineThumb.ShouldBe(new Rune('='));
        style.Glyphs.VerticalLineTrack.ShouldBe(new Rune('|'));
        style.Glyphs.VerticalLineThumb.ShouldBe(new Rune('#'));
    }

    /// <summary>Verifies an unauthored member inside an authored glyphs section falls back to its
    /// own code default independently, rather than the whole family reverting.</summary>
    [Fact]
    public void Definition_WhenOneGlyphIsAuthored_LeavesOtherGlyphsAtDefaults()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"glyphs":{"blockThumb":"@"}}""");
        var theme = Themes.Parse(json);

        var style = ScrollBarStyle.Definition.Resolve(null, theme);

        style.Glyphs.BlockThumb.ShouldBe(new Rune('@'));
        style.Glyphs.VerticalDecrement.ShouldBe(ScrollBarGlyphs.Default.VerticalDecrement);
    }

    /// <summary>Verifies a theme that authors no glyphs section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoGlyphsSection_FallsBackToDefaultGlyphs()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = ScrollBarStyle.Definition.Resolve(null, theme);

        style.Glyphs.ShouldBe(ScrollBarGlyphs.Default);
    }

    /// <summary>Verifies a multi-Rune glyph entry reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenAGlyphHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"glyphs":{"blockThumb":"ab"}}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ScrollBarStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a wide glyph reports the same ArgumentException a hand-authored
    /// ScrollBarGlyphs would.</summary>
    [Fact]
    public void Definition_WhenAGlyphIsWide_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","scrollBar":{"glyphs":{"blockThumb":"界"}}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentException>(() => ScrollBarStyle.Definition.Resolve(null, theme));
    }
}
