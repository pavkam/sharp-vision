// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates box-drawing topology merging with a deterministic maze pattern.</summary>
internal sealed class CanvasMazeSample: CanvasSampleBase
{
    /// <summary>Initializes the maze specimen.</summary>
    internal CanvasMazeSample()
        : base(width: 40, height: 10, minWidth: 38, minHeight: 9, borderStyle: LineStyle.Heavy)
    {
    }

    /// <inheritdoc/>
    protected override void DrawContent(TerminalCanvas canvas, CellStyle style)
    {
        var left = Bounds.X + 2;
        var top = Bounds.Y + 1;

        canvas.DrawBox(new Rect(left, top, 17, 7), LineStyle.Light, style);
        canvas.DrawVerticalLine(new Point(left + 4, top), 5, LineStyle.Light, style);
        canvas.DrawVerticalLine(new Point(left + 8, top + 2), 5, LineStyle.Light, style);
        canvas.DrawVerticalLine(new Point(left + 12, top), 5, LineStyle.Light, style);
        canvas.DrawHorizontalLine(new Point(left, top + 2), 9, LineStyle.Light, style);
        canvas.DrawHorizontalLine(new Point(left + 4, top + 4), 13, LineStyle.Light, style);

        _ = canvas.Draw("S".AsSpan(), new Point(left + 2, top + 1), style);
        _ = canvas.Draw("E".AsSpan(), new Point(left + 14, top + 5), style);

        _ = canvas.Draw("Topology merge:".AsSpan(), new Point(left + 19, top), style);
        _ = canvas.Draw("Lines auto-join".AsSpan(), new Point(left + 19, top + 1), style);
        _ = canvas.Draw("at intersections".AsSpan(), new Point(left + 19, top + 2), style);
        _ = canvas.Draw("producing ┼ ├ ┤".AsSpan(), new Point(left + 19, top + 3), style);
        _ = canvas.Draw("┬ ┴ and corners".AsSpan(), new Point(left + 19, top + 4), style);
        _ = canvas.Draw("deterministically".AsSpan(), new Point(left + 19, top + 5), style);
    }
}
