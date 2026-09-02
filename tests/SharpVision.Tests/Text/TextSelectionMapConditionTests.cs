// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

/// <summary>Verifies the edge conditions of the indexed selection map: empty projections, glyph
/// versus caret hit testing, source occurrence resolution, and geometry lookups without glyphs.</summary>
public sealed class TextSelectionMapConditionTests
{
    /// <summary>Verifies the shared empty map answers every query with its neutral value.</summary>
    [Fact]
    public void Empty_WhenQueried_ReturnsNeutralValuesWithoutThrowing()
    {
        var map = TextSelectionMap.Empty;

        map.Text.ShouldBe(string.Empty);
        map.Glyphs.ShouldBeEmpty();
        map.Sources.ShouldBeEmpty();
        map.VisualRowCount.ShouldBe(0);
        map.HitTest(new Point(7, 3)).ShouldBe(0);
        map.HitTestGlyph(new Point(7, 3)).ShouldBe(0);
        map.NextBoundary(0).ShouldBe(0);
        map.PreviousBoundary(0).ShouldBe(0);
        map.OffsetAtVisualColumn(4, 4).ShouldBe(0);
        map.VisualLineBoundary(0, end: true).ShouldBe(0);
        map.TryGetVisualPosition(0, out var row, out var column).ShouldBeFalse();
        row.ShouldBe(0);
        column.ShouldBe(0);
        map.TryGetCaretGeometry(0, out var bounds, out var source).ShouldBeFalse();
        bounds.ShouldBe(default);
        source.ShouldBeNull();
    }

    /// <summary>Verifies the glyph hit test snaps every cell of a wide glyph to its start while the
    /// caret hit test still splits the glyph at its midpoint, and both agree outside any glyph.</summary>
    [Fact]
    public void HitTestGlyph_WhenCellIsInsideAWideGlyph_ReturnsItsStartUnlikeTheCaretRule()
    {
        var map = new TextSelectionMap(
            "a界b",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(1, 0, 2, 1)),
                new TextSelectionGlyph(new Selection(2, 3), new Rect(3, 0, 1, 1))
            ],
            [],
            1);

        map.HitTest(new Point(1, 0)).ShouldBe(1);
        map.HitTest(new Point(2, 0)).ShouldBe(2);
        map.HitTestGlyph(new Point(1, 0)).ShouldBe(1);
        map.HitTestGlyph(new Point(2, 0)).ShouldBe(1);
        map.HitTestGlyph(new Point(3, 0)).ShouldBe(2);
        map.HitTestGlyph(new Point(9, 0)).ShouldBe(3);
        map.HitTest(new Point(9, 0)).ShouldBe(3);
    }

    /// <summary>Verifies a cell in the gap between two separated glyphs resolves to the nearer
    /// glyph edge, preferring the previous glyph's end on a tie.</summary>
    [Fact]
    public void HitTest_WhenCellFallsInAGapBetweenGlyphs_ResolvesToTheNearerEdge()
    {
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(5, 0, 1, 1))
            ],
            [],
            1);

        map.HitTest(new Point(1, 0)).ShouldBe(1);
        map.HitTest(new Point(4, 0)).ShouldBe(1);
        map.HitTest(new Point(3, 0)).ShouldBe(1);
        map.HitTestGlyph(new Point(3, 0)).ShouldBe(1);
        map.HitTest(new Point(5, 0)).ShouldBe(1);
        map.HitTest(new Point(6, 0)).ShouldBe(2);
    }

    /// <summary>Verifies a hit below the last row clamps to it and a hit on an empty middle row
    /// resolves to that row's nearest mapped offset.</summary>
    [Fact]
    public void HitTest_WhenRowIsOutsideOrEmpty_ClampsAndUsesNearestOffset()
    {
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(0, 2, 1, 1))
            ],
            [],
            3);

        map.VisualRowCount.ShouldBe(3);
        map.HitTest(new Point(0, 9)).ShouldBe(1);
        map.HitTest(new Point(0, -3)).ShouldBe(0);
        map.HitTest(new Point(3, 1)).ShouldBe(1);
        map.OffsetAtVisualColumn(9, 0).ShouldBe(1);
    }

    /// <summary>Verifies a captured source occurrence resolves only by identity, range, and text
    /// together, so a duplicated source or a mutated one never drifts to another occurrence.</summary>
    [Fact]
    public void ResolveSourceOccurrence_WhenIdentityRangeOrTextDiffers_ResolvesOnlyTheExactOccurrence()
    {
        var first = new ControlText("ab");
        var second = new ControlText("ab");
        var occurrenceA = new TextSelectionSource(first, null, new Selection(0, 2), "ab", 1);
        var occurrenceB = new TextSelectionSource(first, null, new Selection(2, 4), "ab", 1);
        var map = new TextSelectionMap("abab", [], [occurrenceA, occurrenceB], 1);

        map.ResolveSourceOccurrence(new TextSelectionSource(first, null, new Selection(2, 4), "ab", 9))
            .ShouldBeSameAs(occurrenceB);
        map.ResolveSourceOccurrence(new TextSelectionSource(first, null, new Selection(0, 2), "ab", 9))
            .ShouldBeSameAs(occurrenceA);
        map.ResolveSourceOccurrence(new TextSelectionSource(second, null, new Selection(0, 2), "ab", 1)).ShouldBeNull();
        map.ResolveSourceOccurrence(new TextSelectionSource(first, null, new Selection(0, 2), "xy", 1)).ShouldBeNull();
        map.ResolveSourceOccurrence(new TextSelectionSource(first, null, new Selection(1, 3), "ab", 1)).ShouldBeNull();
    }

    /// <summary>Verifies caret geometry without any glyph still reports the containing source by
    /// range, including the exclusive end of the last source.</summary>
    [Fact]
    public void TryGetCaretGeometry_WhenNoGlyphExists_ReportsTheContainingSourceOnly()
    {
        var owner = new ControlText("abcd");
        var head = new TextSelectionSource(owner, null, new Selection(0, 2), "ab", 1);
        var tail = new TextSelectionSource(owner, null, new Selection(2, 4), "cd", 1);
        var map = new TextSelectionMap("abcd", [], [head, tail], 0);

        map.TryGetCaretGeometry(1, out var bounds, out var source).ShouldBeFalse();
        bounds.ShouldBe(default);
        source.ShouldBeSameAs(head);
        map.TryGetCaretGeometry(2, out _, out source).ShouldBeFalse();
        source.ShouldBeSameAs(tail);
        map.TryGetCaretGeometry(4, out _, out source).ShouldBeFalse();
        source.ShouldBeSameAs(tail);
        map.TryGetVisualLineBoundary(3, end: true, out var boundary, out _, out source).ShouldBeFalse();
        boundary.ShouldBe(3);
        source.ShouldBeNull();
    }

    /// <summary>Verifies the visual line boundary of a row whose glyphs are spread out uses the
    /// full row extent and reports the selecting glyph's geometry and source.</summary>
    [Fact]
    public void TryGetVisualLineBoundary_WhenRowHasGlyphs_ReportsBoundaryGeometryAndSource()
    {
        var owner = new ControlText("ab");
        var source = new TextSelectionSource(owner, null, new Selection(0, 2), "ab", 1);
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(2, 0, 1, 1), source),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(6, 0, 1, 1), source)
            ],
            [source],
            1);

        map.TryGetVisualLineBoundary(1, end: false, out var start, out var startBounds, out var startSource).ShouldBeTrue();
        start.ShouldBe(0);
        startBounds.ShouldBe(new Rect(2, 0, 1, 1));
        startSource.ShouldBeSameAs(source);
        map.TryGetVisualLineBoundary(0, end: true, out var end, out var endBounds, out _).ShouldBeTrue();
        end.ShouldBe(2);
        endBounds.ShouldBe(new Rect(6, 0, 1, 1));
        map.TryGetVisualPosition(2, out var row, out var column).ShouldBeTrue();
        row.ShouldBe(0);
        column.ShouldBe(7);
    }

    /// <summary>Verifies the fingerprint changes when the source identity changes even though the
    /// text and range are identical, and stays stable for an equivalent rebuild.</summary>
    [Fact]
    public void ComputeFingerprint_WhenSourceIdentityChanges_DiffersDespiteIdenticalText()
    {
        var first = new ControlText("ab");
        var second = new ControlText("ab");
        var byFirst = new TextSelectionSource(first, null, new Selection(0, 2), "ab", 1);
        var byFirstAgain = new TextSelectionSource(first, null, new Selection(0, 2), "ab", 2);
        var bySecond = new TextSelectionSource(second, null, new Selection(0, 2), "ab", 1);

        TextSelectionMap.ComputeFingerprint("ab", [byFirst])
            .ShouldBe(TextSelectionMap.ComputeFingerprint("ab", [byFirstAgain]));
        TextSelectionMap.ComputeFingerprint("ab", [byFirst])
            .ShouldNotBe(TextSelectionMap.ComputeFingerprint("ab", [bySecond]));
        TextSelectionMap.ComputeFingerprint("ab", [])
            .ShouldNotBe(TextSelectionMap.ComputeFingerprint("ba", []));
    }
}
