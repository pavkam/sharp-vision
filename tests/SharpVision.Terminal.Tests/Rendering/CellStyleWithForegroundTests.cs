// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies <see cref="CellStyle.WithForeground"/> preserves all non-foreground fields.</summary>
public sealed class CellStyleWithForegroundTests
{
    /// <summary>Verifies that WithForeground on a default style changes only the foreground.</summary>
    [Fact]
    public void WithForeground_WhenDefaultStyle_ReturnsCopyWithNewForeground()
    {
        var original = CellStyle.Default;
        var result = original.WithForeground(Color.Rgb(255, 0, 0));

        result.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
        result.Background.ShouldBe(original.Background);
        result.Attributes.ShouldBe(original.Attributes);
        result.Hyperlink.ShouldBeNull();
        result.Underline.ShouldBe(original.Underline);
        result.UnderlineColor.ShouldBe(original.UnderlineColor);
    }

    /// <summary>Verifies that WithForeground preserves background, attributes, hyperlink, underline, and underline color.</summary>
    [Fact]
    public void WithForeground_WhenFullyPopulated_PreservesAllOtherFields()
    {
        var original = new CellStyle(
            foreground: Color.Rgb(10, 20, 30),
            background: Color.Rgb(40, 50, 60),
            attributes: TerminalAttributes.Bold | TerminalAttributes.Italic,
            hyperlink: "https://example.com",
            underline: Underline.Curly,
            underlineColor: Color.Rgb(70, 80, 90));

        var result = original.WithForeground(Color.Rgb(100, 110, 120));

        result.Foreground.ShouldBe(Color.Rgb(100, 110, 120));
        result.Background.ShouldBe(Color.Rgb(40, 50, 60));
        result.Attributes.ShouldBe(TerminalAttributes.Bold | TerminalAttributes.Italic);
        result.Hyperlink.ShouldBe("https://example.com");
        result.Underline.ShouldBe(Underline.Curly);
        result.UnderlineColor.ShouldBe(Color.Rgb(70, 80, 90));
    }

    /// <summary>Verifies that multiple WithForeground calls produce independent copies.</summary>
    [Fact]
    public void WithForeground_WhenCalledMultipleTimes_EachReturnIsIndependent()
    {
        var original = new CellStyle(foreground: Color.Rgb(1, 2, 3));

        var first = original.WithForeground(Color.Rgb(10, 20, 30));
        var second = original.WithForeground(Color.Rgb(40, 50, 60));

        first.Foreground.ShouldBe(Color.Rgb(10, 20, 30));
        second.Foreground.ShouldBe(Color.Rgb(40, 50, 60));
        original.Foreground.ShouldBe(Color.Rgb(1, 2, 3));
    }
}
