// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using SharpVision.Tests.Styling;

/// <summary>Verifies ProgressBarStyle resolves its three colors from the theme's registrable
/// "progressBar" style section (see #155) when one is authored, and falls back to code defaults
/// otherwise.</summary>
public sealed class ProgressBarStyleSectionTests
{
    /// <summary>Verifies an authored section's colors are applied.</summary>
    [Fact]
    public void Definition_WhenThemeAuthorsProgressBarSection_ResolvesColors()
    {
        var json = ThemeJson.Create(
            extraStyles: ""","progressBar":{"fillColor":"#ff0000","trackColor":"#00ff00","indeterminateColor":"#0000ff"}""");
        var theme = Themes.Parse(json);

        var style = ProgressBarStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0xff, 0, 0));
        theme.Resolve(style.TrackColor).ShouldBe(Color.Rgb(0, 0xff, 0));
        theme.Resolve(style.IndeterminateColor).ShouldBe(Color.Rgb(0, 0, 0xff));
    }

    /// <summary>Verifies an authored section supplying only one color leaves the others at their default.</summary>
    [Fact]
    public void Definition_WhenSectionSuppliesOnlyOneColor_PreservesTheOtherDefaults()
    {
        var json = ThemeJson.Create(extraStyles: ""","progressBar":{"fillColor":"#ff0000"}""");
        var theme = Themes.Parse(json);

        var style = ProgressBarStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0xff, 0, 0));
        style.TrackColor.ShouldBe(ProgressBarStyle.Default.TrackColor);
        style.IndeterminateColor.ShouldBe(ProgressBarStyle.Default.IndeterminateColor);
    }

    /// <summary>Verifies a theme that authors no "progressBar" section falls back to code defaults.</summary>
    [Fact]
    public void Definition_WhenThemeHasNoProgressBarSection_FallsBackToDefaults()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var style = ProgressBarStyle.Definition.Resolve(null, theme);

        style.FillColor.ShouldBe(ProgressBarStyle.Default.FillColor);
        style.TrackColor.ShouldBe(ProgressBarStyle.Default.TrackColor);
        style.IndeterminateColor.ShouldBe(ProgressBarStyle.Default.IndeterminateColor);
    }

    /// <summary>Verifies a local complete style always wins over any authored section.</summary>
    [Fact]
    public void Definition_WhenLocalStyleIsSupplied_IgnoresSection()
    {
        var json = ThemeJson.Create(extraStyles: ""","progressBar":{"fillColor":"#ff0000"}""");
        var theme = Themes.Parse(json);
        var local = ProgressBarStyle.Default.With(fillColor: Color.Rgb(1, 2, 3));

        var style = ProgressBarStyle.Definition.Resolve(local, theme);

        style.ShouldBe(local);
    }

    /// <summary>Verifies an authored color that references an unknown palette key throws
    /// a source-labelled InvalidDataException.</summary>
    [Fact]
    public void Definition_WhenColorReferencesUnknownPaletteKey_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","progressBar":{"fillColor":"no-such-key"}""");
        var theme = Themes.Parse(json);

        _ = Should.Throw<InvalidDataException>(() => ProgressBarStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies an authored color naming a palette key defined by the theme resolves correctly.</summary>
    [Fact]
    public void Definition_WhenColorReferencesPaletteKey_ResolvesPaletteColor()
    {
        var json = ThemeJson.Create(
            palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\"",
            extraStyles: ""","progressBar":{"fillColor":"brand"}""");
        var theme = Themes.Parse(json);

        var style = ProgressBarStyle.Definition.Resolve(null, theme);

        theme.Resolve(style.FillColor).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies a plain unqualified "progressBar" section key parses without the
    /// third-party-namespacing rejection that applies to unregistered unqualified keys.</summary>
    [Fact]
    public void Parse_WhenProgressBarSectionIsPresent_DoesNotThrow()
    {
        var json = ThemeJson.Create(extraStyles: ""","progressBar":{"fillColor":"#ff0000"}""");

        _ = Should.NotThrow(() => Themes.Parse(json));
    }
}
