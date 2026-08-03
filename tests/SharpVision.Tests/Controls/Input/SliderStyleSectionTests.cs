// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies SliderStyle resolves its three colors from the theme's registrable "slider"
/// style section (see #155) when one is authored, and falls back to code defaults otherwise.</summary>
public sealed class SliderStyleSectionTests
{
    /// <summary>Verifies an authored section's colors are applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsSliderSection_ResolvesColors()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","slider":{"fillColor":"#ff0000","trackColor":"#00ff00","thumbColor":"#0000ff"}""");
        var theme = Themes.Parse(json);

        var style = SliderStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0xff, 0, 0));
        theme.Resolve(style.TrackColor).ShouldBe(Color.Rgb(0, 0xff, 0));
        theme.Resolve(style.ThumbColor).ShouldBe(Color.Rgb(0, 0, 0xff));
    }

    /// <summary>Verifies an authored section supplying only one color leaves the others at their default.</summary>
    [Fact]
    public void Definition_WhenSectionSuppliesOnlyOneColor_PreservesTheOtherDefaults()
    {
        var json = ThemeJson.Create(extraStyles: ""","slider":{"fillColor":"#ff0000"}""");
        var theme = Themes.Parse(json);

        var style = SliderStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0xff, 0, 0));
        style.TrackColor.ShouldBe(SliderStyle.Default.TrackColor);
        style.ThumbColor.ShouldBe(SliderStyle.Default.ThumbColor);
    }

    /// <summary>Verifies a theme that authors no "slider" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoSliderSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = SliderStyle.Definition.Resolve(null, theme);

        style.FillColor.ShouldBe(SliderStyle.Default.FillColor);
        style.TrackColor.ShouldBe(SliderStyle.Default.TrackColor);
        style.ThumbColor.ShouldBe(SliderStyle.Default.ThumbColor);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","slider":{"fillColor":"#ff0000"}""");
        var theme = Themes.Parse(json);
        var local = SliderStyle.Default.With(fillColor: Color.Rgb(1, 2, 3));

        var style = SliderStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an authored color that references an unknown palette key throws
    /// a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenColorReferencesUnknownPaletteKey_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","slider":{"fillColor":"no-such-key"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => SliderStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies an authored color naming a palette key defined by the theme resolves correctly.</summary>
    [Fact]
    public void Definition_WhenColorReferencesPaletteKey_ResolvesPaletteColor()
    {
        var json = ThemeJson.Create(
            palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\"",
            extraStyles: ""","slider":{"fillColor":"brand"}""");
        var theme = Themes.Parse(json);

        var style = SliderStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies a plain unqualified "slider" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenSliderSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","slider":{"fillColor":"#ff0000"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
