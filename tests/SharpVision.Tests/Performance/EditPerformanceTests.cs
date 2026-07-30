// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates the cost of grapheme-boundary checks against document size.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class EditPerformanceTests
{
    /// <summary>Verifies rejecting a non-boundary index near the start of the document costs
    /// roughly the same regardless of total document length. IsBoundary's success path already
    /// returns as soon as it finds a matching offset, so this only exercises the previously
    /// unbounded failure path: the offset never matches, so the old implementation always
    /// enumerated the whole source before returning false — Validate (called by every navigation
    /// and edit operation) paid for the entire document to reject an invalid endpoint near the
    /// document start (see #42).</summary>
    [Fact]
    public void IsBoundary_WhenRejectingANonBoundaryIndexNearTheStart_DoesNotScaleWithDocumentLength()
    {
        var small = Elapsed(documentLength: 100_000);
        var large = Elapsed(documentLength: 1_600_000);

        // Bounded to the candidate index, a 16x larger document should cost about the same to
        // reject near its start. A generous 6x budget comfortably clears measurement noise while
        // still rejecting the old whole-document scan, which grows close to 16x.
        large.TotalMilliseconds.ShouldBeLessThan((small.TotalMilliseconds * 6) + 5);
    }

    private static TimeSpan Elapsed(int documentLength)
    {
        // "e" + combining acute (U+0301) forms one grapheme cluster; index 1 splits it and is
        // never a boundary, regardless of how long the tail after it is.
        var text = "é" + new string('a', documentLength);
        var watch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < 200; iteration++)
        {
            _ = Edit.IsBoundary(text, 1);
        }

        watch.Stop();
        return watch.Elapsed;
    }
}
