// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>Verifies named color parsing owned by the terminal color value.</summary>
public sealed class ColorNameTests
{
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
}
