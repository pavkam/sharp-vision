// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable CheckMark layout and glyph family, including the Square preset
/// that no consuming control (Brackets and Tick are already proven through CheckBox and TreeView)
/// otherwise exercises.</summary>
public sealed class CheckMarkTests
{
    /// <summary>Verifies the Square preset resolves the documented one-cell layout and default
    /// glyph family.</summary>
    [Fact]
    public void Square_ResolvesOneCellLayoutWithDefaultGlyphs()
    {
        CheckMark.Square.MarkStyle.ShouldBe(CheckBoxMarkStyle.Square);
        CheckMark.Square.Glyphs.ShouldBe(CheckBoxGlyphs.Default);
        CheckMark.Square.Width.ShouldBe(1);
    }

    /// <summary>Verifies the constructor rejects an undefined mark style.</summary>
    [Fact]
    public void Constructor_WhenMarkStyleIsUndefined_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new CheckMark((CheckBoxMarkStyle) 99, CheckBoxGlyphs.Default));

    /// <summary>Verifies the constructor round-trips a validated layout and glyph family.</summary>
    [Fact]
    public void Constructor_WhenValid_RoundTripsMarkStyleAndGlyphs()
    {
        var glyphs = new CheckBoxGlyphs(new Rune('-'), new Rune('x'), new Rune('~'));

        var mark = new CheckMark(CheckBoxMarkStyle.Tick, glyphs);

        mark.MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
        mark.Glyphs.ShouldBe(glyphs);
        mark.Width.ShouldBe(1);
    }

    /// <summary>Verifies the Brackets preset resolves the documented three-cell layout, and that
    /// it is also the value a default-constructed CheckMark resolves to.</summary>
    [Fact]
    public void Brackets_ResolvesThreeCellLayoutAndIsTheDefaultValue()
    {
        CheckMark.Brackets.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
        CheckMark.Brackets.Width.ShouldBe(3);
        default(CheckMark).ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies WithGlyphs keeps the receiver's layout while replacing only its glyph
    /// family.</summary>
    [Fact]
    public void WithGlyphs_WhenCalled_KeepsLayoutAndReplacesGlyphs()
    {
        var replacement = new CheckBoxGlyphs(new Rune('_'), new Rune('#'), new Rune('?'));

        var mark = CheckMark.Tick.WithGlyphs(replacement);

        mark.MarkStyle.ShouldBe(CheckMark.Tick.MarkStyle);
        mark.Glyphs.ShouldBe(replacement);
        mark.ShouldNotBe(CheckMark.Tick);
    }

    /// <summary>Verifies WithGlyphs revalidates the replacement family instead of trusting it as
    /// already-validated: a default-uninitialized CheckBoxGlyphs never ran through its own
    /// constructor validation and carries the Rune default value - a non-printable control
    /// character - for every mark, so WithGlyphs must reject it exactly as the CheckMark
    /// constructor's own documented "cannot smuggle an unvalidated rune through" guarantee
    /// promises.</summary>
    [Fact]
    public void WithGlyphs_WhenGivenDefaultUninitializedGlyphs_ThrowsInsteadOfSmugglingControlRune() =>
        _ = Should.Throw<ArgumentException>(() => CheckMark.Tick.WithGlyphs(default));

    /// <summary>Verifies the constructor itself rejects the same default-uninitialized glyph
    /// family smuggling attempt.</summary>
    [Fact]
    public void Constructor_WhenGivenDefaultUninitializedGlyphs_ThrowsInsteadOfSmugglingControlRune() =>
        _ = Should.Throw<ArgumentException>(() => new CheckMark(CheckBoxMarkStyle.Tick, default));

    /// <summary>Verifies GlyphFor selects the exact glyph configured for each of the three
    /// documented states.</summary>
    [Fact]
    public void GlyphFor_WhenGivenEachState_SelectsTheMatchingConfiguredGlyph()
    {
        var glyphs = new CheckBoxGlyphs(new Rune('u'), new Rune('c'), new Rune('i'));
        var mark = new CheckMark(CheckBoxMarkStyle.Tick, glyphs);

        mark.GlyphFor(true).ShouldBe(new Rune('c'));
        mark.GlyphFor(false).ShouldBe(new Rune('u'));
        mark.GlyphFor(null).ShouldBe(new Rune('i'));
    }

    /// <summary>Verifies the equality operators and GetHashCode agree with Equals for both matching
    /// and differing marks, matching the documented presentation-equality contract.</summary>
    [Fact]
    public void EqualityOperators_WhenComparingMarks_MatchEqualsAndHashCode()
    {
        var left = new CheckMark(CheckBoxMarkStyle.Tick, CheckBoxStyle.Tick.Glyphs);
        var right = new CheckMark(CheckBoxMarkStyle.Tick, CheckBoxStyle.Tick.Glyphs);
        var different = CheckMark.Square;

        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.Equals((object) right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());

        (left == different).ShouldBeFalse();
        (left != different).ShouldBeTrue();
        left.Equals((object) different).ShouldBeFalse();
        left.Equals("not a mark").ShouldBeFalse();
    }
}
