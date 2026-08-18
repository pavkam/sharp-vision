// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies topology-aware Unicode line and box drawing.</summary>
public sealed class LineResolverTests
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
        using Frame frame = new(new Size(3, 1));

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
        using Frame frame = new(new Size(3, 3));

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
        { LineStyle.Ascii, "+", "+", "+", "+" }
    };

    /// <summary>Verifies dashed straight segments use the selected pattern.</summary>
    [Fact]
    public void DrawVerticalLine_WhenPatternIsTripleDash_WritesDashedGlyph()
    {
        using Frame frame = new(new Size(1, 2));
        var line = new LineStyle(LineWeight.Heavy, LinePattern.TripleDash);

        frame.Canvas.DrawVerticalLine(default, 2, line);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("┇");
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("┇");
    }

    /// <summary>Verifies structural line glyphs preserve the destination cell background.</summary>
    [Fact]
    public void DrawHorizontalLine_WhenSurfaceIsPainted_PreservesDestinationBackground()
    {
        using Frame frame = new(new Size(3, 1));
        var surface = new CellStyle(ReferenceColors.Get(255), ReferenceColors.Get(238));
        var line = new CellStyle(ReferenceColors.Get(45), Color.Default);
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), surface);

        frame.Canvas.DrawHorizontalLine(default, 3, LineStyle.Light, line);

        for (var x = 0; x < 3; x++)
        {
            frame.GetCell(new Point(x, 0)).Style.ShouldBe(
                new CellStyle(ReferenceColors.Get(45), ReferenceColors.Get(238)));
        }
    }

    #endregion

    #region Topology merging

    /// <summary>Verifies an exact topology cell resolves the requested tee in every standard line family.</summary>
    [Theory]
    [MemberData(nameof(TeeCases))]
    public void DrawLineCell_WhenTeeTopologyIsRequested_WritesExactFamilyJunctions(
        LineStyle line,
        string expectedLeft,
        string expectedRight)
    {
        using Frame frame = new(new Size(2, 1));

        frame.Canvas.DrawLineCell(
            default,
            LineConnections.Up | LineConnections.Down | LineConnections.Left,
            line);
        frame.Canvas.DrawLineCell(
            new Point(1, 0),
            LineConnections.Up | LineConnections.Right | LineConnections.Down,
            line);

        FrameTests.GetText(frame, default).ShouldBe(expectedLeft);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(expectedRight);
    }

    /// <summary>Provides exact tee-family cases.</summary>
    public static TheoryData<LineStyle, string, string> TeeCases => new()
    {
        { LineStyle.Light, "┤", "├" },
        { LineStyle.Heavy, "┫", "┣" },
        { LineStyle.Paired, "╣", "╠" },
        { LineStyle.Rounded, "┤", "├" },
        { LineStyle.Ascii, "+", "+" }
    };

    /// <summary>Verifies an empty exact topology is rejected before the destination cell changes.</summary>
    [Fact]
    public void DrawLineCell_WhenConnectionsAreEmpty_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.DrawRune(new Rune('x'), default);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            frame.Canvas.DrawLineCell(default, LineConnections.None, LineStyle.Light));

        FrameTests.GetText(frame, default).ShouldBe("x");
    }

    /// <summary>Verifies undefined exact-topology bits are rejected before the destination cell changes.</summary>
    [Fact]
    public void DrawLineCell_WhenConnectionsAreUndefined_ThrowsBeforeMutation()
    {
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.DrawRune(new Rune('x'), default);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            frame.Canvas.DrawLineCell(default, (LineConnections) 16, LineStyle.Light));

        FrameTests.GetText(frame, default).ShouldBe("x");
    }

    /// <summary>Verifies crossing lines merge into one exact junction.</summary>
    [Fact]
    public void DrawLine_WhenLightSegmentsCross_WritesFourWayJunction()
    {
        using Frame frame = new(new Size(3, 3));

        frame.Canvas.DrawHorizontalLine(new Point(0, 1), 3, LineStyle.Light);
        frame.Canvas.DrawVerticalLine(new Point(1, 0), 3, LineStyle.Light);

        FrameTests.GetText(frame, new Point(1, 1)).ShouldBe("┼");
    }

    /// <summary>Verifies topology and weight merging are independent of draw order.</summary>
    [Fact]
    public void DrawLine_WhenMixedWeightsCross_IsCommutative()
    {
        using Frame first = new(new Size(3, 3));
        using Frame second = new(new Size(3, 3));

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
        using Frame frame = new(new Size(4, 1));

        frame.Canvas.Clip(new Rect(1, 0, 2, 1))
            .DrawHorizontalLine(default, 4, LineStyle.Light);

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("─");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("─");
        frame.GetCell(new Point(3, 0)).ShouldBe(CellInfo.Blank);
    }

    #endregion
}
