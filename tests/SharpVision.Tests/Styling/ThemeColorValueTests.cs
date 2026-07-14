// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the theme color-value grammar (hex, indexed, palette-key discrimination).</summary>
public sealed class ThemeColorValueTests
{
    /// <summary>Verifies hex and indexed literals are recognized as inline literals.</summary>
    [Theory]
    [InlineData("#fff")]
    [InlineData("#1a2b3c")]
    [InlineData("idx:0")]
    [InlineData("idx:255")]
    public void IsLiteral_WhenLiteral_ReturnsTrue(string value) =>
        ThemeColorValue.IsLiteral(value).ShouldBeTrue();

    /// <summary>Verifies values that are not literals are treated as palette keys.</summary>
    [Theory]
    [InlineData("blue")]
    [InlineData("bg-dark")]
    public void IsLiteral_WhenPaletteKey_ReturnsFalse(string value) =>
        ThemeColorValue.IsLiteral(value).ShouldBeFalse();

    /// <summary>Verifies a hex literal parses to the matching RGB color.</summary>
    [Fact]
    public void ParseLiteral_WhenHex_ReturnsRgb() =>
        ThemeColorValue.ParseLiteral("#1a2b3c").ShouldBe(Color.Rgb(0x1a, 0x2b, 0x3c));

    /// <summary>Verifies an <c>idx:N</c> literal parses to the matching indexed color.</summary>
    [Fact]
    public void ParseLiteral_WhenIndexed_ReturnsIndexed() =>
        ThemeColorValue.ParseLiteral("idx:8").ShouldBe(Color.Indexed(8));

    /// <summary>Verifies malformed or out-of-range literals throw <see cref="FormatException"/>.</summary>
    [Theory]
    [InlineData("idx:256")]
    [InlineData("idx:-1")]
    [InlineData("idx:")]
    [InlineData("idx:x")]
    [InlineData("#gg0000")]
    public void ParseLiteral_WhenMalformed_Throws(string value) =>
        Should.Throw<FormatException>(() => ThemeColorValue.ParseLiteral(value));
}
