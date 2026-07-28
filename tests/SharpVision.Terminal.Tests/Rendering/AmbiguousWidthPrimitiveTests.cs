// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies physical-cell primitives honor the frame ambiguous-width policy.</summary>
public sealed class AmbiguousWidthPrimitiveTests
{
    /// <summary>Verifies a direct one-cell Rune write rejects a Rune that is wide for this frame.</summary>
    [Fact]
    public void DrawRune_WhenAmbiguousRuneIsWideForFrame_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(2, 1), ambiguousWidth: Ambiguous.Wide);

        _ = Should.Throw<ArgumentException>(() =>
            frame.Canvas.DrawRune(new Rune('·'), default));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies Unicode line topology degrades to portable ASCII in a wide frame.</summary>
    [Fact]
    public void DrawBox_WhenAmbiguousWidthIsWide_WritesSingleCellAsciiTopology()
    {
        using Frame frame = new(new Size(3, 3), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.DrawBox(frame.Canvas.Bounds, LineStyle.Light);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("+");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("-");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("+");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("|");
    }

    /// <summary>Verifies shade fills retain one physical cell per requested destination.</summary>
    [Fact]
    public void FillShade_WhenAmbiguousWidthIsWide_WritesPortableAsciiShade()
    {
        using Frame frame = new(new Size(2, 1), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.FillShade(frame.Canvas.Bounds, Shade.Medium);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(":");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(":");
    }

    /// <summary>Verifies quadrant drawing uses a one-cell portable representation.</summary>
    [Fact]
    public void DrawQuadrants_WhenAmbiguousWidthIsWide_WritesPortableAsciiBlock()
    {
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);

        FrameTests.GetText(frame, default).ShouldBe("#");
    }
}
