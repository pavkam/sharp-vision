// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies hex parsing of terminal RGB colors.</summary>
public sealed class ColorHexTests
{
    /// <summary>Verifies that a six-digit hex string parses into the exact RGB components.</summary>
    [Fact]
    public void FromHex_WhenSixDigits_ParsesRgb()
    {
        Color color = Color.FromHex("#1a2b3c");

        color.Kind.ShouldBe(ColorKind.Rgb);
        color.Red.ShouldBe((byte) 0x1a);
        color.Green.ShouldBe((byte) 0x2b);
        color.Blue.ShouldBe((byte) 0x3c);
    }

    /// <summary>Verifies that a three-digit hex string expands each nibble to a full byte.</summary>
    [Fact]
    public void FromHex_WhenThreeDigits_ExpandsNibbles()
    {
        Color color = Color.FromHex("f80");

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
        Color.TryFromHex("#nope", out Color color).ShouldBeFalse();
        color.ShouldBe(Color.Default);
    }
}
