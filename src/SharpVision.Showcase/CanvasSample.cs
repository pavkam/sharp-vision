using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;

namespace SharpVision.Showcase;

/// <summary>Demonstrates low-level Canvas topology, shade, and quadrant primitives.</summary>
internal sealed class CanvasSample: Control
{
    /// <summary>Initializes the fixed-size drawing sample.</summary>
    internal CanvasSample()
    {
        Width = Length.Cells(28);
        Height = Length.Cells(7);
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        _ = constraint;
        return new Size(28, 7);
    }

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        canvas.DrawBox(Bounds, LineStyle.Rounded);

        if (Bounds.Width < 24 || Bounds.Height < 7)
        {
            return;
        }

        canvas.DrawHorizontalLine(
            new Point(Bounds.X + 2, Bounds.Y + 3),
            Math.Max(0, Bounds.Width - 4),
            LineStyle.Light);
        canvas.DrawVerticalLine(
            new Point(Bounds.X + 8, Bounds.Y + 1),
            Math.Max(0, Bounds.Height - 2),
            LineStyle.Heavy);
        canvas.FillShade(
            new Rect(Bounds.X + 12, Bounds.Y + 1, Math.Min(3, Bounds.Width - 12), 1),
            Shade.Light);
        canvas.FillShade(
            new Rect(Bounds.X + 16, Bounds.Y + 1, Math.Min(3, Bounds.Width - 16), 1),
            Shade.Medium);
        canvas.FillShade(
            new Rect(Bounds.X + 20, Bounds.Y + 1, Math.Min(3, Bounds.Width - 20), 1),
            Shade.Dark);
        canvas.DrawQuadrants(
            new Point(Bounds.X + 12, Bounds.Y + 5),
            Quadrants.UpperLeft | Quadrants.LowerRight);
        canvas.DrawQuadrants(
            new Point(Bounds.X + 14, Bounds.Y + 5),
            Quadrants.UpperRight | Quadrants.LowerLeft);
    }
}
