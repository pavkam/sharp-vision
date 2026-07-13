namespace SharpVision.Terminal.Tests.Rendering;

using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using Shouldly;

/// <summary>Verifies validated Rune, fill, and grapheme-preserving style primitives.</summary>
public sealed class CanvasPrimitiveTests
{
    #region Rune drawing

    /// <summary>Verifies drawing rejects a wide Rune before changing the frame.</summary>
    [Fact]
    public void DrawRune_WhenRuneIsWide_ThrowsBeforeMutation()
    {
        using var frame = new Frame(new Size(2, 1));

        _ = Should.Throw<ArgumentException>(() =>
            frame.Canvas.DrawRune(new Rune('界'), default));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies one narrow Rune is drawn with the requested style.</summary>
    [Fact]
    public void DrawRune_WhenRuneIsNarrow_WritesExactCell()
    {
        using var frame = new Frame(new Size(1, 1));
        var style = new Style(Color.Indexed(2), Color.Indexed(4));

        frame.Canvas.DrawRune(new Rune('┼'), default, style);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("┼");
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(style);
    }

    /// <summary>Verifies transparent single-glyph drawing preserves the destination background.</summary>
    [Fact]
    public void DrawRune_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using var frame = new Frame(new Size(1, 1));
        var surface = new Style(Color.Indexed(255), Color.Indexed(238));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), surface);

        frame.Canvas.DrawRune(
            new Rune('│'),
            default,
            new Style(Color.Indexed(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new Style(Color.Indexed(45), Color.Indexed(238)));
    }

    #endregion

    #region Region operations

    /// <summary>Verifies fill honors the intersection of region and canvas clip.</summary>
    [Fact]
    public void Fill_WhenRegionIsClipped_WritesOnlyIntersection()
    {
        using var frame = new Frame(new Size(4, 1));
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
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("界", default);
        var style = new Style(background: Color.Indexed(5));

        frame.Canvas.ApplyStyle(new Rect(1, 0, 1, 1), style);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("界");
        frame.GetCell(new Point(0, 0)).Style.ShouldBe(style);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(style);
    }

    /// <summary>Verifies transparent style overlays preserve destination backgrounds.</summary>
    [Fact]
    public void ApplyStyle_WhenBackgroundIsTransparent_PreservesDestinationBackground()
    {
        using var frame = new Frame(new Size(1, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new Style(Color.Indexed(255), Color.Indexed(238)));

        frame.Canvas.ApplyStyle(
            frame.Canvas.Bounds,
            new Style(Color.Indexed(45), Color.Default),
            BackgroundMode.Transparent);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(new Style(Color.Indexed(45), Color.Indexed(238)));
    }

    /// <summary>Verifies a clipped partial wide owner is not half-restyled.</summary>
    [Fact]
    public void ApplyStyle_WhenClipExcludesWideLead_SkipsCompleteOwner()
    {
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("界", default);
        var style = new Style(background: Color.Indexed(5));

        frame.Canvas.Clip(new Rect(1, 0, 1, 1)).ApplyStyle(new Rect(1, 0, 1, 1), style);

        frame.GetCell(new Point(0, 0)).Style.ShouldBe(Style.Default);
        frame.GetCell(new Point(1, 0)).Style.ShouldBe(Style.Default);
    }

    #endregion
}
