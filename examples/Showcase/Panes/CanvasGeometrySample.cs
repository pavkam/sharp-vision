// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates clipped public line, circle, and ellipse rasterization.</summary>
internal sealed class CanvasGeometrySample: CanvasSampleBase
{
    /// <summary>Initializes the fixed-size cell-geometry specimen.</summary>
    internal CanvasGeometrySample()
        : base(width: 40, height: 12, minWidth: 38, minHeight: 11, borderStyle: LineStyle.Rounded)
    {
    }

    /// <inheritdoc/>
    protected override void DrawContent(TerminalCanvas canvas, CellStyle style)
    {
        canvas.DrawLine(
            new Point(Bounds.X + 2, Bounds.Y + 2),
            new Point(Bounds.X + 13, Bounds.Y + 7),
            new Rune('/'),
            style);
        canvas.DrawCircle(
            new Point(Bounds.X + 21, Bounds.Y + 5),
            radius: 3,
            new Rune('o'),
            style);
        canvas.DrawEllipse(
            new Rect(Bounds.X + 30, Bounds.Y + 1, 14, 8),
            new Rune('e'),
            style);
        _ = canvas.Draw(
            "line circle · ellipse clips · geometry".AsSpan(),
            new Point(Bounds.X + 1, Bounds.Y + 10),
            style);
    }
}
