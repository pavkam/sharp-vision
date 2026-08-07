// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies the patterned <c>DrawQuadrantLine</c> overload's on/off dash geometry.</summary>
public sealed class QuadrantLinePatternTests
{
    #region Horizontal, vertical, and sloped exact-cell coverage

    /// <summary>Verifies every pattern's exact horizontal on/off run-length cycle. A '.' in
    /// <paramref name="expected"/> stands for an untouched (skipped) cell.</summary>
    [Theory]
    [MemberData(nameof(HorizontalCases))]
    public void DrawQuadrantLine_WhenHorizontalWithPattern_WritesExactRunLength(LinePattern pattern, string expected)
    {
        using Frame frame = new(new Size(8, 1));

        _ = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(15, 0), pattern, 0);

        for (var x = 0; x < 8; x++)
        {
            FrameTests.GetText(frame, new Point(x, 0)).ShouldBe(Cell(expected[x]), $"cell {x}");
        }
    }

    /// <summary>Verifies every pattern's exact vertical on/off run-length cycle. A '.' in
    /// <paramref name="expected"/> stands for an untouched (skipped) cell.</summary>
    [Theory]
    [MemberData(nameof(VerticalCases))]
    public void DrawQuadrantLine_WhenVerticalWithPattern_WritesExactRunLength(LinePattern pattern, string expected)
    {
        using Frame frame = new(new Size(1, 8));

        _ = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(0, 15), pattern, 0);

        for (var y = 0; y < 8; y++)
        {
            FrameTests.GetText(frame, new Point(0, y)).ShouldBe(Cell(expected[y]), $"cell {y}");
        }
    }

    /// <summary>Verifies every pattern's exact sloped (diagonal) on/off run-length cycle. A '.' in
    /// <paramref name="expected"/> stands for an untouched (skipped) cell.</summary>
    [Theory]
    [MemberData(nameof(DiagonalCases))]
    public void DrawQuadrantLine_WhenDiagonalWithPattern_WritesExactRunLength(LinePattern pattern, string expected)
    {
        using Frame frame = new(new Size(4, 4));

        _ = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 7), pattern, 0);

        for (var index = 0; index < 4; index++)
        {
            FrameTests.GetText(frame, new Point(index, index)).ShouldBe(Cell(expected[index]), $"cell {index}");
        }
    }

    /// <summary>Provides the exact 8-cell horizontal run-length cycle per pattern.</summary>
    public static TheoryData<LinePattern, string> HorizontalCases => new()
    {
        { LinePattern.Solid, "▀▀▀▀▀▀▀▀" },
        { LinePattern.DoubleDash, "▀▘▝▀.▀▘▝" },
        { LinePattern.TripleDash, "▀.▀.▀.▀." },
        { LinePattern.QuadrupleDash, "▘▝.▘▝.▘▝" }
    };

    /// <summary>Provides the exact 8-cell vertical run-length cycle per pattern.</summary>
    public static TheoryData<LinePattern, string> VerticalCases => new()
    {
        { LinePattern.Solid, "▌▌▌▌▌▌▌▌" },
        { LinePattern.DoubleDash, "▌▘▖▌.▌▘▖" },
        { LinePattern.TripleDash, "▌.▌.▌.▌." },
        { LinePattern.QuadrupleDash, "▘▖.▘▖.▘▖" }
    };

    /// <summary>Provides the exact 4-cell diagonal run-length cycle per pattern.</summary>
    public static TheoryData<LinePattern, string> DiagonalCases => new()
    {
        { LinePattern.Solid, "▚▚▚▚" },
        { LinePattern.DoubleDash, "▚▘▗▚" },
        { LinePattern.TripleDash, "▚.▚." },
        { LinePattern.QuadrupleDash, "▘▗.▘" }
    };

    #endregion

    #region Composition with solid lines and the wide Ambiguous policy

    /// <summary>Verifies a dashed line crossing a solid line still merges: cells where the dash
    /// is "on" gain the full union, and cells where it is "off" keep only the solid line's own
    /// quadrant instead of losing it.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenPatternedLineCrossesSolidLine_MergesOrNotQuadrantBits()
    {
        using Frame frame = new(new Size(4, 1));

        // The whole upper half row is solid...
        frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0));

        // ...and a DoubleDash line runs along the lower half row beneath it.
        _ = frame.Canvas.DrawQuadrantLine(new Point(0, 1), new Point(7, 1), LinePattern.DoubleDash, 0);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("█", "both lower halves on: full cell");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("▛", "only the lower-left half on");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("▜", "only the lower-right half on");
        FrameTests.GetText(frame, new Point(3, 0)).ShouldBe("█", "both lower halves on: full cell");
    }

    /// <summary>Verifies the wide Ambiguous policy degrades every "on" step to the portable '#'
    /// fallback exactly like a solid line, while an "off" step still leaves the cell untouched
    /// rather than writing a degraded empty glyph.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenPolicyIsWide_DegradesOnStepsAndSkipsOffSteps()
    {
        using Frame frame = new(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        _ = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0), LinePattern.TripleDash, 0);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("#");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(string.Empty);
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("#");
        FrameTests.GetText(frame, new Point(3, 0)).ShouldBe(string.Empty);
    }

    #endregion

    #region Phase continuation

    /// <summary>Verifies chaining two segments that share a joint vertex - passing the first
    /// segment's returned step as the second segment's <c>patternStep</c>, exactly as
    /// <c>LineChartRenderer</c> does across a series polyline - reproduces exactly the same cells
    /// as drawing the whole polyline in one call. The shared vertex is redrawn (idempotently) by
    /// both calls at the same step, so the phase neither skips nor double-counts it.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenChainedThroughSharedVertex_MatchesOneContinuousCallHorizontally()
    {
        using Frame whole = new(new Size(8, 1));
        using Frame chained = new(new Size(8, 1));

        _ = whole.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(15, 0), LinePattern.TripleDash, 0);

        var step = chained.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(8, 0), LinePattern.TripleDash, 0);
        _ = chained.Canvas.DrawQuadrantLine(new Point(8, 0), new Point(15, 0), LinePattern.TripleDash, step);

        for (var x = 0; x < 8; x++)
        {
            FrameTests.GetText(chained, new Point(x, 0)).ShouldBe(FrameTests.GetText(whole, new Point(x, 0)), $"cell {x}");
        }
    }

    /// <summary>The same shared-vertex chaining guarantee along a sloped segment, where every
    /// Bresenham iteration moves both coordinates and the "paired quadrant" merge behavior makes
    /// a double-counted or skipped joint step especially easy to miss.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenChainedThroughSharedVertex_MatchesOneContinuousCallDiagonally()
    {
        using Frame whole = new(new Size(8, 8));
        using Frame chained = new(new Size(8, 8));

        _ = whole.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(15, 15), LinePattern.DoubleDash, 0);

        var step = chained.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(8, 8), LinePattern.DoubleDash, 0);
        _ = chained.Canvas.DrawQuadrantLine(new Point(8, 8), new Point(15, 15), LinePattern.DoubleDash, step);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                FrameTests.GetText(chained, new Point(x, y)).ShouldBe(FrameTests.GetText(whole, new Point(x, y)), $"cell ({x}, {y})");
            }
        }
    }

    /// <summary>Verifies the returned step is the step used to draw the segment's final point -
    /// not one past it - so a zero-length segment (start equal to end) returns its
    /// <c>patternStep</c> unchanged, and re-passing the returned value to a chained call
    /// re-evaluates the shared vertex rather than skipping past it.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenSegmentCompletes_ReturnsTheFinalPointsOwnStep()
    {
        using Frame frame = new(new Size(4, 1));
        using Frame zeroLength = new(new Size(1, 1));

        var step = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0), LinePattern.Solid, 5);
        var zeroLengthStep = zeroLength.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(0, 0), LinePattern.Solid, 3);

        step.ShouldBe(12, "8 points from step 5 land on steps 5..12; the endpoint's own step is 12");
        zeroLengthStep.ShouldBe(3, "a zero-length segment only ever visits its single point's own step");
    }

    #endregion

    #region Argument validation

    /// <summary>Verifies invalid arguments are rejected by name.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenArgumentsAreInvalid_Throws()
    {
        using Frame frame = new(new Size(1, 1));

        Should.Throw<ArgumentOutOfRangeException>(() =>
                frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(0, 0), (LinePattern) 99, 0))
            .ParamName.ShouldBe("pattern");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(0, 0), LinePattern.Solid, -1))
            .ParamName.ShouldBe("patternStep");
    }

    #endregion

    private static string Cell(char value) => value == '.' ? string.Empty : value.ToString();
}
