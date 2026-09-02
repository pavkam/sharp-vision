// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the immutable Breadcrumb presentation and separator policy.</summary>
public sealed class BreadcrumbStyleTests
{
    /// <summary>Verifies the default separator, color, and symmetric spacing are complete.</summary>
    [Fact]
    public void Default_WhenRead_ProvidesCompleteSeparatorPresentation()
    {
        var style = BreadcrumbStyle.Default;

        style.SeparatorGlyph.Value.ShouldBe(new Rune('›'));
        style.SeparatorGlyph.Fallback.ShouldBe(new Rune('>'));
        style.SeparatorColor.ShouldBe(SemanticColor.ControlBorder);
        style.SeparatorSpacingBefore.ShouldBe(1);
        style.SeparatorSpacingAfter.ShouldBe(1);
    }

    /// <summary>Verifies transparent separator paint is rejected before construction completes.</summary>
    [Fact]
    public void Constructor_WhenSeparatorColorIsTransparent_Throws()
    {
        var baseline = ControlStyle.Default;

        _ = Should.Throw<ArgumentException>(() => new BreadcrumbStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            new ControlGlyph(new Rune('>'), new Rune('>')),
            Color.Transparent,
            separatorSpacingBefore: 1,
            separatorSpacingAfter: 1));
    }

    /// <summary>Verifies negative constructor spacing is rejected before a style can be observed.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_WhenSeparatorSpacingIsNegative_Throws(int before, int after)
    {
        var baseline = ControlStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new BreadcrumbStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            new ControlGlyph(new Rune('>'), new Rune('>')),
            SemanticColor.ControlBorder,
            before,
            after));
    }

    /// <summary>Verifies with-expression spacing validation leaves the source style unchanged.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Spacing_WhenReplacementIsNegative_ThrowsWithoutMutatingSource(bool before)
    {
        var style = BreadcrumbStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => before
            ? style with { SeparatorSpacingBefore = -1 }
            : style with { SeparatorSpacingAfter = -1 });

        style.SeparatorSpacingBefore.ShouldBe(1);
        style.SeparatorSpacingAfter.ShouldBe(1);
    }

    /// <summary>Verifies a changed separator glyph affects rendering without forcing layout.</summary>
    [Fact]
    public void Definition_WhenSeparatorGlyphChanges_InvalidatesRender()
    {
        var previous = BreadcrumbStyle.Default;
        var current = previous with
        {
            SeparatorGlyph = new ControlGlyph(new Rune('/'), new Rune('>'))
        };

        BreadcrumbStyle.Definition.Compare(previous, null, current, null)
            .ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies spacing changes remeasure every dependent geometry path.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Definition_WhenSeparatorSpacingChanges_InvalidatesMeasure(bool before)
    {
        var previous = BreadcrumbStyle.Default;
        var current = before
            ? previous with { SeparatorSpacingBefore = 2 }
            : previous with { SeparatorSpacingAfter = 2 };

        BreadcrumbStyle.Definition.Compare(previous, null, current, null)
            .ShouldBe(InvalidationImpact.Measure);
    }
}
