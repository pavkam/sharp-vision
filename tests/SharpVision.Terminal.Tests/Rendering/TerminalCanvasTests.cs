// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies grapheme-safe canvas drawing, clipping, wrapping, and repair; validated Rune, fill,
/// and grapheme-preserving style primitives; overflow-safe arbitrary geometry at extreme
/// coordinates; ambiguous-width physical-cell primitives; and previous-frame region copy.
/// </summary>
public sealed class TerminalCanvasTests
{
    /// <summary>Verifies a base-less cluster cannot alter its preceding cell.</summary>
    /// <param name="value">The base-less source cluster.</param>
    [Theory]
    [InlineData("\u0301")]
    [InlineData("\u0903")]
    [InlineData("\u0600")]
    [InlineData("\u200d")]
    [InlineData("\ufe0f")]
    [InlineData("🏽")]
    [InlineData("\U000e0067")]
    public void Draw_WhenClusterHasNoBase_StoresIndependentReplacement(
        string value)
    {
        // Arrange
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("a".AsSpan(), new Point(0, 0));

        // Act
        var result = frame.Canvas.Draw(value.AsSpan(), new Point(1, 0));

        // Assert
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("a");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("�");
        result.Final.ShouldBe(new Point(2, 0));
        result.Cells.ShouldBe(1);
        result.Replaced.ShouldBe(1);
    }

    /// <summary>Verifies transparent drawing preserves the destination background while replacing text semantics.</summary>
    [Fact]
    public void Draw_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(1, 1));
        var surface = new CellStyle(ReferenceColors.Get(255), ReferenceColors.Get(238));
        var text = new CellStyle(
            ReferenceColors.Get(45),
            Color.Default,
            TerminalAttributes.Bold | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: ReferenceColors.Get(220));
        frame.Canvas.Fill(new Rect(0, 0, 1, 1), new Rune(' '), surface);

        _ = frame.Canvas.Draw("X".AsSpan(), new Point(0, 0), text, background: BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(
            ReferenceColors.Get(45),
            ReferenceColors.Get(238),
            TerminalAttributes.Bold | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: ReferenceColors.Get(220)));
    }

    /// <summary>Verifies a transparent style background requests composition without a separate mode.</summary>
    [Fact]
    public void Draw_WhenStyleBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.Fill(
            new Rect(0, 0, 1, 1),
            new Rune(' '),
            new CellStyle(ReferenceColors.Get(15), ReferenceColors.Get(4)));

        _ = frame.Canvas.Draw(
            "X".AsSpan(),
            default,
            new CellStyle(ReferenceColors.Get(14), Color.Transparent));

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(ReferenceColors.Get(14), ReferenceColors.Get(4)));
    }

    /// <summary>
    /// Verifies the frame's explicit ambiguous-width policy reaches drawing.
    /// </summary>
    [Fact]
    public void Draw_WhenAmbiguousPolicyIsWide_OwnsTwoCells()
    {
        using Frame frame = new(new Size(2, 1), ambiguousWidth: Ambiguous.Wide);

        _ = frame.Canvas.Draw("·".AsSpan(), new Point(0, 0));

        frame.GetCell(new Point(0, 0)).Width.ShouldBe(2);
        frame.GetCell(new Point(1, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies narrow and wide graphemes create semantic lead/continuation cells.
    /// </summary>
    [Fact]
    public void Draw_WhenTextContainsNarrowAndWideClusters_AssignsCellOwnership()
    {
        using Frame frame = new(new Size(4, 1));

        var result = frame.Canvas.Draw("A界".AsSpan(), new Point(0, 0));

        result.Final.ShouldBe(new Point(3, 0));
        result.Graphemes.ShouldBe(2);
        result.Cells.ShouldBe(3);
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("A");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).Continuation.ShouldBeTrue();
        frame.GetCell(new Point(2, 0)).Lead.ShouldBe(new Point(1, 0));
    }

    /// <summary>
    /// Verifies overwriting a continuation repairs the complete previous glyph.
    /// </summary>
    [Fact]
    public void Draw_WhenContinuationIsOverwritten_RepairsWholeWideCluster()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        _ = frame.Canvas.Draw("x".AsSpan(), new Point(1, 0));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).Continuation.ShouldBeFalse();
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("x");
    }

    /// <summary>
    /// Verifies overwriting a wide lead clears its stale continuation.
    /// </summary>
    [Fact]
    public void Draw_WhenWideLeadIsOverwritten_RepairsContinuation()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        _ = frame.Canvas.Draw("x".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("x");
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies clearing either occupied cell expands to the full glyph.
    /// </summary>
    [Fact]
    public void Clear_WhenRegionTouchesContinuation_RepairsWholeWideCluster()
    {
        using Frame frame = new(new Size(3, 1));
        _ = frame.Canvas.Draw("界z".AsSpan(), new Point(0, 0));

        frame.Canvas.Clear(new Rect(1, 0, 1, 1));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("z");
    }

    /// <summary>
    /// Verifies a wide edge cluster can be clipped without half output.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsClip_SkipsWholeCluster()
    {
        using Frame frame = new(new Size(2, 1));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Clip);

        result.Clipped.ShouldBe(1);
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("a");
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies a wide edge cluster wraps as one glyph.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsWrap_MovesWholeCluster()
    {
        using Frame frame = new(new Size(2, 2));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Wrap);

        result.Final.ShouldBe(new Point(2, 1));
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("界");
        frame.GetCell(new Point(1, 1)).Continuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a wide edge cluster can be replaced with one visible cell.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsReplace_WritesReplacement()
    {
        using Frame frame = new(new Size(2, 1));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Replace);

        result.Replaced.ShouldBe(1);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("�");
        frame.GetCell(new Point(1, 0)).Width.ShouldBe(1);
    }

    /// <summary>
    /// Verifies <see cref="DrawResult.Cells"/> counts the logical cell advance actually applied
    /// (matching <see cref="DrawResult.Final"/>), not the pre-demotion width of a wide cluster
    /// that <see cref="Edge.Replace"/> narrowed to a single-cell replacement glyph.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsReplace_CellsMatchesFinalAdvance()
    {
        using Frame frame = new(new Size(2, 1));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Replace);

        result.Final.ShouldBe(new Point(2, 0));
        result.Cells.ShouldBe(2, "'a' advances 1 cell and the replaced wide cluster advances only the 1 cell it was demoted to");
    }

    /// <summary>
    /// Verifies a wide edge cluster wraps relative to the canvas's own clip, not the frame,
    /// when the clip is narrower than the frame.
    /// </summary>
    [Fact]
    public void Draw_WhenClipIsNarrowerThanFrameAndPolicyIsWrap_WrapsInsideClip()
    {
        using Frame frame = new(new Size(6, 2));
        var canvas = frame.Canvas.Clip(new Rect(0, 0, 2, 2));

        var result = canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Wrap);

        result.Clipped.ShouldBe(0);
        result.Final.ShouldBe(new Point(2, 1));
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("界");
        frame.GetCell(new Point(1, 1)).Continuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a wide edge cluster is replaced inside the canvas's own clip, not silently
    /// dropped, when the clip is narrower than the frame.
    /// </summary>
    [Fact]
    public void Draw_WhenClipIsNarrowerThanFrameAndPolicyIsReplace_ReplacesInsideClip()
    {
        using Frame frame = new(new Size(6, 1));
        var canvas = frame.Canvas.Clip(new Rect(0, 0, 2, 1));

        var result = canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Replace);

        result.Replaced.ShouldBe(1);
        result.Clipped.ShouldBe(0);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("�");
        frame.GetCell(new Point(1, 0)).Width.ShouldBe(1);
    }

    /// <summary>
    /// Verifies <see cref="Edge.Wrap"/> targets the canvas's own clip left edge, not frame-absolute
    /// column zero, when the clip's right edge coincides with the frame's right edge but starts at
    /// a nonzero column.
    /// </summary>
    [Fact]
    public void Draw_WhenClipHasNonzeroXAndPolicyIsWrap_WrapsToClipLeftEdge()
    {
        using Frame frame = new(new Size(6, 2));
        var canvas = frame.Canvas.Clip(new Rect(2, 0, 4, 2));

        var result = canvas.Draw("界".AsSpan(), new Point(5, 0), edge: Edge.Wrap);

        result.Clipped.ShouldBe(0);
        result.Final.ShouldBe(new Point(4, 1));
        FrameTests.GetText(frame, new Point(2, 1)).ShouldBe("界");
        frame.GetCell(new Point(3, 1)).Continuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies combining and ZWJ text is stored once as complete UTF-8.
    /// </summary>
    [Fact]
    public void Draw_WhenClusterHasMultipleRunes_StoresCompleteGrapheme()
    {
        using Frame frame = new(new Size(3, 1));

        _ = frame.Canvas.Draw("e\u0301👩‍💻".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("e\u0301");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("👩‍💻");
        frame.GetCell(new Point(2, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies invalid UTF-16 is stored as one replacement-cell payload.
    /// </summary>
    [Fact]
    public void Draw_WhenUtf16IsInvalid_StoresReplacementRune()
    {
        using Frame frame = new(new Size(1, 1));

        _ = frame.Canvas.Draw("\ud800".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("�");
        frame.GetCell(new Point(0, 0)).Width.ShouldBe(1);
    }

    /// <summary>
    /// Verifies arena-limit validation happens before any cell mutation.
    /// </summary>
    [Fact]
    public void Draw_WhenTextExceedsArenaLimit_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(2, 1), maxTextBytes: 1);

        _ = Should.Throw<InvalidOperationException>(() => frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0)));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies child clips never draw outside their intersection.
    /// </summary>
    [Fact]
    public void Clip_WhenDrawingCrossesIntersection_PreservesOutsideCells()
    {
        using Frame frame = new(new Size(4, 1));
        var canvas = frame.Canvas.Clip(new Rect(1, 0, 2, 1));

        _ = canvas.Draw("abcd".AsSpan(), new Point(0, 0));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("b");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("c");
        frame.GetCell(new Point(3, 0)).ShouldBe(CellInfo.Blank);
    }

    #region Line and column control clusters

    /// <summary>Verifies "\n" moves to the next row at the line's origin column.</summary>
    [Fact]
    public void Draw_WhenTextContainsLineFeed_MovesToNextRowAtOrigin()
    {
        using Frame frame = new(new Size(4, 2));

        var result = frame.Canvas.Draw("ab\ncd".AsSpan(), new Point(1, 0));

        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("a");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("b");
        FrameTests.GetText(frame, new Point(1, 1)).ShouldBe("c");
        FrameTests.GetText(frame, new Point(2, 1)).ShouldBe("d");
        result.Final.ShouldBe(new Point(3, 1));
    }

    /// <summary>Verifies "\r\n" moves to the next row at the line's origin column, same as "\n" alone.</summary>
    [Fact]
    public void Draw_WhenTextContainsCarriageReturnLineFeed_MovesToNextRowAtOrigin()
    {
        using Frame frame = new(new Size(4, 2));

        var result = frame.Canvas.Draw("ab\r\ncd".AsSpan(), new Point(1, 0));

        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("a");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("b");
        FrameTests.GetText(frame, new Point(1, 1)).ShouldBe("c");
        FrameTests.GetText(frame, new Point(2, 1)).ShouldBe("d");
        result.Final.ShouldBe(new Point(3, 1));
    }

    /// <summary>
    /// Verifies a lone "\r" (not followed by "\n", so grapheme segmentation (GB3) keeps it as its
    /// own cluster) returns to the line's origin column on the same row, matching canonical
    /// terminal carriage-return semantics, instead of also advancing to the next row like "\n".
    /// </summary>
    [Fact]
    public void Draw_WhenTextContainsLoneCarriageReturn_ReturnsToOriginOnSameRow()
    {
        using Frame frame = new(new Size(4, 2));

        var result = frame.Canvas.Draw("ab\rcd".AsSpan(), new Point(1, 0));

        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("c");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("d");
        frame.GetCell(new Point(1, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(2, 1)).ShouldBe(CellInfo.Blank);
        result.Final.ShouldBe(new Point(3, 0));
    }

    #endregion

    #region Arbitrary geometry

    /// <summary>Verifies every line octant includes both caller-supplied endpoints.</summary>
    [Theory]
    [InlineData(0, 0, 5, 3)]
    [InlineData(5, 3, 0, 0)]
    [InlineData(0, 3, 5, 0)]
    [InlineData(3, 0, 1, 3)]
    public void DrawLine_WhenEndpointsVary_RasterizesBothEndpoints(
        int x1,
        int y1,
        int x2,
        int y2)
    {
        using Frame frame = new(new Size(6, 4));

        frame.Canvas.DrawLine(new Point(x1, y1), new Point(x2, y2), new Rune('*'));

        FrameTests.GetText(frame, new Point(x1, y1)).ShouldBe("*");
        FrameTests.GetText(frame, new Point(x2, y2)).ShouldBe("*");
    }

    /// <summary>Verifies a shallow diagonal follows exact deterministic Bresenham cells.</summary>
    [Fact]
    public void DrawLine_WhenSlopeIsShallow_RasterizesExactCells()
    {
        using Frame frame = new(new Size(6, 4));

        frame.Canvas.DrawLine(default, new Point(5, 3), new Rune('*'));

        AssertRows(frame, "*     ", " **   ", "   ** ", "     *");
    }

    /// <summary>Verifies clipped line traversal paints only visible cells.</summary>
    [Fact]
    public void DrawLine_WhenEndpointsCrossClip_PaintsVisibleIntersection()
    {
        using Frame frame = new(new Size(6, 3));
        var canvas = frame.Canvas.Clip(new Rect(1, 1, 4, 1));

        canvas.DrawLine(new Point(-2, 1), new Point(7, 1), new Rune('-'));

        AssertRows(frame, "      ", " ---- ", "      ");
    }

    /// <summary>Verifies odd ellipse bounds rasterize one symmetric outline.</summary>
    [Fact]
    public void DrawEllipse_WhenBoundsAreOdd_RasterizesExactOutline()
    {
        using Frame frame = new(new Size(7, 5));

        frame.Canvas.DrawEllipse(frame.Canvas.Bounds, new Rune('*'));

        AssertRows(frame, "  ***  ", " *   * ", "*     *", " *   * ", "  ***  ");
    }

    /// <summary>Verifies even ellipse bounds rasterize one symmetric outline.</summary>
    [Fact]
    public void DrawEllipse_WhenBoundsAreEven_RasterizesExactOutline()
    {
        using Frame frame = new(new Size(6, 4));

        frame.Canvas.DrawEllipse(frame.Canvas.Bounds, new Rune('*'));

        AssertRows(frame, " **** ", "*    *", "*    *", " **** ");
    }

    /// <summary>Verifies ellipse points outside a child canvas are skipped.</summary>
    [Fact]
    public void DrawEllipse_WhenCanvasIsClipped_PaintsOnlyVisibleOutline()
    {
        using Frame frame = new(new Size(5, 3));
        var canvas = frame.Canvas.Clip(new Rect(2, 0, 2, 3));

        canvas.DrawEllipse(frame.Canvas.Bounds, new Rune('*'));

        AssertRows(frame, "  ** ", "     ", "  ** ");
    }

    /// <summary>Verifies one-cell ellipse axes degrade to deterministic lines and points.</summary>
    [Fact]
    public void DrawEllipse_WhenOneAxisIsOneCell_DrawsDegenerateGeometry()
    {
        using Frame frame = new(new Size(5, 5));

        frame.Canvas.DrawEllipse(new Rect(2, 0, 1, 5), new Rune('|'));
        frame.Canvas.DrawEllipse(new Rect(0, 2, 5, 1), new Rune('-'));

        AssertRows(frame, "  |  ", "  |  ", "-----", "  |  ", "  |  ");
    }

    /// <summary>Verifies zero and positive radii use cell-coordinate circle geometry.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void DrawCircle_WhenRadiusIsValid_PaintsCardinalCells(int radius)
    {
        using Frame frame = new(new Size(9, 9));
        var center = new Point(4, 4);

        frame.Canvas.DrawCircle(center, radius, new Rune('o'));

        FrameTests.GetText(frame, new Point(center.X, center.Y - radius)).ShouldBe("o");
        FrameTests.GetText(frame, new Point(center.X + radius, center.Y)).ShouldBe("o");
        FrameTests.GetText(frame, new Point(center.X, center.Y + radius)).ShouldBe("o");
        FrameTests.GetText(frame, new Point(center.X - radius, center.Y)).ShouldBe("o");
    }

    /// <summary>Verifies identical geometry calls produce identical frame cells.</summary>
    [Fact]
    public void DrawEllipse_WhenRepeated_ProducesIdenticalCells()
    {
        using Frame first = new(new Size(8, 6));
        using Frame second = new(new Size(8, 6));

        first.Canvas.DrawEllipse(new Rect(1, 1, 6, 4), new Rune('#'));
        second.Canvas.DrawEllipse(new Rect(1, 1, 6, 4), new Rune('#'));

        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                first.GetCell(new Point(x, y)).ShouldBe(second.GetCell(new Point(x, y)));
            }
        }
    }

    /// <summary>Verifies invalid geometry arguments fail before changing the frame.</summary>
    [Fact]
    public void DrawGeometry_WhenInputIsInvalid_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(3, 3));

        _ = Should.Throw<ArgumentException>(() =>
            frame.Canvas.DrawLine(default, new Point(2, 2), new Rune('界')));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            frame.Canvas.DrawCircle(new Point(1, 1), -1, new Rune('*')));

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                frame.GetCell(new Point(x, y)).ShouldBe(CellInfo.Blank);
            }
        }
    }

    #endregion

    #region Rune drawing

    /// <summary>Verifies drawing rejects a wide Rune before changing the frame.</summary>
    [Fact]
    public void DrawRune_WhenRuneIsWide_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(2, 1));

        _ = Should.Throw<ArgumentException>(() =>
            frame.Canvas.DrawRune(new Rune('界'), default));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies one narrow Rune is drawn with the requested style.</summary>
    [Fact]
    public void DrawRune_WhenRuneIsNarrow_WritesExactCell()
    {
        using Frame frame = new(new Size(1, 1));
        var style = new CellStyle(ReferenceColors.Get(2), ReferenceColors.Get(4));

        frame.Canvas.DrawRune(new Rune('┼'), default, style);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("┼");
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(style);
    }

    /// <summary>Verifies transparent single-glyph drawing preserves the destination background.</summary>
    [Fact]
    public void DrawRune_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(1, 1));
        var surface = new CellStyle(ReferenceColors.Get(255), ReferenceColors.Get(238));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), surface);

        frame.Canvas.DrawRune(
            new Rune('│'),
            default,
            new CellStyle(ReferenceColors.Get(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(ReferenceColors.Get(45), ReferenceColors.Get(238)));
    }

    #endregion

    #region Region operations

    /// <summary>Verifies fill honors the intersection of region and canvas clip.</summary>
    [Fact]
    public void Fill_WhenRegionIsClipped_WritesOnlyIntersection()
    {
        using Frame frame = new(new Size(4, 1));
        var canvas = frame.Canvas.Clip(new Rect(1, 0, 2, 1));

        canvas.Fill(new Rect(0, 0, 4, 1), new Rune('x'));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("x");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("x");
        frame.GetCell(new Point(3, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies style mutation preserves and consistently styles a wide owner.</summary>
    [Fact]
    public void ApplyStyle_WhenRegionTouchesWideGlyph_StylesCompleteOwner()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界", default);
        var style = new CellStyle(background: ReferenceColors.Get(5));

        frame.Canvas.ApplyStyle(new Rect(1, 0, 1, 1), style);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("界");
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(style);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(style);
    }

    /// <summary>Verifies transparent style overlays preserve destination backgrounds.</summary>
    [Fact]
    public void ApplyStyle_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new CellStyle(ReferenceColors.Get(255), ReferenceColors.Get(238)));

        frame.Canvas.ApplyStyle(
            frame.Canvas.Bounds,
            new CellStyle(ReferenceColors.Get(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(ReferenceColors.Get(45), ReferenceColors.Get(238)));
    }

    /// <summary>Verifies a clipped partial wide owner is not half-restyled.</summary>
    [Fact]
    public void ApplyStyle_WhenClipExcludesWideLead_SkipsCompleteOwner()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界", default);
        var style = new CellStyle(background: ReferenceColors.Get(5));

        frame.Canvas.Clip(new Rect(1, 0, 1, 1)).ApplyStyle(new Rect(1, 0, 1, 1), style);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(CellStyle.Default);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(CellStyle.Default);
    }

    /// <summary>Verifies foreground transforms preserve rich owner semantics and visit leads row-major.</summary>
    [Fact]
    public void ApplyForeground_WhenStoredOwnersAreRich_PreservesSemanticContentAndVisitsLeadsOnce()
    {
        using Frame frame = new(new Size(4, 1));
        var original = new CellStyle(
            ReferenceColors.Get(1),
            ReferenceColors.Get(2),
            TerminalAttributes.Bold,
            "https://example.test/prism",
            Underline.Curly,
            ReferenceColors.Get(3));
        _ = frame.Canvas.Draw("A 界", default, original);
        var visited = new List<Point>();

        frame.Canvas.ApplyForeground(
            frame.Canvas.Bounds,
            point =>
            {
                visited.Add(point);
                return Color.Rgb(point.X * 20, 40, 60);
            });

        visited.ShouldBe([new Point(0, 0), new Point(1, 0), new Point(2, 0)]);
        AssertPreserved(original, frame.GetCell(new Point(0, 0)).Style, Color.Rgb(0, 40, 60));
        AssertPreserved(original, frame.GetCell(new Point(1, 0)).Style, Color.Rgb(20, 40, 60));
        AssertPreserved(original, frame.GetCell(new Point(2, 0)).Style, Color.Rgb(40, 40, 60));
        AssertPreserved(original, frame.GetCell(new Point(3, 0)).Style, Color.Rgb(40, 40, 60));
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("A");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(" ");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("界");
        var continuation = frame.GetCell(new Point(3, 0));
        continuation.Continuation.ShouldBeTrue();
        continuation.Lead.ShouldBe(new Point(2, 0));
    }

    /// <summary>Verifies a clipped wide owner remains unchanged without invoking the selector.</summary>
    [Fact]
    public void ApplyForeground_WhenClipExcludesWideLead_SkipsOwnerAndSelector()
    {
        using Frame frame = new(new Size(2, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("界", default, original);
        var calls = 0;

        frame.Canvas.Clip(new Rect(1, 0, 1, 1)).ApplyForeground(
            frame.Canvas.Bounds,
            _ =>
            {
                calls++;
                return ReferenceColors.Get(9);
            });

        calls.ShouldBe(0);
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(original);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
        frame.GetCell(new Point(1, 0)).Lead.ShouldBe(new Point(0, 0));
    }

    /// <summary>Verifies a null foreground selector fails before any stored cell changes.</summary>
    [Fact]
    public void ApplyForeground_WhenSelectorIsNull_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(1, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        frame.Canvas.DrawRune(new Rune('A'), default, original);

        _ = Should.Throw<ArgumentNullException>(() =>
            frame.Canvas.ApplyForeground(frame.Canvas.Bounds, null!));

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(original);
        FrameTests.GetText(frame, default).ShouldBe("A");
    }

    /// <summary>Verifies selector failures preserve the failing owner and wide-cell links.</summary>
    [Fact]
    public void ApplyForeground_WhenSelectorThrows_PropagatesIdentityAndPreservesRemainingOwners()
    {
        using Frame frame = new(new Size(3, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("A界", default, original);
        var failure = new InvalidOperationException("selector failed");

        var thrown = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.ApplyForeground(
                frame.Canvas.Bounds,
                point => point.X == 1 ? throw failure : ReferenceColors.Get(9)));

        thrown.ShouldBeSameAs(failure);
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(
            new CellStyle(ReferenceColors.Get(9), original.Background));
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
        var continuation = frame.GetCell(new Point(2, 0));
        continuation.Style.ShouldBe(original);
        continuation.Continuation.ShouldBeTrue();
        continuation.Lead.ShouldBe(new Point(1, 0));
    }

    /// <summary>Verifies stored spaces transform while untouched blanks do not invoke the selector.</summary>
    [Fact]
    public void ApplyForeground_WhenRegionContainsStoredSpaceAndBlank_TransformsOnlyStoredSpace()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw(" ", default, new CellStyle(ReferenceColors.Get(1)));
        var visited = new List<Point>();

        frame.Canvas.ApplyForeground(
            frame.Canvas.Bounds,
            point =>
            {
                visited.Add(point);
                return ReferenceColors.Get(7);
            });

        visited.ShouldBe([default]);
        frame.GetCell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(7));
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies whole-style composition preserves wide ownership, glyph storage, and retained hyperlink identity.</summary>
    /// <param name="selectedX">The lead or continuation cell selected by the requested region.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ApplyCellStyle_WhenRegionTouchesLinkedWideOwner_TransformsCompleteOwner(int selectedX)
    {
        // Arrange
        using Frame frame = new(new Size(2, 1));
        var hyperlink = new string("https://example.test/wide".ToCharArray());
        var original = new CellStyle(
            ReferenceColors.Get(1),
            ReferenceColors.Get(2),
            TerminalAttributes.Bold,
            hyperlink,
            Underline.Curly,
            ReferenceColors.Get(3));
        var replacement = new CellStyle(
            ReferenceColors.Get(9),
            ReferenceColors.Get(10),
            TerminalAttributes.Italic | TerminalAttributes.Overline,
            hyperlink,
            Underline.Dotted,
            ReferenceColors.Get(11));
        _ = frame.Canvas.Draw("界", default, original);

        // Act
        frame.Canvas.ApplyCellStyle(new Rect(selectedX, 0, 1, 1), (_, _) => replacement);

        // Assert
        FrameTests.GetText(frame, default).ShouldBe("界");
        frame.GetCell(default).Style.ShouldBe(replacement);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(replacement);
        frame.GetCell(default).Style.Hyperlink.ShouldBeSameAs(hyperlink);
        frame.GetCell(new Point(1, 0)).Style.Hyperlink.ShouldBeSameAs(hyperlink);
        frame.GetCell(new Point(1, 0)).Continuation.ShouldBeTrue();
        frame.GetCell(new Point(1, 0)).Lead.ShouldBe(default);
    }

    /// <summary>Verifies the returned style replaces the hyperlink across a complete wide owner.</summary>
    [Fact]
    public void ApplyCellStyle_WhenSelectorReplacesHyperlink_AppliesReturnedHyperlinkToCompleteOwner()
    {
        // Arrange
        using Frame frame = new(new Size(2, 1));
        var originalHyperlink = new string("https://example.test/original".ToCharArray());
        var replacementHyperlink = new string("https://example.test/replacement".ToCharArray());
        var original = new CellStyle(hyperlink: originalHyperlink);
        var replacement = new CellStyle(
            ReferenceColors.Get(9),
            ReferenceColors.Get(10),
            TerminalAttributes.Italic,
            replacementHyperlink);
        _ = frame.Canvas.Draw("界", default, original);

        // Act
        frame.Canvas.ApplyCellStyle(new Rect(1, 0, 1, 1), (_, _) => replacement);

        // Assert
        frame.GetCell(default).Style.ShouldBe(replacement);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(replacement);
        frame.GetCell(default).Style.Hyperlink.ShouldBeSameAs(replacementHyperlink);
        frame.GetCell(new Point(1, 0)).Style.Hyperlink.ShouldBeSameAs(replacementHyperlink);
        frame.GetCell(default).Style.Hyperlink.ShouldNotBeSameAs(originalHyperlink);
        FrameTests.GetText(frame, default).ShouldBe("界");
        frame.GetCell(new Point(1, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies complete stored owners are composed once in row-major order while untouched blanks remain untouched.</summary>
    [Fact]
    public void ApplyCellStyle_WhenRegionContainsStoredOwnersAndBlanks_VisitsOwnersOnceInRowMajorOrder()
    {
        // Arrange
        using Frame frame = new(new Size(4, 2));
        var first = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var space = new CellStyle(ReferenceColors.Get(3), ReferenceColors.Get(4));
        var wide = new CellStyle(ReferenceColors.Get(5), ReferenceColors.Get(6));
        frame.Canvas.DrawRune(new Rune('A'), new Point(2, 0), first);
        frame.Canvas.DrawRune(new Rune(' '), new Point(0, 1), space);
        _ = frame.Canvas.Draw("界", new Point(2, 1), wide);
        var untouchedIndex = frame.GetIndex(new Point(1, 0));
        var untouchedRevision = frame.GetCellByIndex(untouchedIndex).MutationRevision;
        var visited = new List<(Point Point, CellStyle Style)>();

        // Act
        frame.Canvas.ApplyCellStyle(
            frame.Canvas.Bounds,
            (point, style) =>
            {
                visited.Add((point, style));
                return style.WithForeground(ReferenceColors.Get(12));
            });

        // Assert
        visited.ShouldBe(
        [
            (new Point(2, 0), first),
            (new Point(0, 1), space),
            (new Point(2, 1), wide),
        ]);
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe(" ");
        frame.GetCell(new Point(0, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(12));
        frame.GetCell(new Point(2, 1)).Style.ShouldBe(frame.GetCell(new Point(3, 1)).Style);
        frame.GetCellByIndex(untouchedIndex).MutationRevision.ShouldBe(untouchedRevision);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies a partially clipped wide owner is skipped before selection or style mutation.</summary>
    [Fact]
    public void ApplyCellStyle_WhenClipExcludesPartOfWideOwner_SkipsOwnerAndSelector()
    {
        // Arrange
        using Frame frame = new(new Size(2, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("界", default, original);
        var calls = 0;

        // Act
        frame.Canvas.Clip(new Rect(1, 0, 1, 1)).ApplyCellStyle(
            frame.Canvas.Bounds,
            (_, style) =>
            {
                calls++;
                return style.WithForeground(ReferenceColors.Get(9));
            });

        // Assert
        calls.ShouldBe(0);
        frame.GetCell(default).Style.ShouldBe(original);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
    }

    /// <summary>Verifies callback validation precedes frame lifetime validation and valid calls honor disposal.</summary>
    [Fact]
    public void ApplyCellStyle_WhenSelectorIsNullOrFrameIsDisposed_ThrowsInValidationOrder()
    {
        // Arrange
        var frame = new Frame(new Size(1, 1));
        var canvas = frame.Canvas;
        frame.Dispose();

        // Act
        var nullThrown = Should.Throw<ArgumentNullException>(() =>
            canvas.ApplyCellStyle(default, null!));
        var disposedThrown = Should.Throw<ObjectDisposedException>(() =>
            canvas.ApplyCellStyle(default, (_, style) => style));

        // Assert
        nullThrown.ParamName.ShouldBe("selector");
        disposedThrown.ObjectName.ShouldBe(typeof(Frame).FullName);
    }

    /// <summary>Verifies selector failure propagates unchanged after committing only the completed traversal prefix.</summary>
    [Fact]
    public void ApplyCellStyle_WhenSelectorThrows_PreservesPrefixAndRemainingOwners()
    {
        // Arrange
        using Frame frame = new(new Size(4, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("AB界", default, original);
        var replacement = new CellStyle(ReferenceColors.Get(9), ReferenceColors.Get(10));
        var failure = new InvalidOperationException("selector failed");

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.ApplyCellStyle(
                frame.Canvas.Bounds,
                (point, _) => point.X == 1 ? throw failure : replacement));

        // Assert
        thrown.ShouldBeSameAs(failure);
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(replacement);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
        frame.GetCell(new Point(2, 0)).Style.ShouldBe(original);
        frame.GetCell(new Point(3, 0)).Style.ShouldBe(original);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("B");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("界");
    }

    /// <summary>Verifies a write-scoped effect leaves untouched stored owners unchanged inside its region.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawWritesSubset_PreservesUntouchedInRegionOwners()
    {
        using Frame frame = new(new Size(3, 1));
        var original = new CellStyle(
            ReferenceColors.Get(1),
            ReferenceColors.Get(2),
            TerminalAttributes.Bold,
            "https://example.test/underlay",
            Underline.Curly,
            ReferenceColors.Get(3));
        _ = frame.Canvas.Draw("ZZZ", default, original);
        var visited = new List<Point>();
        var untouchedRevision = frame.GetCellByIndex(0).MutationRevision;

        frame.Canvas.DrawWithForeground(
            frame.Canvas.Bounds,
            canvas => canvas.DrawRune(new Rune('A'), new Point(1, 0), original),
            point =>
            {
                visited.Add(point);
                return Color.Rgb(10, 20, 30);
            });

        visited.ShouldBe([new Point(1, 0)]);
        FrameTests.GetText(frame, default).ShouldBe("Z");
        frame.GetCell(default).Style.ShouldBe(original);
        frame.GetCellByIndex(0).MutationRevision.ShouldBe(untouchedRevision);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("A");
        AssertPreserved(original, frame.GetCell(new Point(1, 0)).Style, Color.Rgb(10, 20, 30));
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("Z");
        frame.GetCell(new Point(2, 0)).Style.ShouldBe(original);
    }

    /// <summary>Verifies an identical semantic overwrite remains observable as a write to the effect.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawOverwritesIdentically_TransformsWrittenOwner()
    {
        using Frame frame = new(new Size(1, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("A", default, original);
        var selectorCalls = 0;

        frame.Canvas.DrawWithForeground(
            frame.Canvas.Bounds,
            canvas => canvas.DrawRune(new Rune('A'), default, original),
            _ =>
            {
                selectorCalls++;
                return ReferenceColors.Get(7);
            });

        selectorCalls.ShouldBe(1);
        FrameTests.GetText(frame, default).ShouldBe("A");
        AssertPreserved(original, frame.GetCell(default).Style, ReferenceColors.Get(7));
    }

    /// <summary>Verifies written spaces transform while pre-existing stored owners remain unchanged.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawWritesSpace_TransformsSpaceOnly()
    {
        using Frame frame = new(new Size(2, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("Z", new Point(1, 0), original);
        var visited = new List<Point>();

        frame.Canvas.DrawWithForeground(
            frame.Canvas.Bounds,
            canvas => _ = canvas.Draw(" ", default, original),
            point =>
            {
                visited.Add(point);
                return ReferenceColors.Get(7);
            });

        visited.ShouldBe([default]);
        FrameTests.GetText(frame, default).ShouldBe(" ");
        AssertPreserved(original, frame.GetCell(default).Style, ReferenceColors.Get(7));
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("Z");
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
    }

    /// <summary>Verifies write-scoped selection visits complete written owners once in row-major order.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawWritesWideAndNarrowOwners_VisitsLeadsRowMajorAndKeepsWideAtomic()
    {
        using Frame frame = new(new Size(5, 2));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var visited = new List<Point>();

        frame.Canvas.DrawWithForeground(
            frame.Canvas.Bounds,
            canvas =>
            {
                _ = canvas.Draw("B", new Point(4, 1), original);
                _ = canvas.Draw("A", new Point(3, 0), original);
                _ = canvas.Draw("界", default, original);
            },
            point =>
            {
                visited.Add(point);
                return Color.Rgb(point.X * 10, point.Y * 20, 30);
            });

        visited.ShouldBe([default, new Point(3, 0), new Point(4, 1)]);
        var lead = frame.GetCell(default);
        var continuation = frame.GetCell(new Point(1, 0));
        lead.Width.ShouldBe(2);
        continuation.Continuation.ShouldBeTrue();
        continuation.Lead.ShouldBe(default);
        continuation.Style.ShouldBe(lead.Style);
        lead.Style.Foreground.ShouldBe(Color.Rgb(0, 0, 30));
    }

    /// <summary>Verifies a drawing failure propagates without invoking the foreground selector.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawThrows_PropagatesIdentityWithoutEffectPass()
    {
        using Frame frame = new(new Size(1, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var failure = new InvalidOperationException("draw failed");
        var selectorCalls = 0;

        var thrown = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.DrawWithForeground(
                frame.Canvas.Bounds,
                canvas =>
                {
                    canvas.DrawRune(new Rune('A'), default, original);
                    throw failure;
                },
                _ =>
                {
                    selectorCalls++;
                    return ReferenceColors.Get(7);
                }));

        thrown.ShouldBeSameAs(failure);
        selectorCalls.ShouldBe(0);
        FrameTests.GetText(frame, default).ShouldBe("A");
        frame.GetCell(default).Style.ShouldBe(original);
    }

    /// <summary>Verifies selector failure preserves a transformed prefix and unchanged remaining writes.</summary>
    [Fact]
    public void DrawWithForeground_WhenSelectorThrows_PropagatesIdentityWithPartialProgress()
    {
        using Frame frame = new(new Size(3, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var failure = new InvalidOperationException("selector failed");

        var thrown = Should.Throw<InvalidOperationException>(() =>
            frame.Canvas.DrawWithForeground(
                frame.Canvas.Bounds,
                canvas => _ = canvas.Draw("ABC", default, original),
                point => point.X == 1 ? throw failure : ReferenceColors.Get(7)));

        thrown.ShouldBeSameAs(failure);
        AssertPreserved(original, frame.GetCell(default).Style, ReferenceColors.Get(7));
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(original);
        frame.GetCell(new Point(2, 0)).Style.ShouldBe(original);
    }

    /// <summary>Verifies selector-side writes occur after the closed draw provenance window.</summary>
    [Fact]
    public void DrawWithForeground_WhenSelectorMutatesLaterOwner_DoesNotTransformSelectorWrite()
    {
        using Frame frame = new(new Size(3, 1));
        var canvas = frame.Canvas;
        var drawn = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var selectorWrite = new CellStyle(
            ReferenceColors.Get(9),
            ReferenceColors.Get(4),
            TerminalAttributes.Italic,
            "https://example.test/selector-write",
            Underline.Curly,
            ReferenceColors.Get(5));
        var visited = new List<Point>();

        canvas.DrawWithForeground(
            canvas.Bounds,
            draw => _ = draw.Draw("ABC", default, drawn),
            point =>
            {
                visited.Add(point);

                if (point == default)
                {
                    _ = canvas.Draw("X", new Point(1, 0), selectorWrite);
                }

                return ReferenceColors.Get(7);
            });

        visited.ShouldBe([default, new Point(2, 0)]);
        AssertPreserved(drawn, frame.GetCell(default).Style, ReferenceColors.Get(7));
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("X");
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(selectorWrite);
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("C");
        AssertPreserved(drawn, frame.GetCell(new Point(2, 0)).Style, ReferenceColors.Get(7));
    }

    /// <summary>Verifies a selector overwrite of its current owner remains outside the draw window.</summary>
    [Fact]
    public void DrawWithForeground_WhenSelectorOverwritesCurrentOwner_PreservesSelectorWrite()
    {
        using Frame frame = new(new Size(1, 1));
        var canvas = frame.Canvas;
        var drawn = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var selectorWrite = new CellStyle(ReferenceColors.Get(9), ReferenceColors.Get(4), TerminalAttributes.Italic);
        var selectorCalls = 0;

        canvas.DrawWithForeground(
            canvas.Bounds,
            draw => _ = draw.Draw("A", default, drawn),
            point =>
            {
                selectorCalls++;
                _ = canvas.Draw("A", default, selectorWrite);
                return ReferenceColors.Get(7);
            });

        selectorCalls.ShouldBe(1);
        FrameTests.GetText(frame, default).ShouldBe("A");
        frame.GetCell(default).Style.ShouldBe(selectorWrite);
    }

    /// <summary>Verifies null callbacks win validation and never draw or change stored state.</summary>
    [Fact]
    public void DrawWithForeground_WhenCallbackIsNull_ThrowsBeforeStateOrDisposalAccess()
    {
        var frame = new Frame(new Size(1, 1));
        var canvas = frame.Canvas;
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = canvas.Draw("Z", default, original);
        var drawCalls = 0;

        void Draw(TerminalCanvas _) => drawCalls++;

        static Color Select(Point _) => ReferenceColors.Get(7);

        _ = Should.Throw<ArgumentNullException>(() =>
            canvas.DrawWithForeground(canvas.Bounds, null!, Select));
        _ = Should.Throw<ArgumentNullException>(() =>
            canvas.DrawWithForeground(canvas.Bounds, Draw, null!));

        drawCalls.ShouldBe(0);
        FrameTests.GetText(frame, default).ShouldBe("Z");
        frame.GetCell(default).Style.ShouldBe(original);

        frame.Dispose();

        _ = Should.Throw<ArgumentNullException>(() =>
            canvas.DrawWithForeground(default, null!, Select));
        _ = Should.Throw<ArgumentNullException>(() =>
            canvas.DrawWithForeground(default, Draw, null!));
        _ = Should.Throw<ObjectDisposedException>(() =>
            canvas.DrawWithForeground(default, Draw, Select));
        drawCalls.ShouldBe(0);
    }

    /// <summary>Verifies nested effects expose inner writes to the outer write scope without leaking regions.</summary>
    [Fact]
    public void DrawWithForeground_WhenNested_OuterEffectSeesInnerWritesInsideOuterRegion()
    {
        using Frame frame = new(new Size(3, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        var innerVisited = new List<Point>();
        var outerVisited = new List<Point>();

        frame.Canvas.DrawWithForeground(
            new Rect(0, 0, 2, 1),
            outer => outer.DrawWithForeground(
                frame.Canvas.Bounds,
                inner => _ = inner.Draw("ABC", default, original),
                point =>
                {
                    innerVisited.Add(point);
                    return ReferenceColors.Get(2);
                }),
            point =>
            {
                outerVisited.Add(point);
                return ReferenceColors.Get(3);
            });

        innerVisited.ShouldBe([default, new Point(1, 0), new Point(2, 0)]);
        outerVisited.ShouldBe([default, new Point(1, 0)]);
        frame.GetCell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        frame.GetCell(new Point(1, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        frame.GetCell(new Point(2, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(2));
    }

    /// <summary>Verifies write-revision metadata cannot create semantic frame damage.</summary>
    [Fact]
    public void DrawWithForeground_WhenOverwriteIsSemanticallyIdentical_ProducesNoDamage()
    {
        using Frame front = new(new Size(1, 1));
        var original = new CellStyle(ReferenceColors.Get(1), ReferenceColors.Get(2));
        _ = front.Canvas.Draw("A", default, original);
        using var back = front.Clone();

        back.GetCellByIndex(0).MutationRevision.ShouldBe(front.GetCellByIndex(0).MutationRevision);

        back.Canvas.DrawWithForeground(
            back.Canvas.Bounds,
            canvas => _ = canvas.Draw("A", default, original),
            _ => original.Foreground);

        DamageTests.GetSpans(front, back).ShouldBeEmpty();
    }

    #endregion

    #region Bounded geometry

    // Overflow-safe geometry at extreme public coordinates carries a bounded-work obligation -
    // hostile geometry must not monopolize the render thread - but that property has no dedicated
    // regression test here. Proving it directly would require either a wall-clock budget (which
    // measures the host machine as much as the product and is inherently flaky under CI load or
    // coverage instrumentation) or a mutable instance counter on <see cref="Frame"/> solely for
    // test observability, and neither belongs in this type. The bound is documented on each
    // primitive's early-reject check and enforced by code review; this region keeps only the cases
    // that assert an observable, deterministic result.

    /// <summary>Verifies a fully invisible segment writes nothing at all.</summary>
    [Fact]
    public void DrawLine_WhenGeometryIsFullyOutsideClip_WritesNoCells()
    {
        using Frame frame = new(new Size(6, 3));
        var canvas = frame.Canvas.Clip(new Rect(1, 1, 2, 1));

        canvas.DrawLine(new Point(0, 0), new Point(5, 0), new Rune('*'));

        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies an origin at the integer maximum does not wrap its inset arithmetic into negative
    /// coordinates and draw a box nowhere near the request.
    /// </summary>
    [Fact]
    public void DrawBox_WhenOriginIsIntMaxValue_DoesNotWrapCoordinates()
    {
        using Frame frame = new(new Size(6, 3));

        frame.Canvas.DrawBox(new Rect(int.MaxValue, int.MaxValue, 4, 3), LineStyle.Light);

        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies an axis line whose origin plus length exceeds the integer range clips instead of
    /// throwing, and still paints its visible span.
    /// </summary>
    [Fact]
    public void DrawHorizontalLine_WhenOriginPlusLengthOverflows_ClipsWithoutThrowing()
    {
        using Frame frame = new(new Size(6, 3));

        frame.Canvas.DrawHorizontalLine(new Point(int.MaxValue - 2, 1), int.MaxValue, LineStyle.Light);

        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies drawing text whose origin sits at the integer maximum saturates the cursor advance
    /// instead of throwing, even though every glyph is already off-frame and invisible.
    /// </summary>
    [Fact]
    public void Draw_WhenOriginIsNearIntMaxValue_DoesNotThrowAndClipsWholeText()
    {
        using Frame frame = new(new Size(6, 3));

        var result = frame.Canvas.Draw("AB".AsSpan(), new Point(int.MaxValue - 1, 0));

        result.Final.ShouldBe(new Point(int.MaxValue, 0));
        result.Clipped.ShouldBe(2);
        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies <see cref="Edge.Wrap"/> advancing to a row already at the integer maximum saturates
    /// instead of throwing, when the wrapped row itself is already off-frame and invisible.
    /// </summary>
    [Fact]
    public void Draw_WhenEdgeWrapRowIsNearIntMaxValue_DoesNotThrow()
    {
        using Frame frame = new(new Size(2, 1));

        var result = frame.Canvas.Draw("界".AsSpan(), new Point(1, int.MaxValue), edge: Edge.Wrap);

        result.Final.ShouldBe(new Point(2, int.MaxValue));
        result.Clipped.ShouldBe(1);
        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies a tab control character advancing the cursor past the integer maximum saturates
    /// instead of throwing, when the resulting tab stop is already off-frame and invisible.
    /// </summary>
    [Fact]
    public void Draw_WhenTabAdvanceIsNearIntMaxValue_DoesNotThrow()
    {
        using Frame frame = new(new Size(2, 1));

        var result = frame.Canvas.Draw("\t".AsSpan(), new Point(int.MaxValue - 2, 0));

        result.Final.ShouldBe(new Point(int.MaxValue, 0));
        AssertBlank(frame);
    }

    private static void AssertBlank(Frame frame)
    {
        for (var y = 0; y < frame.Size.Height; y++)
        {
            for (var x = 0; x < frame.Size.Width; x++)
            {
                FrameTests.GetText(frame, new Point(x, y)).ShouldBeEmpty();
            }
        }
    }

    #endregion

    #region Ambiguous width primitives

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

    #endregion

    #region Frame region copy

    // The copy is the mechanism behind render-clean subtree reuse, so an invalid destination here
    // would surface much later as a corrupt frame rather than as a failed copy.

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

    /// <summary>
    /// Verifies a destination wide lead straddling the region's left edge is repaired rather than
    /// left with its continuation overwritten, which would otherwise leave an orphan lead that
    /// shifts every later cell in the row one column to the right.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenDestinationWideLeadPrecedesRegion_RepairsTheOrphanedLead()
    {
        using var previous = new Frame(new Size(4, 1));
        _ = previous.Canvas.Draw("w", new Point(3, 0));
        using var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw("你", new Point(2, 0));
        Attach(frame, previous);

        frame.Canvas.CopyFromPrevious(new Rect(3, 0, 1, 1));

        FrameTests.GetText(frame, new Point(2, 0)).ShouldBeEmpty();
        FrameTests.GetText(frame, new Point(3, 0)).ShouldBe("w");
    }

    /// <summary>
    /// Verifies a destination wide lead straddling the region's right edge is repaired rather than
    /// leaving its continuation intact, which would otherwise leave an orphan continuation and stale
    /// content surviving past the end of the copied span.
    /// </summary>
    [Fact]
    public void CopyFromPrevious_WhenDestinationWideLeadEndsRegion_RepairsTheOrphanedContinuation()
    {
        using var previous = new Frame(new Size(5, 1));
        _ = previous.Canvas.Draw("z", new Point(3, 0));
        using var frame = new Frame(new Size(5, 1));
        _ = frame.Canvas.Draw("你", new Point(3, 0));
        Attach(frame, previous);

        frame.Canvas.CopyFromPrevious(new Rect(0, 0, 4, 1));

        FrameTests.GetText(frame, new Point(3, 0)).ShouldBe("z");
        FrameTests.GetText(frame, new Point(4, 0)).ShouldBeEmpty();
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

    #endregion

    private static void AssertPreserved(CellStyle original, CellStyle actual, Color foreground)
    {
        actual.Foreground.ShouldBe(foreground);
        actual.Background.ShouldBe(original.Background);
        actual.Attributes.ShouldBe(original.Attributes);
        actual.Hyperlink.ShouldBe(original.Hyperlink);
        actual.Underline.ShouldBe(original.Underline);
        actual.UnderlineColor.ShouldBe(original.UnderlineColor);
    }

    /// <summary>Asserts the exact rendered text of every frame row.</summary>
    /// <param name="frame">The rendered frame under test.</param>
    /// <param name="expected">One expected string per row, in top-to-bottom order.</param>
    internal static void AssertRows(Frame frame, params string[] expected)
    {
        expected.Length.ShouldBe(frame.Size.Height);

        for (var y = 0; y < expected.Length; y++)
        {
            expected[y].Length.ShouldBe(frame.Size.Width);

            for (var x = 0; x < expected[y].Length; x++)
            {
                var actual = FrameTests.GetText(frame, new Point(x, y));
                var value = expected[y][x];

                if (value == ' ')
                {
                    actual.ShouldBeEmpty();
                }
                else
                {
                    actual.ShouldBe(value.ToString());
                }
            }
        }
    }
}
