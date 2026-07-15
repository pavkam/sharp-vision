// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates shade fills and merged quadrant-block drawing.</summary>
internal sealed class CanvasShadeSample: Control
{
    /// <summary>Initializes the shade and quadrant sample.</summary>
    internal CanvasShadeSample()
    {
        Width = Length.Cells(36);
        Height = Length.Cells(6);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(36, 6);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 36 || Bounds.Height < 6)
        {
            return;
        }

        canvas.FillShade(new Rect(Bounds.X + 2, Bounds.Y + 1, 1, 1), Shade.Light, style);
        canvas.FillShade(new Rect(Bounds.X + 3, Bounds.Y + 1, 1, 1), Shade.Medium, style);
        canvas.FillShade(new Rect(Bounds.X + 4, Bounds.Y + 1, 1, 1), Shade.Dark, style);
        _ = canvas.Draw("light / medium / dark".AsSpan(), new Point(Bounds.X + 7, Bounds.Y + 1), style);
        canvas.DrawQuadrants(
            new Point(Bounds.X + 2, Bounds.Y + 3),
            Quadrants.UpperLeft | Quadrants.LowerRight,
            style);
        canvas.DrawQuadrants(
            new Point(Bounds.X + 4, Bounds.Y + 3),
            Quadrants.UpperRight | Quadrants.LowerLeft,
            style);
        _ = canvas.Draw("quadrants merge per cell".AsSpan(), new Point(Bounds.X + 7, Bounds.Y + 3), style);
    }
}
