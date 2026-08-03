// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using SharpVision.Tests.Styling;

/// <summary>Verifies SpinnerStyle resolves Frames from the theme's registrable "spinner" style
/// section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class SpinnerStyleSectionTests
{
    /// <summary>Verifies an authored section's frames are applied in order.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsSpinnerSection_ResolvesFrames()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":["|","/","-","\\"]}""");
        var theme = Themes.Parse(json);

        var style = SpinnerStyle.Definition.Resolve(null, theme);

        style.Frames.ShouldBe([new Rune('|'), new Rune('/'), new Rune('-'), new Rune('\\')]);
    }

    /// <summary>Verifies a theme that authors no "spinner" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoSpinnerSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = SpinnerStyle.Definition.Resolve(null, theme);

        style.Frames.ShouldBe(SpinnerStyle.Default.Frames);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":["|","/","-","\\"]}""");
        var theme = Themes.Parse(json);
        var local = SpinnerStyle.Ascii;

        var style = SpinnerStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an empty frames array reports the same ArgumentException a hand-authored
    /// empty sequence would.</summary>
    [Fact]
    public void Definition_WhenFramesArrayIsEmpty_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":[]}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentException>(() => SpinnerStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a multi-Rune frame entry reports a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenAFrameHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":["ab"]}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => SpinnerStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a wide frame glyph reports the same ArgumentException a hand-authored
    /// SpinnerStyle would.</summary>
    [Fact]
    public void Definition_WhenAFrameIsWide_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":["界"]}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<ArgumentException>(() => SpinnerStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies a plain unqualified "spinner" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenSpinnerSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","spinner":{"frames":["|"]}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
