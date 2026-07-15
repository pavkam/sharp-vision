// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies role resolution, fallbacks, and failure modes of the theme loader.</summary>
public sealed class ThemeLoaderTests
{
    private static string Json(string roles, string palette = "\"bg\": \"#101010\", \"fg\": \"#e0e0e0\"") =>
        $$"""
          { "version": 1, "name": "T", "slug": "t", "colorScheme": "dark", "order": 1,
            "author": "A", "license": "MIT", "source": "s",
            "palette": { {{palette}} }, "roles": { {{roles}} } }
          """;

    /// <summary>Verifies a palette key, an inline hex literal, and an inline index literal all resolve.</summary>
    [Fact]
    public void FromJson_WhenPaletteKeyAndInlineHexAndIndex_Resolves()
    {
        var theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\", \"accent\": \"#ff8800\", \"border\": \"idx:8\""),
            "t");

        theme.TryGetColor(ColorRole.Background, out var bg).ShouldBeTrue();
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
        theme.TryGetColor(ColorRole.Accent, out var accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        theme.TryGetColor(ColorRole.Border, out var border).ShouldBeTrue();
        border.ShouldBe(Color.Indexed(8));
    }

    /// <summary>Verifies every derived role falls back correctly when only background and foreground are given.</summary>
    [Fact]
    public void FromJson_WhenOnlyBackgroundAndForeground_FillsFallbacks()
    {
        var theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\""), "t");

        // accent -> foreground; surface -> background; border/muted -> foreground; selection -> accent(=fg)
        theme.TryGetColor(ColorRole.Accent, out var accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0xe0, 0xe0, 0xe0));
        theme.TryGetColor(ColorRole.Surface, out var surface).ShouldBeTrue();
        surface.ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
        theme.TryGetColor(ColorRole.Info, out var info).ShouldBeTrue();
        info.ShouldBe(accent);
    }

    /// <summary>Verifies that when both Border and Muted are absent, both fall back to Foreground.</summary>
    [Fact]
    public void FromJson_WhenBorderAndMutedAbsent_BothFallBackToForeground()
    {
        var theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\""), "t");

        theme.TryGetColor(ColorRole.Foreground, out var fg).ShouldBeTrue();
        theme.TryGetColor(ColorRole.Border, out var border).ShouldBeTrue();
        theme.TryGetColor(ColorRole.Muted, out var muted).ShouldBeTrue();
        border.ShouldBe(fg);
        muted.ShouldBe(fg);
    }

    /// <summary>Verifies that when Border is present but Muted is absent, Muted takes Border's value.</summary>
    [Fact]
    public void FromJson_WhenBorderPresentMutedAbsent_MutedTakesBorder()
    {
        var theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\", \"border\": \"#123456\""), "t");

        theme.TryGetColor(ColorRole.Muted, out var muted).ShouldBeTrue();
        muted.ShouldBe(Color.Rgb(0x12, 0x34, 0x56));
    }

    /// <summary>Verifies that when Muted is present but Border is absent, Border takes Muted's value.</summary>
    [Fact]
    public void FromJson_WhenMutedPresentBorderAbsent_BorderTakesMuted()
    {
        var theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\", \"muted\": \"#654321\""), "t");

        theme.TryGetColor(ColorRole.Border, out var border).ShouldBeTrue();
        border.ShouldBe(Color.Rgb(0x65, 0x43, 0x21));
    }

    /// <summary>Verifies every documented failure mode is reported as <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("\"foreground\": \"fg\"")]                                   // missing background
    [InlineData("\"background\": \"bg\"")]                                   // missing foreground
    [InlineData("\"background\": \"bg\", \"foreground\": \"missing\"")]       // unknown palette key
    [InlineData("\"background\": \"bg\", \"foreground\": \"#zz\"")]           // bad hex
    [InlineData("\"background\": \"bg\", \"foreground\": \"fg\", \"nope\": \"fg\"")] // unknown role
    [InlineData("\"background\": \"bg\", \"foreground\": \"fg\", \"accent\": \"idx:256\"")] // out-of-range index
    [InlineData("\"background\": \"bg\", \"foreground\": null")]             // null role value
    public void FromJson_WhenInvalid_Throws(string roles) =>
        Should.Throw<InvalidDataException>(() => ThemeLoader.FromJson(Json(roles), "t"));

    /// <summary>Verifies a null palette entry value is reported as <see cref="InvalidDataException"/> rather than a raw <see cref="NullReferenceException"/>.</summary>
    [Fact]
    public void FromJson_WhenPaletteValueIsNull_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeLoader.FromJson(
            Json(
                "\"background\": \"bg\", \"foreground\": \"fg\"",
                "\"bg\": null, \"fg\": \"#e0e0e0\""),
            "t"));

    /// <summary>Verifies malformed JSON reaching <see cref="ThemeLoader.FromJson"/> is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void FromJson_WhenMalformedJson_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeLoader.FromJson("{ not json", "t"));
}
