// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies Theme.ResolveSectionColorValue resolves a registrable section's color string
/// the same way Themes.cs resolves a profile color, but through the public Palette instead of the
/// private parse-time dictionary (see #155).</summary>
public sealed class ThemeSectionColorResolutionTests
{
    /// <summary>Verifies "transparent" resolves to Color.Transparent.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsTransparent_ResolvesTransparent()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var value = theme.ResolveSectionColorValue("transparent", "context");

        theme.Resolve(value).ShouldBe(Color.Transparent);
    }

    /// <summary>Verifies "default" resolves to Color.Default.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsDefault_ResolvesDefault()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var value = theme.ResolveSectionColorValue("default", "context");

        theme.Resolve(value).ShouldBe(Color.Default);
    }

    /// <summary>Verifies a ThemeColor name resolves through the theme's semantic color.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsThemeColorName_ResolvesSemanticColor()
    {
        var theme = Themes.Parse(ThemeJson.Create(accent: "#77aaff"));

        var value = theme.ResolveSectionColorValue("accent", "context");

        theme.Resolve(value).ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    /// <summary>Verifies a hex literal resolves directly.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsHexLiteral_ResolvesLiteral()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        var value = theme.ResolveSectionColorValue("#123456", "context");

        theme.Resolve(value).ShouldBe(Color.Rgb(0x12, 0x34, 0x56));
    }

    /// <summary>Verifies an invalid hex literal throws InvalidDataException.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenHexLiteralIsInvalid_Throws()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        _ = Should.Throw<InvalidDataException>(() => theme.ResolveSectionColorValue("#zzz", "context"));
    }

    /// <summary>Verifies a named palette key resolves through the theme's own retained palette.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsPaletteKey_ResolvesPaletteColor()
    {
        var theme = Themes.Parse(
            ThemeJson.Create(palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\""));

        var value = theme.ResolveSectionColorValue("brand", "context");

        theme.Resolve(value).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies an unknown palette key throws InvalidDataException.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsUnknownPaletteKey_Throws()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        _ = Should.Throw<InvalidDataException>(() => theme.ResolveSectionColorValue("no-such-key", "context"));
    }

    /// <summary>Verifies a null value throws ArgumentNullException.</summary>
    [Fact]
    public void ResolveSectionColorValue_WhenValueIsNull_Throws()
    {
        var theme = Themes.Parse(ThemeJson.Create());

        _ = Should.Throw<ArgumentNullException>(() => theme.ResolveSectionColorValue(null!, "context"));
    }
}
