// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

/// <summary>Verifies the core indexed semantic-text selection map.</summary>
public sealed class TextSelectionMapTests
{
    /// <summary>Verifies navigation moves only across complete extended grapheme boundaries.</summary>
    [Fact]
    public void BoundaryNavigation_WhenTextContainsExtendedGraphemes_MovesAtomically()
    {
        var text = "Ae\u0301\ud83d\udc69\u200d\ud83d\udcbb";
        var map = new TextSelectionMap(text, [], [], 0);

        map.NextBoundary(0).ShouldBe(1);
        map.NextBoundary(1).ShouldBe(3);
        map.NextBoundary(3).ShouldBe(text.Length);
        map.PreviousBoundary(text.Length).ShouldBe(3);
        map.PreviousBoundary(3).ShouldBe(1);
    }

    /// <summary>Verifies both cells of a wide grapheme resolve around its visual midpoint.</summary>
    [Fact]
    public void HitTest_WhenGlyphIsWide_UsesCellMidpoint()
    {
        var map = new TextSelectionMap(
            "\u754c",
            [new TextSelectionGlyph(new Selection(0, 1), new Rect(4, 0, 2, 1))],
            [],
            1);

        map.HitTest(new Point(4, 0)).ShouldBe(0);
        map.HitTest(new Point(5, 0)).ShouldBe(1);
    }

    /// <summary>Verifies empty visual rows resolve to the nearest semantic row endpoint.</summary>
    [Fact]
    public void HitTest_WhenRequestedRowIsSparse_ReturnsNearestMappedOffset()
    {
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(0, 2, 1, 1))
            ],
            [],
            3);

        map.HitTest(new Point(0, 1)).ShouldBe(1);
    }

    /// <summary>Verifies visual line boundaries use the complete occupied row extent.</summary>
    [Fact]
    public void VisualLineBoundary_WhenRowContainsSeparatedGlyphs_ReturnsRowEndpoints()
    {
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(1, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(8, 0, 1, 1))
            ],
            [],
            1);

        map.VisualLineBoundary(1, end: false).ShouldBe(0);
        map.VisualLineBoundary(1, end: true).ShouldBe(2);
    }

    /// <summary>Verifies an offset inside a run of several consecutive blank rows never resolves to
    /// a row before the run, reproducing the caret-inversion regression where the tie-break between
    /// the flanking semantic glyphs compared raw UTF-16 character distance instead of visual row
    /// distance: "a" ends only one character before the offset while "b" starts three characters
    /// after it, so the nearer-by-characters glyph ("a") used to win despite its row lying on the
    /// wrong side of the requested offset.</summary>
    [Fact]
    public void TryGetVisualPosition_WhenOffsetIsInsideConsecutiveBlankRows_DoesNotResolveBeforeTheRun()
    {
        var map = new TextSelectionMap(
            "a\n\n\n\nb",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(5, 6), new Rect(0, 4, 1, 1))
            ],
            [],
            5);

        _ = map.TryGetVisualPosition(2, out var row, out _);

        row.ShouldNotBe(0);
    }

    /// <summary>Verifies long rows use the bounded binary query index rather than a linear scan.</summary>
    [Fact]
    public void HitTest_WhenRowIsLong_InspectsLogarithmicEntries()
    {
        const int count = 16_384;
        var glyphs = new TextSelectionGlyph[count];
        for (var index = 0; index < count; index++)
        {
            glyphs[index] = new TextSelectionGlyph(
                new Selection(index, index + 1),
                new Rect(index, 0, 1, 1));
        }
        var map = new TextSelectionMap(new string('a', count), glyphs, [], 1);

        map.HitTest(new Point(count / 2, 0), out var inspected).ShouldBe(count / 2);

        inspected.ShouldBeLessThan(64);
    }
}
