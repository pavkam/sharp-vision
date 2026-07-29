// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies geometry primitives stay bounded and overflow-safe for extreme public coordinates.
/// </summary>
/// <remarks>
/// The primitives document clipped output, but the normative obligation is bounded work: hostile
/// geometry must not monopolize the render thread. Each test therefore asserts both the drawn
/// result and that the call completes well inside a wall-clock budget that a per-coordinate loop
/// could never meet.
/// </remarks>
public sealed class CanvasBoundedGeometryTests
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Verifies a segment spanning the entire integer range on an invisible row returns without
    /// walking one iteration per integer step.
    /// </summary>
    [Fact]
    public void DrawLine_WhenEndpointsSpanTheIntegerRange_CompletesWithinBoundedWork()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawLine(
            new Point(int.MinValue, -1),
            new Point(int.MaxValue, -1),
            new Rune('*')));

        elapsed.ShouldBeLessThan(_budget);
        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies an extreme segment that genuinely crosses the clip still paints the exact visible
    /// cells, so the bounded traversal did not change what a caller sees.
    /// </summary>
    [Fact]
    public void DrawLine_WhenExtremeSegmentCrossesClip_PaintsVisibleIntersection()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawLine(
            new Point(int.MinValue, 1),
            new Point(int.MaxValue, 1),
            new Rune('-')));

        elapsed.ShouldBeLessThan(_budget);
        CanvasPrimitiveTests.AssertRows(frame, "      ", "------", "      ");
    }

    /// <summary>Verifies a fully invisible segment writes nothing at all.</summary>
    [Fact]
    public void DrawLine_WhenGeometryIsFullyOutsideClip_WritesNoCells()
    {
        using Frame frame = new(new Size(6, 3));
        var canvas = frame.Canvas.Clip(new Rect(1, 1, 2, 1));

        canvas.DrawLine(new Point(0, 0), new Point(5, 0), new Rune('*'));

        AssertBlank(frame);
    }

    /// <summary>Verifies a maximum radius cannot spin the rasterizer over billions of octants.</summary>
    [Fact]
    public void DrawCircle_WhenRadiusIsMaximum_CompletesWithinBoundedWork()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawCircle(
            new Point(0, 0),
            int.MaxValue,
            new Rune('*')));

        elapsed.ShouldBeLessThan(_budget);
        AssertBlank(frame);
    }

    /// <summary>Verifies a maximum radius centred far outside the frame is rejected outright.</summary>
    [Fact]
    public void DrawCircle_WhenCentreAndRadiusAreExtreme_CompletesWithinBoundedWork()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawCircle(
            new Point(int.MinValue, int.MinValue),
            int.MaxValue,
            new Rune('*')));

        elapsed.ShouldBeLessThan(_budget);
        AssertBlank(frame);
    }

    /// <summary>
    /// Verifies bounds past the cubic Int64 overflow threshold rasterize without wrapping their
    /// error terms, which previously could stop the horizontal bounds advancing and never
    /// terminate.
    /// </summary>
    [Fact]
    public void DrawEllipse_WhenBoundsExceedOverflowThreshold_RasterizesWithoutIntermediateOverflow()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawEllipse(
            new Rect(-1_500_000, -1_500_000, 3_000_000, 3_000_000),
            new Rune('*')));

        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    /// <summary>Verifies a fully invisible ellipse is rejected before any dimension-proportional work.</summary>
    [Fact]
    public void DrawEllipse_WhenBoundsAreFullyOutsideClip_CompletesWithinBoundedWork()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() => frame.Canvas.DrawEllipse(
            new Rect(1_000_000, 1_000_000, 2_000_000_000, 2_000_000_000),
            new Rune('*')));

        elapsed.ShouldBeLessThan(_budget);
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

    /// <summary>Verifies an axis line far past the frame costs no per-cell work and draws nothing.</summary>
    [Fact]
    public void DrawAxisLines_WhenLengthIsMaximum_CompleteWithinBoundedWork()
    {
        using Frame frame = new(new Size(6, 3));

        var elapsed = Measure(() =>
        {
            frame.Canvas.DrawHorizontalLine(new Point(0, 9), int.MaxValue, LineStyle.Light);
            frame.Canvas.DrawVerticalLine(new Point(9, 0), int.MaxValue, LineStyle.Light);
        });

        elapsed.ShouldBeLessThan(_budget);
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

    private static TimeSpan Measure(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();

        return Stopwatch.GetElapsedTime(start);
    }
}
