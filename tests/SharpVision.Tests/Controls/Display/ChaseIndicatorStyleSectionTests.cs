// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using SharpVision.Tests.Styling;

/// <summary>Verifies ChaseIndicatorStyle resolves Active/Inactive from the theme's registrable
/// "chaseIndicator" style section (see #155) when one is authored, and falls back to code
/// defaults otherwise.</summary>
public sealed class ChaseIndicatorStyleSectionTests
{
    /// <summary>Verifies an authored section's glyphs are applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsChaseIndicatorSection_ResolvesGlyphs()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","chaseIndicator":{"active":"★","inactive":"☆"}""");
        var theme = Themes.Parse(json);

        var style = ChaseIndicatorStyle.Definition.Resolve(null, theme);

        style.Active.ShouldBe(new Rune('★'));
        style.Inactive.ShouldBe(new Rune('☆'));
    }

    /// <summary>Verifies a theme that authors no "chaseIndicator" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoChaseIndicatorSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = ChaseIndicatorStyle.Definition.Resolve(null, theme);

        style.Active.ShouldBe(ChaseIndicatorStyle.Default.Active);
        style.Inactive.ShouldBe(ChaseIndicatorStyle.Default.Inactive);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","chaseIndicator":{"active":"★","inactive":"☆"}""");
        var theme = Themes.Parse(json);
        var local = ChaseIndicatorStyle.Diamond;

        var style = ChaseIndicatorStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an empty glyph string reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenActiveGlyphIsEmpty_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","chaseIndicator":{"active":""}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ChaseIndicatorStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a multi-Rune glyph string reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenInactiveGlyphHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","chaseIndicator":{"inactive":"ab"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ChaseIndicatorStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a wide glyph string reports the same ArgumentException a hand-authored
    /// ChaseIndicatorStyle would.</summary>
    [Fact]
    public void Definition_WhenActiveGlyphIsWide_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","chaseIndicator":{"active":"界"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentException>(() => ChaseIndicatorStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "chaseIndicator" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenChaseIndicatorSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","chaseIndicator":{"active":"★"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
