// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the complete immutable Pager presentation.</summary>
public sealed class PagerStyleTests
{
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
}
