// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies shade and quadrant block drawing.</summary>
public sealed class BlockResolverTests
{
    #region Shades

    /// <summary>Verifies every shade resolves to its exact Block Elements Rune.</summary>
    [Theory]
    [InlineData(Shade.Light, "░")]
    [InlineData(Shade.Medium, "▒")]
    [InlineData(Shade.Dark, "▓")]
    [InlineData(Shade.Solid, "█")]
    public void FillShade_WhenShadeIsSelected_WritesExactGlyph(Shade shade, string expected)
    {
        using Frame frame = new(new Size(2, 1));

        frame.Canvas.FillShade(new Rect(0, 0, 2, 1), shade);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(expected);
    }

    #endregion

    #region Quadrants

    /// <summary>Verifies every quadrant mask resolves to the exact Unicode Rune.</summary>
    [Theory]
    [MemberData(nameof(QuadrantCases))]
    public void DrawQuadrants_WhenMaskIsSelected_WritesExactGlyph(
        Quadrants quadrants,
        string expected)
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, quadrants);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
    }

    /// <summary>Verifies separate quadrant draws merge commutatively.</summary>
    [Fact]
    public void DrawQuadrants_WhenSeparateMasksShareCell_MergesBits()
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);
        frame.Canvas.DrawQuadrants(default, Quadrants.LowerRight);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▚");
    }

    /// <summary>
    /// Verifies the portable ASCII fallback glyph ('#'), which every non-empty quadrant mask
    /// resolves to under <see cref="Ambiguous.Wide"/>, round-trips through <c>TryDecode</c> as a
    /// conservative superset (<see cref="Quadrants.All"/>) instead of being silently treated as
    /// empty. A frame whose exact quadrant Rune has already been demoted to '#' by the wide
    /// policy can never recover which quadrants were actually filled, so <c>DrawQuadrants</c>'s
    /// documented "merges filled quadrants" contract can only be honored by over-including
    /// (mirroring how <c>LineResolver</c> decodes the ASCII line-cross fallback '+' to all four
    /// connections) rather than under-including.
    /// </summary>
    [Fact]
    public void TryDecode_WhenRuneIsThePortableAsciiFallback_DecodesAsAllQuadrants()
    {
        new Rune('#').TryDecode(out Quadrants quadrants).ShouldBeTrue();
        quadrants.ShouldBe(Quadrants.All);
    }

    /// <summary>
    /// Verifies a second quadrant draw sharing a cell with a first draw that was already demoted
    /// to the ASCII fallback (because the frame uses <see cref="Ambiguous.Wide"/>) still merges
    /// as the union of both requested masks rather than discarding the first draw's bits.
    /// </summary>
    [Fact]
    public void DrawQuadrants_WhenAmbiguousWidthIsWideAndMasksShareCell_MergesBits()
    {
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);
        frame.Canvas.DrawQuadrants(default, Quadrants.LowerRight);

        var bytes = frame.GetGrapheme(frame.GetIndex(default));
        Rune.DecodeFromUtf8(bytes, out var stored, out _).ShouldBe(OperationStatus.Done);
        stored.TryDecode(out Quadrants merged).ShouldBeTrue();
        merged.ShouldBe(Quadrants.All, "the union of UpperLeft and LowerRight must be recoverable, not just the last draw's mask");
    }

    /// <summary>Provides all non-empty quadrant masks and Unicode forms.</summary>
    public static TheoryData<Quadrants, string> QuadrantCases => new()
    {
        { Quadrants.UpperLeft, "▘" },
        { Quadrants.UpperRight, "▝" },
        { Quadrants.Upper, "▀" },
        { Quadrants.LowerLeft, "▖" },
        { Quadrants.UpperLeft | Quadrants.LowerLeft, "▌" },
        { Quadrants.UpperRight | Quadrants.LowerLeft, "▞" },
        { Quadrants.Upper | Quadrants.LowerLeft, "▛" },
        { Quadrants.LowerRight, "▗" },
        { Quadrants.UpperLeft | Quadrants.LowerRight, "▚" },
        { Quadrants.UpperRight | Quadrants.LowerRight, "▐" },
        { Quadrants.Upper | Quadrants.LowerRight, "▜" },
        { Quadrants.Lower, "▄" },
        { Quadrants.UpperLeft | Quadrants.Lower, "▙" },
        { Quadrants.UpperRight | Quadrants.Lower, "▟" },
        { Quadrants.All, "█" }
    };

    #endregion

    #region Fractional bars

    /// <summary>Verifies a rightward bar draws full cells and the exact left-eighth cap.</summary>
    [Fact]
    public void DrawBar_WhenGrowingRight_WritesFullCellsAndExactCap()
    {
        using Frame frame = new(new Size(4, 1));

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Right, eighths: 19);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("▍");
        FrameTests.GetText(frame, new Point(3, 0)).ShouldBe("");
    }

    /// <summary>Verifies an upward bar draws full cells and the exact lower-eighth cap.</summary>
    [Fact]
    public void DrawBar_WhenGrowingUp_WritesFullCellsAndExactCap()
    {
        using Frame frame = new(new Size(1, 4));

        frame.Canvas.DrawBar(new Point(0, 3), BarDirection.Up, eighths: 21);

        FrameTests.GetText(frame, new Point(0, 3)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(0, 2)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("▅");
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("");
    }

    /// <summary>Verifies every exact upward cap glyph, one eighth at a time.</summary>
    [Theory]
    [InlineData(1, "▁")]
    [InlineData(2, "▂")]
    [InlineData(3, "▃")]
    [InlineData(4, "▄")]
    [InlineData(5, "▅")]
    [InlineData(6, "▆")]
    [InlineData(7, "▇")]
    public void DrawBar_WhenGrowingUpByEighths_WritesEveryLowerBlock(int eighths, string expected)
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Up, eighths);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
    }

    /// <summary>Verifies every exact rightward cap glyph, one eighth at a time.</summary>
    [Theory]
    [InlineData(1, "▏")]
    [InlineData(2, "▎")]
    [InlineData(3, "▍")]
    [InlineData(4, "▌")]
    [InlineData(5, "▋")]
    [InlineData(6, "▊")]
    [InlineData(7, "▉")]
    public void DrawBar_WhenGrowingRightByEighths_WritesEveryLeftBlock(int eighths, string expected)
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Right, eighths);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
    }

    /// <summary>Verifies a downward cap snaps to the half block Block Elements actually defines.
    /// Rounding must engage the upper-half glyph from two eighths up and drop back to nothing
    /// below, since no upper one- or three-eighth block exists to draw.</summary>
    [Theory]
    [InlineData(9, "")]
    [InlineData(10, "▀")]
    [InlineData(12, "▀")]
    [InlineData(14, "█")]
    public void DrawBar_WhenGrowingDown_SnapsTheCapToHalfCells(int eighths, string expectedCap)
    {
        using Frame frame = new(new Size(1, 2));

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Down, eighths);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe(expectedCap);
    }

    /// <summary>The same for a leftward bar, whose only partial glyph is the right half block.</summary>
    [Fact]
    public void DrawBar_WhenGrowingLeft_SnapsTheCapToHalfCells()
    {
        using Frame frame = new(new Size(2, 1));

        frame.Canvas.DrawBar(new Point(1, 0), BarDirection.Left, eighths: 12);

        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("█");
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▐");
    }

    /// <summary>Verifies the wide Ambiguous policy degrades the bar to the portable solid
    /// fallback, rounding the cap to a whole cell.</summary>
    [Theory]
    [InlineData(12, "#", "#")]
    [InlineData(11, "#", "")]
    public void DrawBar_WhenPolicyIsWide_FallsBackToWholeCells(int eighths, string first, string second)
    {
        using Frame frame = new(new Size(2, 1), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Right, eighths);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(first);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(second);
    }

    /// <summary>Verifies a zero extent draws nothing at all.</summary>
    [Fact]
    public void DrawBar_WhenExtentIsZero_DrawsNothing()
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Up, eighths: 0);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("");
    }

    /// <summary>Verifies invalid arguments are rejected by name.</summary>
    [Fact]
    public void DrawBar_WhenArgumentsAreInvalid_Throws()
    {
        using Frame frame = new(new Size(1, 1));

        Should.Throw<ArgumentOutOfRangeException>(() =>
                frame.Canvas.DrawBar(new Point(0, 0), (BarDirection) 99, eighths: 1))
            .ParamName.ShouldBe("direction");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                frame.Canvas.DrawBar(new Point(0, 0), BarDirection.Up, eighths: -1))
            .ParamName.ShouldBe("eighths");
    }

    #endregion

    #region Quadrant lines

    /// <summary>Verifies a horizontal half-cell line occupies only the upper quadrant row.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenHorizontalAlongUpperHalves_WritesUpperBlocks()
    {
        using Frame frame = new(new Size(4, 1));

        frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0));

        for (var x = 0; x < 4; x++)
        {
            FrameTests.GetText(frame, new Point(x, 0)).ShouldBe("▀");
        }
    }

    /// <summary>Verifies a diagonal descends through paired quadrants rather than whole cells.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenDiagonal_WritesPairedQuadrants()
    {
        using Frame frame = new(new Size(2, 2));

        frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(3, 3));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▚");
        FrameTests.GetText(frame, new Point(1, 1)).ShouldBe("▚");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("");
    }

    /// <summary>Verifies two lines through one cell merge into the union glyph instead of the
    /// second overwriting the first.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenLinesShareCells_MergesQuadrants()
    {
        using Frame frame = new(new Size(4, 1));

        frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0));
        frame.Canvas.DrawQuadrantLine(new Point(0, 1), new Point(7, 1));

        for (var x = 0; x < 4; x++)
        {
            FrameTests.GetText(frame, new Point(x, 0)).ShouldBe("█");
        }
    }

    /// <summary>Verifies a segment reaching outside the frame clips per cell instead of folding
    /// negative half-cell coordinates onto cell zero.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenPartiallyOffFrame_ClipsWithoutFolding()
    {
        using Frame frame = new(new Size(2, 1));

        frame.Canvas.DrawQuadrantLine(new Point(-4, 0), new Point(3, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▀");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("▀");
    }

    #endregion
}
