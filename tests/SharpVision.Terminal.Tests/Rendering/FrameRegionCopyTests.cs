// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies the previous-frame region copy preserves grapheme ownership, frame compatibility, and
/// the finite text arena.
/// </summary>
/// <remarks>
/// The copy is the mechanism behind render-clean subtree reuse, so an invalid destination here
/// would surface much later as a corrupt frame rather than as a failed copy.
/// </remarks>
public sealed class FrameRegionCopyTests
{
    /// <summary>
    /// Verifies copying only the continuation column of a wide cluster blanks it instead of
    /// producing a continuation whose lead was never copied.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenRegionSplitsWideClusterContinuation_WritesBlank()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("\u4f60", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1));
        Attach(frame, previous);

        frame.Canvas.CopyFromPrevious(new Rect(1, 0, 3, 1));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBeEmpty();
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies copying only the lead column of a wide cluster blanks it instead of leaving a lead
    /// whose continuation column stayed outside the region.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenRegionSplitsWideClusterLead_WritesBlank()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("\u4f60", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1));
        Attach(frame, previous);

        frame.Canvas.CopyFromPrevious(new Rect(0, 0, 1, 1));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBeEmpty();
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBeEmpty();
    }

    /// <summary>Verifies a region containing the complete cluster copies it intact.</summary>
    [Fact]
    public void CopyFromPrevious_WhenRegionContainsCompleteCluster_CopiesIt()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("\u4f60", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1));
        Attach(frame, previous);

        frame.Canvas.CopyFromPrevious(new Rect(0, 0, 2, 1));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("\u4f60");
    }

    /// <summary>
    /// Verifies a mismatched previous frame is rejected before any destination cell changes, so a
    /// stride mismatch cannot silently copy the wrong row.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenGeometryDiffers_ThrowsWithoutMutating()
    {
        using var previous = new Frame(new Size(8, 2));
        _ = previous.Canvas.Draw("x", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw("a", new Point(0, 0));
        Attach(frame, previous);

        _ = Should.Throw<InvalidOperationException>(
            () => frame.Canvas.CopyFromPrevious(new Rect(0, 0, 4, 1)));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("a");
    }

    /// <summary>
    /// Verifies a copy that cannot fit the destination arena is rejected whole, instead of
    /// appending past the advertised bound one cell at a time.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenRegionExceedsTextArena_ThrowsWithoutMutating()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("\u00e9", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1), maxTextBytes: 1);
        Attach(frame, previous);

        _ = Should.Throw<InvalidOperationException>(
            () => frame.Canvas.CopyFromPrevious(new Rect(0, 0, 4, 1)));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBeEmpty();
    }

    /// <summary>Verifies repeated copies cannot grow the arena past the advertised bound.</summary>
    [Fact]
    public void CopyFromPrevious_WhenRepeated_NeverExceedsTextArena()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("\u00e9", new Point(0, 0));
        using var frame = new Frame(new Size(4, 1), maxTextBytes: 8);
        Attach(frame, previous);

        for (var iteration = 0; iteration < 8; iteration++)
        {
            try
            {
                frame.Canvas.CopyFromPrevious(new Rect(0, 0, 4, 1));
            }
            catch (InvalidOperationException)
            {
                // The arena is finite; refusing the copy is the contract under test.
            }
        }

        frame.TextLength.ShouldBeLessThanOrEqualTo(frame.MaxTextBytes);
    }

    private static void Attach(Frame frame, Frame previous) => frame.PreviousFrame = previous;
}
