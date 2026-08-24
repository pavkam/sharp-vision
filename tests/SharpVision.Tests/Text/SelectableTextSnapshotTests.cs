// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using Moq;

/// <summary>Verifies selectable-text projection validation and ownership.</summary>
public sealed class SelectableTextSnapshotTests
{
    /// <summary>Verifies glyph ranges remain contained by the semantic text.</summary>
    [Fact]
    public void Constructor_WhenGlyphRangeExceedsText_Throws()
    {
        var glyph = new SelectableTextGlyph(new Selection(0, 2), new Rect(0, 0, 1, 1));

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SelectableTextSnapshot("a", [glyph], isAuthoritative: true));

        exception.ParamName.ShouldBe("glyphs");
    }

    /// <summary>Verifies the snapshot retains an owned copy of the supplied glyphs.</summary>
    [Fact]
    public void Constructor_WhenInputArrayIsMutated_RetainsOwnedGlyphs()
    {
        var originalBounds = new Rect(0, 0, 1, 1);
        var glyphs = new[]
        {
            new SelectableTextGlyph(new Selection(0, 1), originalBounds),
        };
        var snapshot = new SelectableTextSnapshot("a", glyphs, isAuthoritative: true);

        glyphs[0] = new SelectableTextGlyph(new Selection(0, 1), new Rect(5, 5, 2, 1));

        snapshot.Glyphs[0].Bounds.ShouldBe(originalBounds);
    }

    /// <summary>Verifies a visible glyph always maps at least one semantic grapheme.</summary>
    [Fact]
    public void Constructor_WhenGlyphRangeIsEmpty_Throws()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new SelectableTextGlyph(new Selection(1, 1), new Rect(0, 0, 1, 1)));
    }

    /// <summary>Verifies visible glyph rectangles own positive cell extents.</summary>
    /// <param name="width">The candidate cell width.</param>
    /// <param name="height">The candidate cell height.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Constructor_WhenGlyphBoundsAreNotPositive_Throws(int width, int height)
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SelectableTextGlyph(new Selection(0, 1), new Rect(0, 0, width, height)));
    }

    /// <summary>Verifies semantic text is required.</summary>
    [Fact]
    public void Constructor_WhenTextIsNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            new SelectableTextSnapshot(null!, [], isAuthoritative: true));
    }

    /// <summary>Verifies the visible glyph collection is required.</summary>
    [Fact]
    public void Constructor_WhenGlyphsAreNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            new SelectableTextSnapshot("a", null!, isAuthoritative: true));
    }

    /// <summary>Verifies null glyph entries are rejected as invalid collection content.</summary>
    [Fact]
    public void Constructor_WhenGlyphEntryIsNull_Throws()
    {
        var glyphs = new SelectableTextGlyph[1];

        var exception = Should.Throw<ArgumentNullException>(() =>
            new SelectableTextSnapshot("a", glyphs, isAuthoritative: true));

        exception.ParamName.ShouldBe("glyphs");
    }

    /// <summary>Verifies projected ranges cannot expose part of a grapheme cluster.</summary>
    [Fact]
    public void Constructor_WhenGlyphEndpointSplitsGrapheme_Throws()
    {
        const string text = "e\u0301";
        var glyph = new SelectableTextGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1));

        var exception = Should.Throw<ArgumentException>(() =>
            new SelectableTextSnapshot(text, [glyph], isAuthoritative: true));

        exception.ParamName.ShouldBe("glyphs");
    }

    /// <summary>Verifies one projected glyph cannot span multiple complete graphemes.</summary>
    [Fact]
    public void Constructor_WhenGlyphSpansMultipleGraphemes_Throws()
    {
        var glyph = new SelectableTextGlyph(new Selection(0, 2), new Rect(0, 0, 2, 1));

        var exception = Should.Throw<ArgumentException>(() =>
            new SelectableTextSnapshot("ab", [glyph], isAuthoritative: true));

        exception.ParamName.ShouldBe("glyphs");
    }

    /// <summary>Verifies validation never rereads the caller-owned glyph collection.</summary>
    [Fact]
    public void Constructor_WhenGlyphCollectionIsAdversarial_MaterializesItOnce()
    {
        var glyph = new SelectableTextGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1));
        var enumerationCount = 0;
        var glyphs = new Mock<IReadOnlyList<SelectableTextGlyph>>(MockBehavior.Strict);
        _ = glyphs
            .Setup(static value => value.GetEnumerator())
            .Returns(() => enumerationCount++ == 0
                ? new[] { glyph }.AsEnumerable().GetEnumerator()
                : throw new InvalidOperationException("The caller-owned collection was reread."));

        var snapshot = new SelectableTextSnapshot("a", glyphs.Object, isAuthoritative: true);

        enumerationCount.ShouldBe(1);
        glyphs.Verify(static value => value.GetEnumerator(), Times.Once);
        glyphs.VerifyNoOtherCalls();
        snapshot.Glyphs.ShouldBe([glyph]);
    }

    /// <summary>Verifies duplicate visual projections may map the same semantic grapheme.</summary>
    [Fact]
    public void Constructor_WhenGlyphRangeIsRepeated_PreservesBothGlyphs()
    {
        var first = new SelectableTextGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1));
        var second = new SelectableTextGlyph(new Selection(0, 1), new Rect(2, 0, 1, 1));

        var snapshot = new SelectableTextSnapshot("a", [first, second], isAuthoritative: true);

        snapshot.Glyphs.ShouldBe([first, second]);
    }

    /// <summary>Verifies valid construction preserves semantic and authority values.</summary>
    [Fact]
    public void Constructor_WhenProjectionIsValid_PreservesValues()
    {
        const string text = "e\u0301";
        var glyph = new SelectableTextGlyph(
            new Selection(0, text.Length),
            new Rect(2, 3, 1, 1));

        var snapshot = new SelectableTextSnapshot(text, [glyph], isAuthoritative: false);

        snapshot.Text.ShouldBeSameAs(text);
        snapshot.Glyphs.ShouldBe([glyph]);
        snapshot.IsAuthoritative.ShouldBeFalse();
    }
}
