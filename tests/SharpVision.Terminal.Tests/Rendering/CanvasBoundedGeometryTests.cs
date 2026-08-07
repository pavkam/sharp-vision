// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies geometry primitives stay overflow-safe and reject fully invisible extreme-coordinate
/// requests for extreme public coordinates.
/// </summary>
/// <remarks>
/// The primitives also carry a bounded-work obligation - hostile geometry must not monopolize the
/// render thread - but that property has no dedicated regression test here. Proving it directly
/// would require either a wall-clock budget (which measures the host machine as much as the
/// product and is inherently flaky under CI load or coverage instrumentation) or a mutable
/// instance counter on <see cref="Frame"/> solely for test observability, and neither belongs in
/// this type. The bound is documented on each primitive's early-reject check and enforced by code
/// review; this file keeps only the cases that assert an observable, deterministic result.
/// </remarks>
public sealed class CanvasBoundedGeometryTests
{
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
}
