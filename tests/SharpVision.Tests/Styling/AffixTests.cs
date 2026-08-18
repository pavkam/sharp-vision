// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies Affix construction validation and Unicode width behavior, independent of any
/// hosting control's layout or render seam.</summary>
public sealed class AffixTests
{
    /// <summary>Verifies a printable single-cell content string with the default fallback constructs.</summary>
    [Fact]
    public void Constructor_WhenContentIsOneCell_UsesDefaultFallback()
    {
        var affix = new Affix("!");

        affix.Content.ShouldBe("!");
        affix.Fallback.ShouldBe("?");
        affix.Color.ShouldBeNull();
    }

    /// <summary>Verifies a wide two-cell CJK content string constructs with an explicit fallback and color.</summary>
    [Fact]
    public void Constructor_WhenContentIsTwoCellsWide_AssignsExplicitFallbackAndColor()
    {
        // U+4E16 is a wide CJK ideograph (two cells wide under every policy).
        var affix = new Affix("世", "!", SemanticColor.Warning);

        affix.Content.ShouldBe("世");
        affix.Fallback.ShouldBe("!");
        affix.Color.ShouldBe(new ControlColor(SemanticColor.Warning));
    }

    /// <summary>Verifies a null content string is rejected before any other validation.</summary>
    [Fact]
    public void Constructor_WhenContentIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new Affix(null!));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies an empty content string is rejected.</summary>
    [Fact]
    public void Constructor_WhenContentIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix(""));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies content containing a control cluster is rejected.</summary>
    [Fact]
    public void Constructor_WhenContentContainsControlCharacter_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix("\0"));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies content spanning more than one grapheme cluster is rejected.</summary>
    [Fact]
    public void Constructor_WhenContentSpansMultipleGraphemeClusters_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix("ab"));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies an unattached combining mark - a single grapheme cluster with no base
    /// scalar to attach to - is rejected instead of silently accepted as printable content.</summary>
    [Fact]
    public void Constructor_WhenContentIsAnUnattachedCombiningMark_ThrowsArgumentException()
    {
        // U+0301 COMBINING ACUTE ACCENT with no preceding base scalar.
        var exception = Should.Throw<ArgumentException>(() => new Affix("́"));

        exception.ParamName.ShouldBe("content");
    }

    /// <summary>Verifies a base scalar followed by a combining mark - one ordinary grapheme
    /// cluster - is accepted, distinguishing "starts with a mark" from "contains a mark".</summary>
    [Fact]
    public void Constructor_WhenContentIsABaseScalarWithACombiningMark_ConstructsSuccessfully()
    {
        // 'e' + U+0301 COMBINING ACUTE ACCENT is one grapheme cluster (an unnormalized "e").
        var affix = new Affix("é");

        affix.Content.ShouldBe("é");
    }

    /// <summary>Verifies a null fallback is rejected.</summary>
    [Fact]
    public void Constructor_WhenFallbackIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new Affix("!", null!));

        exception.ParamName.ShouldBe("fallback");
    }

    /// <summary>Verifies an empty fallback is rejected.</summary>
    [Fact]
    public void Constructor_WhenFallbackIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix("!", ""));

        exception.ParamName.ShouldBe("fallback");
    }

    /// <summary>Verifies a non-ASCII fallback is rejected.</summary>
    [Fact]
    public void Constructor_WhenFallbackIsNonAscii_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix("!", "é"));

        exception.ParamName.ShouldBe("fallback");
    }

    /// <summary>Verifies a control-character fallback is rejected.</summary>
    [Fact]
    public void Constructor_WhenFallbackIsControlCharacter_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new Affix("!", "\t"));

        exception.ParamName.ShouldBe("fallback");
    }

    /// <summary>Verifies an East Asian Ambiguous-width content scalar resolves to different cell
    /// counts under the Narrow and Wide policies, the exact dynamic the seam's "reserve what you
    /// measured" rule depends on.</summary>
    [Fact]
    public void Content_WhenAmbiguousWidthScalar_MeasuresDifferentlyUnderNarrowAndWide()
    {
        // U+00B7 MIDDLE DOT is East Asian Ambiguous: one cell under Narrow, two under Wide.
        var affix = new Affix("·");

        Width.Measure(affix.Content, Ambiguous.Narrow).Cells.ShouldBe(1);
        Width.Measure(affix.Content, Ambiguous.Wide).Cells.ShouldBe(2);
    }

    /// <summary>Verifies an unambiguous wide CJK content scalar measures two cells under both policies.</summary>
    [Fact]
    public void Content_WhenWideScalar_MeasuresTwoCellsUnderEitherPolicy()
    {
        // U+4E16 is a wide CJK ideograph (two cells wide under every policy).
        var affix = new Affix("世");

        Width.Measure(affix.Content, Ambiguous.Narrow).Cells.ShouldBe(2);
        Width.Measure(affix.Content, Ambiguous.Wide).Cells.ShouldBe(2);
    }
}
