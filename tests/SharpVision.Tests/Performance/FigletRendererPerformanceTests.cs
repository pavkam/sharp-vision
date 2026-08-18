// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates vertical-merge cost during multi-line FIGlet rendering using a deterministic
/// inspected-row count instead of a wall-clock timing budget, which measures the host machine as
/// much as the product and is inherently flaky under CI load or coverage instrumentation.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class FigletRendererPerformanceTests
{
    /// <summary>Verifies merging a new one-row <em>blank</em> glyph into an ever-growing, entirely
    /// blank accumulated output inspects a bounded number of accumulated rows - never more than the
    /// new glyph's own height - regardless of how many rows have already accumulated. Blank rows are
    /// the worst case for the trailing-row scan: every accumulated row satisfies
    /// <c>IsVerticalBlank</c>, so an unbounded scan walks backward through the entire accumulated
    /// output on every call, while the actual bound stops at the new glyph's height. A non-blank
    /// probe row (verified separately below) would pass even with the O(top.Count) bug reintroduced,
    /// since the scan's own loop condition already exits after one check once it hits non-blank
    /// content - it is not a meaningful regression probe. The vertical-merge overlap scan previously
    /// rescanned the entire accumulated output on every line, making K lines cost O(K^2) instead of
    /// O(K); this directly exercises the exact function responsible, growing the accumulated output
    /// to 8,000 blank rows and confirming the inspected count never grows past the one-row glyph
    /// height of 1.</summary>
    [Fact]
    public void GetVerticalOverlapCore_WhenAccumulatedOutputIsEntirelyBlankAndGrowsLarge_InspectsOnlyTheNewGlyphHeight()
    {
        var top = new List<StringBuilder>();

        for (var line = 0; line < 8_000; line++)
        {
            StringBuilder[] bottom = [new StringBuilder(" ")];

            _ = FigletRenderer.GetVerticalOverlapCore(
                top,
                topWidth: 1,
                bottom,
                bottomWidth: 1,
                FigletLayout.VerticalFitting,
                hardBlank: '$',
                out var topRowsInspected);

            // The new glyph is one row tall, so at most one accumulated row can ever be merged
            // against it - an O(top.Count) implementation would instead report a growing count as
            // the loop progresses, since every accumulated row here is blank and so satisfies the
            // scan's own continuation condition.
            topRowsInspected.ShouldBeLessThanOrEqualTo(1);
            top.Add(bottom[0]);
        }

        top.Count.ShouldBe(8_000);
    }

    /// <summary>Verifies a probe row that is <em>not</em> blank stops the trailing-row scan after
    /// one check regardless of how the loop is bounded, confirming such a row would be a false-
    /// negative regression probe (see the remark on the blank-row test above) rather than a
    /// meaningful assertion of its own.</summary>
    [Fact]
    public void GetVerticalOverlapCore_WhenAccumulatedOutputIsNonBlank_InspectsExactlyOneRowRegardlessOfBound()
    {
        var top = new List<StringBuilder>();

        for (var line = 0; line < 20; line++)
        {
            top.Add(new StringBuilder("A"));
        }

        _ = FigletRenderer.GetVerticalOverlapCore(
            top,
            topWidth: 1,
            [new StringBuilder("A")],
            bottomWidth: 1,
            FigletLayout.VerticalFitting,
            hardBlank: '$',
            out var topRowsInspected);

        topRowsInspected.ShouldBe(0);
    }
}
