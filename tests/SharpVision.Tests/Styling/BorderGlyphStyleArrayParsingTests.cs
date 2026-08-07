// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies theme documents can specify an explicit eight-Rune border glyph array
/// alongside the ten named families.</summary>
public sealed class BorderGlyphStyleArrayParsingTests
{
    /// <summary>Verifies an explicit eight-Rune array resolves to the matching BorderGlyphStyle.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsExplicitArray_ResolvesEachSegmentInOrder()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7","8"]""");

        var theme = ThemeCatalog.Parse(json, "t");
        var style = theme.Input.Normal.Border.GlyphStyle;

        style.TopLeft.ShouldBe(new Rune('1'));
        style.Top.ShouldBe(new Rune('2'));
        style.TopRight.ShouldBe(new Rune('3'));
        style.Right.ShouldBe(new Rune('4'));
        style.BottomRight.ShouldBe(new Rune('5'));
        style.Bottom.ShouldBe(new Rune('6'));
        style.BottomLeft.ShouldBe(new Rune('7'));
        style.Left.ShouldBe(new Rune('8'));
    }

    /// <summary>Verifies the named-family form is unaffected by the new array form (regression).</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsNamedFamily_StillResolvesStandardSet()
    {
        var json = ThemeJson.Create(inputGlyphStyle: "\"paired\"");

        var theme = ThemeCatalog.Parse(json, "t");

        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
    }

    /// <summary>Verifies an array with too few Runes is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayIsTooShort_Throws()
    {
        var json = ThemeJson.Create(inputGlyphStyle: """["1","2","3"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an array with too many Runes is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayIsTooLong_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7","8","9"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an array element containing more than one Rune is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayElementHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["ab","2","3","4","5","6","7","8"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies a non-string array element is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayElementIsNotString_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7",8]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an unrecognized string family name is still rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsUnknownName_Throws()
    {
        var json = ThemeJson.Create(inputGlyphStyle: "\"not-a-family\"");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }
}
