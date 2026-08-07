// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies retained named palette colors and resolved control colors.</summary>
public sealed class ThemePaletteTests
{
    /// <summary>Verifies loading retains named colors separately from resolved control colors.</summary>
    [Fact]
    public void FromJson_WhenPaletteIsNamed_RetainsPaletteAndSemanticMaps()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(
                palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\"",
                accent: "brand"),
            "test");

        theme.Palette["brand"].ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
        ThemeColorHelper.Accent(theme).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies the published maps cannot be changed through dictionary interfaces.</summary>
    [Fact]
    public void Theme_WhenMapsArePublished_AreReadOnly()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(),
            "test");

        _ = Should.Throw<NotSupportedException>(() =>
            ((IDictionary<string, Color>) theme.Palette).Add("extra", Color.Default));
    }
}
