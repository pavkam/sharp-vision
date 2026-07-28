// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates sub-cell sparkline rendering using Unicode block elements.</summary>
internal sealed class CanvasSparklineSample: Control
{
    private static readonly double[] _values =
        [3, 5, 2, 7, 4, 8, 6, 3, 5, 9, 7, 4, 6, 8, 5, 3, 7, 6, 4, 8, 9, 7, 5, 3, 6, 8, 7, 5];

    private static readonly string[] _blocks = [" ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];

    /// <summary>Initializes the sparkline specimen.</summary>
    internal CanvasSparklineSample()
    {
        Width = Length.Cells(40);
        Height = Length.Cells(8);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(40, 8);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 10 || Bounds.Height < 6)
        {
            return;
        }

        _ = canvas.Draw("Sparkline · sub-cell resolution".AsSpan(), new Point(Bounds.X + 2, Bounds.Y + 1), style);

        var chartLeft = Bounds.X + 2;
        var chartBottom = Bounds.Bottom - 2;
        var chartHeight = Bounds.Height - 4;
        var available = Math.Min(_values.Length, Bounds.Width - 4);
        var max = 0.0;

        for (var index = 0; index < available; index++)
        {
            max = Math.Max(max, _values[index]);
        }

        if (max <= 0)
        {
            return;
        }

        for (var index = 0; index < available; index++)
        {
            var ratio = _values[index] / max;
            var totalEighths = (int) (ratio * chartHeight * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var row = 0; row < fullCells && row < chartHeight; row++)
            {
                _ = canvas.Draw(
                    _blocks[8].AsSpan(),
                    new Point(chartLeft + index, chartBottom - row),
                    style,
                    background: BackgroundMode.Transparent);
            }

            if (remainder > 0 && fullCells < chartHeight)
            {
                _ = canvas.Draw(
                    _blocks[remainder].AsSpan(),
                    new Point(chartLeft + index, chartBottom - fullCells),
                    style,
                    background: BackgroundMode.Transparent);
            }
        }

        canvas.DrawHorizontalLine(
            new Point(chartLeft, chartBottom + 1),
            available,
            LineStyle.Light,
            style);
    }
}
