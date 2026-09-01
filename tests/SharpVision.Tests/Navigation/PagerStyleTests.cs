// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the complete immutable Pager presentation.</summary>
public sealed class PagerStyleTests
{
    /// <summary>Verifies the shared glyph value rejects terminal controls and wide scalars before
    /// they can enter a Pager style.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0x4e16)]
    public void ControlGlyph_WhenPagerCandidateIsNotPrintableOneCell_Throws(int scalar)
    {
        var rune = new Rune(scalar);

        _ = Should.Throw<ArgumentException>(() => new ControlGlyph(rune, new Rune('?')));
        _ = Should.Throw<ArgumentException>(() => new ControlGlyph(new Rune('?'), rune));
    }

    /// <summary>Verifies the standard presentation owns all semantic glyph and color defaults.</summary>
    [Fact]
    public void Default_WhenResolved_UsesCodeOwnedPresentation()
    {
        var actual = PagerStyle.Default;

        actual.FirstPageGlyph.ShouldBe(new ControlGlyph(new Rune('«'), new Rune('<')));
        actual.PreviousPageGlyph.ShouldBe(new ControlGlyph(new Rune('‹'), new Rune('<')));
        actual.NextPageGlyph.ShouldBe(new ControlGlyph(new Rune('›'), new Rune('>')));
        actual.LastPageGlyph.ShouldBe(new ControlGlyph(new Rune('»'), new Rune('>')));
        actual.OmittedPagesGlyph.ShouldBe(new ControlGlyph(new Rune('…'), new Rune('.')));
        actual.CurrentPageColor.ShouldBe(SemanticColor.Accent);
    }

    /// <summary>Verifies structural equality includes every Pager presentation member.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = PagerStyle.Default;
        var equivalent = new PagerStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.FirstPageGlyph,
            baseline.PreviousPageGlyph,
            baseline.NextPageGlyph,
            baseline.LastPageGlyph,
            baseline.OmittedPagesGlyph,
            baseline.CurrentPageColor);

        equivalent.ShouldBe(baseline);
    }

    /// <summary>Verifies transparent current-page paint is rejected before construction completes.</summary>
    [Fact]
    public void Constructor_WhenCurrentPageColorIsTransparent_Throws()
    {
        var baseline = PagerStyle.Default;

        _ = Should.Throw<ArgumentException>(() => new PagerStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.FirstPageGlyph,
            baseline.PreviousPageGlyph,
            baseline.NextPageGlyph,
            baseline.LastPageGlyph,
            baseline.OmittedPagesGlyph,
            Color.Transparent));
    }

    /// <summary>Verifies replacement paint is validated by the immutable init accessor.</summary>
    [Fact]
    public void With_WhenCurrentPageColorIsTransparent_Throws()
    {
        var baseline = PagerStyle.Default;

        _ = Should.Throw<ArgumentException>(() => baseline with { CurrentPageColor = Color.Transparent });
    }

    /// <summary>Verifies every glyph init accessor rejects the invalid default value rather than
    /// allowing a later measure pass to encounter an unprintable scalar.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void With_WhenGlyphIsInvalid_Throws(int member)
    {
        var baseline = PagerStyle.Default;

        _ = Should.Throw<ArgumentException>(() => member switch
        {
            0 => baseline with { FirstPageGlyph = default },
            1 => baseline with { PreviousPageGlyph = default },
            2 => baseline with { NextPageGlyph = default },
            3 => baseline with { LastPageGlyph = default },
            4 => baseline with { OmittedPagesGlyph = default },
            _ => throw new UnreachableException()
        });
    }

    /// <summary>Verifies constructor validation finishes before assigning any complete style
    /// member, including the value-type default that bypasses ControlGlyph's constructor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Constructor_WhenGlyphIsInvalid_Throws(int member)
    {
        var baseline = PagerStyle.Default;
        var glyphs = new[]
        {
            baseline.FirstPageGlyph,
            baseline.PreviousPageGlyph,
            baseline.NextPageGlyph,
            baseline.LastPageGlyph,
            baseline.OmittedPagesGlyph
        };
        glyphs[member] = default;

        _ = Should.Throw<ArgumentException>(() => new PagerStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            glyphs[0],
            glyphs[1],
            glyphs[2],
            glyphs[3],
            glyphs[4],
            baseline.CurrentPageColor));
    }
}
