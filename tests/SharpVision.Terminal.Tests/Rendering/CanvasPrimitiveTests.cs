// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies validated Rune, fill, and grapheme-preserving style primitives.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class CanvasPrimitiveTests
{
    private static readonly Action<Canvas> _allocationDraw = DrawAllocationCell;
    private static readonly Func<Point, Color> _allocationSelector = SelectAllocationForeground;

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
            Attributes.Bold,
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
        continuation.IsContinuation.ShouldBeTrue();
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
        continuation.IsContinuation.ShouldBeTrue();
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

    /// <summary>Verifies a write-scoped effect leaves untouched stored owners unchanged inside its region.</summary>
    [Fact]
    public void DrawWithForeground_WhenDrawWritesSubset_PreservesUntouchedInRegionOwners()
    {
        using Frame frame = new(new Size(3, 1));
        var original = new CellStyle(
            ReferenceColors.Get(1),
            ReferenceColors.Get(2),
            Attributes.Bold,
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
        continuation.IsContinuation.ShouldBeTrue();
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
            Attributes.Italic,
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
        var selectorWrite = new CellStyle(ReferenceColors.Get(9), ReferenceColors.Get(4), Attributes.Italic);
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

        void Draw(Canvas _) => drawCalls++;

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

    /// <summary>Verifies warmed write-scoped foreground drawing allocates no managed memory per render.</summary>
    [Fact]
    public void DrawWithForeground_WhenCallbacksAreCached_AllocatesNoManagedMemory()
    {
        using Frame frame = new(new Size(1, 1));
        var canvas = frame.Canvas;

        for (var index = 0; index < 32; index++)
        {
            Render();
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 128; index++)
            {
                Render();
            }

            minimum = Math.Min(
                minimum,
                GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);
        return;

        void Render()
        {
            frame.Clear();
            canvas.DrawWithForeground(canvas.Bounds, _allocationDraw, _allocationSelector);
        }
    }

    #endregion

    private static void DrawAllocationCell(Canvas canvas) =>
        canvas.DrawRune(new Rune('A'), default);

    private static Color SelectAllocationForeground(Point _) => ReferenceColors.Get(7);

    private static void AssertPreserved(CellStyle original, CellStyle actual, Color foreground)
    {
        actual.Foreground.ShouldBe(foreground);
        actual.Background.ShouldBe(original.Background);
        actual.Attributes.ShouldBe(original.Attributes);
        actual.Hyperlink.ShouldBe(original.Hyperlink);
        actual.Underline.ShouldBe(original.Underline);
        actual.UnderlineColor.ShouldBe(original.UnderlineColor);
    }

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
