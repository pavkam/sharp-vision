// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates a mini dashboard combining boxes, bars, labels, and shade fills.</summary>
internal sealed class CanvasDashboardSample: ControlBase
{
    /// <summary>Initializes the dashboard specimen.</summary>
    internal CanvasDashboardSample()
    {
        Width = Length.Cells(44);
        Height = Length.Cells(12);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(44, 12);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 40 || Bounds.Height < 11)
        {
            return;
        }

        _ = canvas.Draw("System Monitor".AsSpan(), new Point(Bounds.X + 2, Bounds.Y + 1), style);
        canvas.DrawHorizontalLine(new Point(Bounds.X + 1, Bounds.Y + 2), Bounds.Width - 2, LineStyle.Light, style);

        DrawGauge(canvas, style, Bounds.X + 2, Bounds.Y + 3, "CPU", 72);
        DrawGauge(canvas, style, Bounds.X + 2, Bounds.Y + 5, "MEM", 45);
        DrawGauge(canvas, style, Bounds.X + 2, Bounds.Y + 7, "DSK", 88);

        canvas.DrawVerticalLine(new Point(Bounds.X + 26, Bounds.Y + 3), 7, LineStyle.Light, style);

        _ = canvas.Draw("Uptime".AsSpan(), new Point(Bounds.X + 28, Bounds.Y + 3), style);
        _ = canvas.Draw("14d 7h".AsSpan(), new Point(Bounds.X + 28, Bounds.Y + 4), style);
        _ = canvas.Draw("Net I/O".AsSpan(), new Point(Bounds.X + 28, Bounds.Y + 6), style);
        _ = canvas.Draw("↑ 42 MB".AsSpan(), new Point(Bounds.X + 28, Bounds.Y + 7), style);
        _ = canvas.Draw("↓ 1.2 GB".AsSpan(), new Point(Bounds.X + 28, Bounds.Y + 8), style);

        canvas.DrawHorizontalLine(new Point(Bounds.X + 1, Bounds.Y + 10), Bounds.Width - 2, LineStyle.Light, style);
    }

    private static void DrawGauge(TerminalCanvas canvas, CellStyle style, int x, int y, string label, int percent)
    {
        _ = canvas.Draw($"{label} ".AsSpan(), new Point(x, y), style);
        var barStart = x + 4;
        var barWidth = 16;
        var filled = (int) (barWidth * percent / 100.0);
        canvas.FillShade(new Rect(barStart, y, filled, 1), Shade.Solid, style);
        canvas.FillShade(new Rect(barStart + filled, y, barWidth - filled, 1), Shade.Light, style);
        _ = canvas.Draw($" {percent}%".AsSpan(), new Point(barStart + barWidth, y), style);
    }
}
