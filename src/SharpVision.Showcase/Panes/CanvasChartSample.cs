// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates a deterministic responsive chart drawn directly into semantic cells.</summary>
internal sealed class CanvasChartSample: Control
{
    private static readonly int[] _values = [2, 4, 3, 6, 5, 7];

    /// <summary>Initializes the chart sample.</summary>
    internal CanvasChartSample()
    {
        Width = Length.Cells(36);
        Height = Length.Cells(9);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(36, 9);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 20 || Bounds.Height < 7)
        {
            return;
        }

        _ = canvas.Draw("CPU · deterministic samples".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 1), style);
        var baseline = Bounds.Bottom - 2;
        canvas.DrawHorizontalLine(new Point(Bounds.X + 2, baseline), Bounds.Width - 4, LineStyle.Light, style);

        for (var index = 0; index < _values.Length; index++)
        {
            var height = Math.Min(_values[index], Math.Max(0, Bounds.Height - 4));
            var x = Bounds.X + 3 + (index * 4);
            canvas.FillShade(new Rect(x, baseline - height, 2, height), Shade.Solid, style);
        }
    }
}
