// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates the cost of grapheme-boundary checks against document size using deterministic
/// operation counts instead of a wall-clock timing budget, which measures the host machine as much
/// as the product and is inherently flaky under CI load or coverage instrumentation.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class EditPerformanceTests
{
    /// <summary>Verifies rejecting a non-boundary index near the start of the document enumerates a
    /// bounded number of graphemes regardless of total document length. IsBoundary's success path
    /// already returns as soon as it finds a matching offset, so this only exercises the previously
    /// unbounded failure path: the offset never matches, so the old implementation always
    /// enumerated the whole source before returning false — Validate (called by every navigation
    /// and edit operation) paid for the entire document to reject an invalid endpoint near the
    /// document start.</summary>
    [Theory]
    [InlineData(100_000)]
    [InlineData(1_600_000)]
    public void IsBoundary_WhenRejectingANonBoundaryIndexNearTheStart_EnumeratesABoundedGraphemeCount(
        int documentLength)
    {
        // "e" + combining acute (U+0301) forms one grapheme cluster; index 1 splits it and is
        // never a boundary, regardless of how long the tail after it is.
        var text = "e\u0301" + new string('a', documentLength);

        var result = Edit.IsBoundaryCore(text, 1, out var iterations);

        result.ShouldBeFalse();

        // The candidate splits the first cluster, so a bounded scan sees only that cluster and
        // the very next one (whose offset finally exceeds the index) before the loop exits -
        // never the millions of clusters after it. A document 16x larger enumerating the same
        // tiny count (rather than a count that grows with document length) is exactly what the
        // early exit guarantees.
        iterations.ShouldBe(2);
    }

    /// <summary>Verifies an unlimited Replace call (maxLength = 0, the common case) skips the
    /// retained-length scan entirely, rather than merely running it fast. Both calls perform the
    /// same O(document) string.Concat copy; isolating the grapheme-count call count (instead of
    /// wall-clock time across document sizes) proves the "retained length" scan Replace used to run
    /// unconditionally is now skipped whenever maxLength is non-positive, since it is only ever
    /// consulted when maxLength is positive.</summary>
    [Fact]
    public void Replace_WhenMaxLengthIsUnbounded_SkipsTheRetainedLengthScanEntirely()
    {
        var text = new string('a', 1_600_000);
        var caret = new Selection(text.Length, text.Length);

        _ = Edit.ReplaceCore(text, caret, "x", maxLength: 0, acceptsReturn: false, acceptsTab: false,
            out var unboundedCalls);
        unboundedCalls.ShouldBe(0);

        _ = Edit.ReplaceCore(text, caret, "x", maxLength: int.MaxValue, acceptsReturn: false, acceptsTab: false,
            out var boundedCalls);
        boundedCalls.ShouldBe(2);
    }
}
