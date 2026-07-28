// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies <see cref="CellGlyphResolver.ValidateSingleCell"/> accepts narrow printable runes
/// and rejects wide or control runes.</summary>
public sealed class CellGlyphResolverTests
{
    /// <summary>Verifies a standard ASCII letter passes validation.</summary>
    [Fact]
    public void ValidateSingleCell_WhenNarrowAsciiRune_ReturnsRune()
    {
        var rune = new Rune('A');
        var result = CellGlyphResolver.ValidateSingleCell(rune, "test");
        result.ShouldBe(rune);
    }

    /// <summary>Verifies a wide CJK ideograph is rejected.</summary>
    [Fact]
    public void ValidateSingleCell_WhenWideCjkCharacter_ThrowsArgumentException()
    {
        // U+4E16 '世' is a wide CJK ideograph (two cells wide).
        var wide = new Rune(0x4E16);
        var ex = Should.Throw<ArgumentException>(
            () => CellGlyphResolver.ValidateSingleCell(wide, "glyph"));
        ex.ParamName.ShouldBe("glyph");
    }

    /// <summary>Verifies a control character is rejected.</summary>
    [Fact]
    public void ValidateSingleCell_WhenControlCharacter_ThrowsArgumentException()
    {
        // U+0000 NUL is a control character.
        var control = new Rune('\0');
        var ex = Should.Throw<ArgumentException>(
            () => CellGlyphResolver.ValidateSingleCell(control, "value"));
        ex.ParamName.ShouldBe("value");
    }
}
