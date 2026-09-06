// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies ControlGlyph validates its Value and Fallback through the shared
/// CellGlyphResolver.ValidateSingleCell extension instead of a hand-rolled duplicate.</summary>
public sealed class ControlGlyphTests
{
    /// <summary>Verifies two printable one-cell Runes construct without throwing.</summary>
    [Fact]
    public void Constructor_WhenValueAndFallbackAreOneCell_AssignsBoth()
    {
        var value = new Rune('x');
        var fallback = new Rune('?');

        var glyph = new ControlGlyph(value, fallback);

        glyph.Value.ShouldBe(value);
        glyph.Fallback.ShouldBe(fallback);
    }

    /// <summary>Verifies a wide CJK ideograph Value is rejected with the shared resolver's
    /// message and the Value parameter name.</summary>
    [Fact]
    public void Constructor_WhenValueIsWide_ThrowsArgumentException()
    {
        // U+4E16 '世' is a wide CJK ideograph (two cells wide).
        var wide = new Rune(0x4E16);

        var exception = Should.Throw<ArgumentException>(
            () => new ControlGlyph(wide, new Rune('?')));

        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldStartWith("The rune must be printable and one cell wide.");
    }

    /// <summary>Verifies a control character Fallback is rejected with the Fallback parameter
    /// name.</summary>
    [Fact]
    public void Constructor_WhenFallbackIsControlCharacter_ThrowsArgumentException()
    {
        var control = new Rune('\0');

        var exception = Should.Throw<ArgumentException>(
            () => new ControlGlyph(new Rune('x'), control));

        exception.ParamName.ShouldBe("fallback");
    }

    /// <summary>Verifies Resolve applies the single glyph-repair rule: it returns the preferred
    /// glyph under a policy where the ambiguous-width value still measures one cell, and the
    /// portable fallback under a policy where that same value measures two cells.</summary>
    [Fact]
    public void Resolve_WhenAmbiguousWidthIsNarrow_ReturnsFallbackOnlyForAmbiguousValue()
    {
        // U+00B7 MIDDLE DOT is East Asian Ambiguous: one cell under Narrow, two under Wide.
        var glyph = new ControlGlyph(new Rune(0x00B7), new Rune('.'));

        var underNarrow = glyph.Resolve(Ambiguous.Narrow);
        var underWide = glyph.Resolve(Ambiguous.Wide);

        underNarrow.ShouldBe(glyph.Value);
        underWide.ShouldBe(glyph.Fallback);
    }
}
