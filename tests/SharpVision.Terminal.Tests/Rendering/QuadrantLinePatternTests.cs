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

    /// <summary>Verifies chaining two segments through the returned pattern step reproduces
    /// exactly the same cells as drawing the whole polyline in one call, so a multi-segment
    /// series keeps a continuous dash phase across the joint instead of restarting it.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenChainedThroughReturnedStep_MatchesOneContinuousCall()
    {
        using Frame whole = new(new Size(8, 1));
        using Frame chained = new(new Size(8, 1));

        _ = whole.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(15, 0), LinePattern.TripleDash, 0);

        var step = chained.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0), LinePattern.TripleDash, 0);
        _ = chained.Canvas.DrawQuadrantLine(new Point(8, 0), new Point(15, 0), LinePattern.TripleDash, step);

        for (var x = 0; x < 8; x++)
        {
            FrameTests.GetText(chained, new Point(x, 0)).ShouldBe(FrameTests.GetText(whole, new Point(x, 0)), $"cell {x}");
        }
    }

    /// <summary>Verifies the returned step advances by exactly one past the segment's final
    /// point, regardless of pattern, so a caller can chain calls without recomputing geometry.</summary>
    [Fact]
    public void DrawQuadrantLine_WhenSegmentCompletes_ReturnsStepPastTheFinalPoint()
    {
        using Frame frame = new(new Size(4, 1));

        var step = frame.Canvas.DrawQuadrantLine(new Point(0, 0), new Point(7, 0), LinePattern.Solid, 5);

        step.ShouldBe(13);
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
