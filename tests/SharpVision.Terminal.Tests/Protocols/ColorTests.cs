// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies <see cref="Color"/>: hex parsing of terminal RGB colors and named color resolution.
/// </summary>
public sealed class ColorTests
{
    #region Hex parsing

    /// <summary>Verifies the zero-initialized color retains terminal-default semantics.</summary>
    [Fact]
    public void Default_WhenZeroInitialized_IsTerminalDefault()
    {
        var color = default(Color);

        color.ShouldBe(Color.Default);
        color.IsDefault.ShouldBeTrue();
        color.IsRgb.ShouldBeFalse();
    }

    /// <summary>Verifies packed RGB black remains distinct from the zero-initialized terminal default.</summary>
    [Fact]
    public void Rgb_WhenBlack_DoesNotCollideWithDefault()
    {
        var black = Color.Rgb(0, 0, 0);

        black.ShouldNotBe(Color.Default);
        black.IsRgb.ShouldBeTrue();
        black.Red.ShouldBe((byte) 0);
        black.Green.ShouldBe((byte) 0);
        black.Blue.ShouldBe((byte) 0);
    }

    /// <summary>Verifies a concrete RGB color preserves every channel.</summary>
    [Fact]
    public void Color_WhenConcrete_PreservesValue()
    {
        var color = Color.Rgb(10, 20, 30);

        color.IsRgb.ShouldBeTrue();
        color.Red.ShouldBe((byte) 10);
        color.Green.ShouldBe((byte) 20);
        color.Blue.ShouldBe((byte) 30);
    }

    /// <summary>Verifies callers use behavioral color queries instead of a representation enum.</summary>
    [Fact]
    public void PublicSurface_WhenInspected_DoesNotExposeKindProperty() =>
        typeof(Color).GetProperty("Kind").ShouldBeNull();

    /// <summary>Verifies that a six-digit hex string parses into the exact RGB components.</summary>
    [Fact]
    public void FromHex_WhenSixDigits_ParsesRgb()
    {
        var color = Color.FromHex("#1a2b3c");

        color.IsRgb.ShouldBeTrue();
        color.Red.ShouldBe((byte) 0x1a);
        color.Green.ShouldBe((byte) 0x2b);
        color.Blue.ShouldBe((byte) 0x3c);
    }

    /// <summary>Verifies that a three-digit hex string expands each nibble to a full byte.</summary>
    [Fact]
    public void FromHex_WhenThreeDigits_ExpandsNibbles()
    {
        var color = Color.FromHex("f80");

        color.ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
    }

    /// <summary>Verifies that mixed-case digits with a leading hash parse correctly.</summary>
    [Fact]
    public void FromHex_WhenMixedCaseWithHash_ParsesRgb() =>
        Color.FromHex("#AbCdEf").ShouldBe(Color.Rgb(0xab, 0xcd, 0xef));

    /// <summary>Verifies that null, empty, wrong-length, non-hex, and alpha-bearing strings all throw.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#gg0000")]
    [InlineData("#12345678")] // no alpha
    public void FromHex_WhenMalformed_Throws(string? value)
    {
        if (value is null)
        {
            _ = Should.Throw<ArgumentNullException>(() => Color.FromHex(value!));
        }
        else
        {
            _ = Should.Throw<FormatException>(() => Color.FromHex(value));
        }
    }

    /// <summary>Verifies that <see cref="Color.TryFromHex"/> returns false and the default color for malformed input.</summary>
    [Fact]
    public void TryFromHex_WhenMalformed_ReturnsFalseAndDefault()
    {
        Color.TryFromHex("#nope", out var color).ShouldBeFalse();
        color.ShouldBe(Color.Default);
    }

    /// <summary>Verifies that a six-character string with embedded, leading, or trailing whitespace is rejected.</summary>
    [Theory]
    [InlineData("1 2345")]
    [InlineData(" 12345")]
    [InlineData("12345 ")]
    public void FromHex_WhenWhitespaceInDigits_Throws(string value)
    {
        _ = Should.Throw<FormatException>(() => Color.FromHex(value));

        Color.TryFromHex(value, out var color).ShouldBeFalse();
        color.ShouldBe(Color.Default);
    }

    #endregion

    #region Named colors

    /// <summary>Verifies named ANSI and status aliases resolve to reference RGB values.</summary>
    [Theory]
    [InlineData("black", 0x00, 0x00, 0x00)]
    [InlineData("red", 0xcd, 0x00, 0x00)]
    [InlineData("green", 0x00, 0xcd, 0x00)]
    [InlineData("yellow", 0xcd, 0xcd, 0x00)]
    [InlineData("blue", 0x00, 0x00, 0xee)]
    [InlineData("magenta", 0xcd, 0x00, 0xcd)]
    [InlineData("cyan", 0x00, 0xcd, 0xcd)]
    [InlineData("white", 0xe5, 0xe5, 0xe5)]
    [InlineData("brightblack", 0x7f, 0x7f, 0x7f)]
    [InlineData("gray", 0x7f, 0x7f, 0x7f)]
    [InlineData("grey", 0x7f, 0x7f, 0x7f)]
    [InlineData("brightred", 0xff, 0x00, 0x00)]
    [InlineData("brightgreen", 0x00, 0xff, 0x00)]
    [InlineData("brightyellow", 0xff, 0xff, 0x00)]
    [InlineData("brightblue", 0x5c, 0x5c, 0xff)]
    [InlineData("brightmagenta", 0xff, 0x00, 0xff)]
    [InlineData("brightcyan", 0x00, 0xff, 0xff)]
    [InlineData("brightwhite", 0xff, 0xff, 0xff)]
    [InlineData("error", 0xff, 0x00, 0x00)]
    [InlineData("warning", 0xff, 0xff, 0x00)]
    [InlineData("hotkey", 0xff, 0xff, 0x00)]
    [InlineData("success", 0x00, 0xff, 0x00)]
    [InlineData("info", 0x00, 0xff, 0xff)]
    [InlineData("accent", 0x00, 0xff, 0xff)]
    [InlineData("muted", 0x7f, 0x7f, 0x7f)]
    public void TryFromName_WhenNameIsSupported_ReturnsReferenceRgb(
        string name,
        int red,
        int green,
        int blue)
    {
        var parsed = Color.TryFromName(name, out var color);

        parsed.ShouldBeTrue();
        color.ShouldBe(Color.Rgb(red, green, blue));
    }

    /// <summary>Verifies name parsing is case-insensitive.</summary>
    [Fact]
    public void TryFromName_WhenNameUsesMixedCase_ReturnsReferenceRgb()
    {
        Color.TryFromName("BrIgHtBlUe", out var color).ShouldBeTrue();

        color.ShouldBe(Color.Rgb(0x5c, 0x5c, 0xff));
    }

    /// <summary>Verifies unsupported names leave the output at terminal default.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("orange")]
    public void TryFromName_WhenNameIsUnsupported_ReturnsFalseAndDefault(string? name)
    {
        Color.TryFromName(name, out var color).ShouldBeFalse();

        color.ShouldBe(Color.Default);
    }

    #endregion
}
