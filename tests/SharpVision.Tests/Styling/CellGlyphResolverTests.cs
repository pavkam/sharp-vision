// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies fixed-cell glyph validation and ambient-width fallback.</summary>
public sealed class CellGlyphResolverTests
{
    /// <summary>Verifies a standard ASCII letter passes validation.</summary>
    [Fact]
    public void ValidateSingleCell_WhenNarrowAsciiRune_ReturnsRune()
    {
        var rune = new Rune('A');
        var result = rune.ValidateSingleCell("test");
        result.ShouldBe(rune);
    }

    /// <summary>Verifies a wide CJK ideograph is rejected.</summary>
    [Fact]
    public void ValidateSingleCell_WhenWideCjkCharacter_ThrowsArgumentException()
    {
        // U+4E16 '世' is a wide CJK ideograph (two cells wide).
        var wide = new Rune(0x4E16);
        var ex = Should.Throw<ArgumentException>(
            () => wide.ValidateSingleCell("glyph"));
        ex.ParamName.ShouldBe("glyph");
    }

    /// <summary>Verifies a control character is rejected.</summary>
    [Fact]
    public void ValidateSingleCell_WhenControlCharacter_ThrowsArgumentException()
    {
        // U+0000 NUL is a control character.
        var control = new Rune('\0');
        var ex = Should.Throw<ArgumentException>(
            () => control.ValidateSingleCell("value"));
        ex.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies the default overload validates against Narrow, accepting a glyph that an
    /// explicit Wide policy would reject.</summary>
    [Fact]
    public void ValidateSingleCell_WhenAmbiguousWidthRune_AcceptsUnderNarrowDefault()
    {
        // U+00B7 MIDDLE DOT is East Asian Ambiguous: one cell under Narrow, two under Wide.
        var ambiguous = new Rune(0x00B7);

        var result = ambiguous.ValidateSingleCell("glyph");

        result.ShouldBe(ambiguous);
    }

    /// <summary>Verifies an ambiguous-width glyph uses its portable fallback when the ambient
    /// policy makes the requested glyph wide.</summary>
    [Fact]
    public void Resolve_WhenAmbiguousWidthRuneBecomesWide_ReturnsFallback()
    {
        var ambiguous = new Rune(0x00B7);
        var fallback = new Rune('.');

        var result = ambiguous.Resolve(fallback, Ambiguous.Wide);

        result.ShouldBe(fallback);
    }

    /// <summary>Verifies the explicit-policy overload rejects an Ambiguous-width glyph under Wide.</summary>
    [Fact]
    public void ValidateSingleCell_WhenAmbiguousWidthRuneValidatedUnderWide_ThrowsArgumentException()
    {
        var ambiguous = new Rune(0x00B7);

        var ex = Should.Throw<ArgumentException>(
            () => ambiguous.ValidateSingleCell("glyph", Ambiguous.Wide));

        ex.ParamName.ShouldBe("glyph");
    }

    /// <summary>Verifies the explicit-policy overload still accepts the same glyph under Narrow.</summary>
    [Fact]
    public void ValidateSingleCell_WhenAmbiguousWidthRuneValidatedUnderNarrow_ReturnsRune()
    {
        var ambiguous = new Rune(0x00B7);

        var result = ambiguous.ValidateSingleCell("glyph", Ambiguous.Narrow);

        result.ShouldBe(ambiguous);
    }
}
