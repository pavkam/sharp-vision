// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies complete and partial intrinsic appearance values.</summary>
public sealed class AppearanceValueTests
{
    /// <summary>Verifies a partial border set replaces only the supplied member.</summary>
    [Fact]
    public void Apply_WhenOnlyForegroundIsSet_PreservesBorderDefinition()
    {
        var original = new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            ThemeColor.ControlBorder,
            ThemeColor.Control,
            ThemeDecoration.Border);
        var set = new BorderSet(foreground: ThemeColor.ActiveBorder);

        var result = set.Apply(original);

        result.Sides.ShouldBe(BorderSide.All);
        result.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        result.Foreground.ThemeColor.ShouldBe(ThemeColor.ActiveBorder);
        result.Background.ThemeColor.ShouldBe(ThemeColor.Control);
        result.Attributes.ThemeDecoration.ShouldBe(ThemeDecoration.Border);
    }

    /// <summary>Verifies transparent foregrounds are rejected before a face is constructed.</summary>
    [Fact]
    public void Constructor_WhenForegroundIsTransparent_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Transparent,
            Color.Default,
            Attributes.None,
            Underline.None,
            Color.Default));
    }

    /// <summary>Verifies custom border art retains all eight validated Runes.</summary>
    [Fact]
    public void Constructor_WhenBorderGlyphStyleIsCustom_PreservesEverySegment()
    {
        var style = new BorderGlyphStyle(
            new Rune('1'),
            new Rune('2'),
            new Rune('3'),
            new Rune('4'),
            new Rune('5'),
            new Rune('6'),
            new Rune('7'),
            new Rune('8'));

        style.TopLeft.ShouldBe(new Rune('1'));
        style.Top.ShouldBe(new Rune('2'));
        style.TopRight.ShouldBe(new Rune('3'));
        style.Right.ShouldBe(new Rune('4'));
        style.BottomRight.ShouldBe(new Rune('5'));
        style.Bottom.ShouldBe(new Rune('6'));
        style.BottomLeft.ShouldBe(new Rune('7'));
        style.Left.ShouldBe(new Rune('8'));
    }
}
