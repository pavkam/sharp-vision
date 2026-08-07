// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates clipped public line, circle, and ellipse rasterization.</summary>
internal sealed class CanvasGeometrySample: ControlBase
{
    /// <summary>Initializes the fixed-size cell-geometry specimen.</summary>
    internal CanvasGeometrySample()
    {
        Width = Length.Cells(40);
        Height = Length.Cells(12);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(40, 12);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
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
