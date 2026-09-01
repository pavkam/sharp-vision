// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies the immutable Breadcrumb presentation and separator policy.</summary>
public sealed class BreadcrumbStyleTests
{
    /// <summary>Verifies the default separator and color are complete and paintable.</summary>
    [Fact]
    public void Default_WhenRead_ProvidesCompleteSeparatorPresentation()
    {
        var style = BreadcrumbStyle.Default;

        style.SeparatorGlyph.Value.ShouldBe(new Rune('›'));
        style.SeparatorGlyph.Fallback.ShouldBe(new Rune('>'));
        style.SeparatorColor.ShouldBe(SemanticColor.ControlBorder);
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
            Color.Transparent));
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
}
