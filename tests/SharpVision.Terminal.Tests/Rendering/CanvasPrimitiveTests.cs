// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;




/// <summary>Verifies validated Rune, fill, and grapheme-preserving style primitives.</summary>
public sealed class CanvasPrimitiveTests
{
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
        var style = new CellStyle(Color.Indexed(2), Color.Indexed(4));

        frame.Canvas.DrawRune(new Rune('┼'), default, style);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("┼");
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(style);
    }

    /// <summary>Verifies transparent single-glyph drawing preserves the destination background.</summary>
    [Fact]
    public void DrawRune_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(1, 1));
        var surface = new CellStyle(Color.Indexed(255), Color.Indexed(238));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), surface);

        frame.Canvas.DrawRune(
            new Rune('│'),
            default,
            new CellStyle(Color.Indexed(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(Color.Indexed(45), Color.Indexed(238)));
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
        var style = new CellStyle(background: Color.Indexed(5));

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
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new CellStyle(Color.Indexed(255), Color.Indexed(238)));

        frame.Canvas.ApplyStyle(
            frame.Canvas.Bounds,
            new CellStyle(Color.Indexed(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new CellStyle(Color.Indexed(45), Color.Indexed(238)));
    }

    /// <summary>Verifies a clipped partial wide owner is not half-restyled.</summary>
    [Fact]
    public void ApplyStyle_WhenClipExcludesWideLead_SkipsCompleteOwner()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界", default);
        var style = new CellStyle(background: Color.Indexed(5));

        frame.Canvas.Clip(new Rect(1, 0, 1, 1)).ApplyStyle(new Rect(1, 0, 1, 1), style);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(CellStyle.Default);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(CellStyle.Default);
    }

    #endregion

    private static void AssertRows(Frame frame, params string[] expected)
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
