// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders one compact series with eight-level lower block glyphs.</summary>
internal static class SparklineRenderer
{
    private static readonly Rune[] _levels =
    [
        new Rune(' '), new Rune('▁'), new Rune('▂'), new Rune('▃'), new Rune('▄'),
        new Rune('▅'), new Rune('▆'), new Rune('▇'), new Rune('█')
    ];

    /// <summary>Renders the most recent points that fit within the chart bounds.</summary>
    internal static void Render(IChartControl chart, TerminalCanvas canvas, TerminalStyle inheritedStyle)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = context.Layout.Plot;

        if (plot.Width == 0 || plot.Height == 0 || chart.Series.Count == 0)
        {
            return;
        }

        var series = chart.Series[0];
        var visible = Math.Min(series.Points.Count, plot.Width);
        var first = series.Points.Count - visible;

        for (var index = 0; index < visible; index++)
        {
            var point = series.Points[first + index];
            var ratio = Math.Clamp(
                (point.Value - context.Range.Minimum) / (context.Range.Maximum - context.Range.Minimum),
                0,
                1);
            var eighths = (int) Math.Round(ratio * plot.Height * 8, MidpointRounding.AwayFromZero);
            var fullCells = eighths / 8;
            var remainder = eighths % 8;
            var x = plot.X + index;
            var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex: 0);

            for (var row = 0; row < fullCells && row < plot.Height; row++)
            {
                canvas.DrawRune(
                    context.Chart.ActualStyle.Glyphs.Bar,
                    new Point(x, plot.Bottom - 1 - row),
                    style,
                    BackgroundMode.Transparent);
            }

            if (remainder > 0 && fullCells < plot.Height)
            {
                canvas.DrawRune(
                    _levels[remainder],
                    new Point(x, plot.Bottom - 1 - fullCells),
                    style,
                    BackgroundMode.Transparent);
            }
        }
    }
}
