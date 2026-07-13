namespace SharpVision.Terminal.Tests.Rendering;

using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using Shouldly;

/// <summary>Verifies topology-aware Unicode line and box drawing.</summary>
public sealed class LineTests
{
    #region Line families

    /// <summary>Verifies each standard weight produces its exact horizontal glyph.</summary>
    [Theory]
    [InlineData(LineWeight.Light, "─")]
    [InlineData(LineWeight.Heavy, "━")]
    [InlineData(LineWeight.Paired, "═")]
    public void DrawHorizontalLine_WhenWeightIsSelected_WritesExactGlyph(
        LineWeight weight,
        string expected)
    {
        using var frame = new Frame(new Size(3, 1));

        frame.Canvas.DrawHorizontalLine(default, 3, new LineStyle(weight));

        for (var x = 0; x < 3; x++)
        {
            FrameTests.GetText(frame, new Point(x, 0)).ShouldBe(expected);
        }
    }

    /// <summary>Verifies rounded and double boxes resolve exact corners.</summary>
    [Theory]
    [MemberData(nameof(BoxCases))]
    public void DrawBox_WhenStyleIsSelected_WritesExactCorners(
        LineStyle line,
        string topLeft,
        string topRight,
        string bottomLeft,
        string bottomRight)
    {
        using var frame = new Frame(new Size(3, 3));

        frame.Canvas.DrawBox(new Rect(0, 0, 3, 3), line);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(topLeft);
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe(topRight);
        FrameTests.GetText(frame, new Point(0, 2)).ShouldBe(bottomLeft);
        FrameTests.GetText(frame, new Point(2, 2)).ShouldBe(bottomRight);
    }

    /// <summary>Provides exact box-family cases.</summary>
    public static TheoryData<LineStyle, string, string, string, string> BoxCases => new()
    {
        { LineStyle.Light, "┌", "┐", "└", "┘" },
        { LineStyle.Heavy, "┏", "┓", "┗", "┛" },
        { LineStyle.Paired, "╔", "╗", "╚", "╝" },
        { LineStyle.Rounded, "╭", "╮", "╰", "╯" },
        { LineStyle.Ascii, "+", "+", "+", "+" },
    };

    /// <summary>Verifies dashed straight segments use the selected pattern.</summary>
    [Fact]
    public void DrawVerticalLine_WhenPatternIsTripleDash_WritesDashedGlyph()
    {
        using var frame = new Frame(new Size(1, 2));
        var line = new LineStyle(LineWeight.Heavy, LinePattern.TripleDash);

        frame.Canvas.DrawVerticalLine(default, 2, line);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("┇");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("┇");
    }

    /// <summary>Verifies structural line glyphs preserve the destination cell background.</summary>
    [Fact]
    public void DrawHorizontalLine_WhenSurfaceIsPainted_PreservesDestinationBackground()
    {
        using var frame = new Frame(new Size(3, 1));
        var surface = new Style(Color.Indexed(255), Color.Indexed(238));
        var line = new Style(Color.Indexed(45), Color.Default);
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), surface);

        frame.Canvas.DrawHorizontalLine(default, 3, LineStyle.Light, line);

        for (var x = 0; x < 3; x++)
        {
            frame.GetCell(new Point(x, 0)).Style.ShouldBe(
                new Style(Color.Indexed(45), Color.Indexed(238)));
        }
    }

    #endregion

    #region Topology merging

    /// <summary>Verifies crossing lines merge into one exact junction.</summary>
    [Fact]
    public void DrawLine_WhenLightSegmentsCross_WritesFourWayJunction()
    {
        using var frame = new Frame(new Size(3, 3));

        frame.Canvas.DrawHorizontalLine(new Point(0, 1), 3, LineStyle.Light);
        frame.Canvas.DrawVerticalLine(new Point(1, 0), 3, LineStyle.Light);

        FrameTests.GetText(frame, new Point(1, 1)).ShouldBe("┼");
    }

    /// <summary>Verifies topology and weight merging are independent of draw order.</summary>
    [Fact]
    public void DrawLine_WhenMixedWeightsCross_IsCommutative()
    {
        using var first = new Frame(new Size(3, 3));
        using var second = new Frame(new Size(3, 3));

        first.Canvas.DrawHorizontalLine(new Point(0, 1), 3, LineStyle.Light);
        first.Canvas.DrawVerticalLine(new Point(1, 0), 3, LineStyle.Heavy);
        second.Canvas.DrawVerticalLine(new Point(1, 0), 3, LineStyle.Heavy);
        second.Canvas.DrawHorizontalLine(new Point(0, 1), 3, LineStyle.Light);

        FrameTests.GetText(first, new Point(1, 1))
            .ShouldBe(FrameTests.GetText(second, new Point(1, 1)));
        FrameTests.GetText(first, new Point(1, 1)).ShouldBe("╋");
    }

    /// <summary>Verifies a child clip prevents topology mutation outside its bounds.</summary>
    [Fact]
    public void DrawHorizontalLine_WhenClipped_WritesOnlyVisibleCells()
    {
        using var frame = new Frame(new Size(4, 1));

        frame.Canvas.Clip(new Rect(1, 0, 2, 1))
            .DrawHorizontalLine(default, 4, LineStyle.Light);

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("─");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("─");
        frame.GetCell(new Point(3, 0)).ShouldBe(CellInfo.Blank);
    }

    #endregion
}
