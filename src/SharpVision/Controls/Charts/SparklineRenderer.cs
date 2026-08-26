// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders one compact series with eighth-cell column resolution.</summary>
internal static class SparklineRenderer
{
    /// <summary>Renders the most recent points that fit within the chart bounds.</summary>
    /// <remarks>The eighth-block rasterization lives in the canvas <c>DrawBar</c> primitive; this
    /// renderer used to hardcode a private copy of the lower-block table the canvas now owns. The
    /// glyph fill mode instead rounds each column to whole cells of the style's own bar glyph,
    /// exactly as authored.</remarks>
    internal static void Render(IChartControl chart, TerminalCanvas canvas, TerminalStyle inheritedStyle)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = context.Layout.Plot;

        if (plot.Width == 0 || plot.Height == 0 || chart.Series.Count == 0)
        {
            return;
        }

        var fractional = chart.ActualStyle.FillMode == ChartFillMode.Fractional;
        var series = chart.Series[0];
        var visible = Math.Min(series.Points.Count, plot.Width);
        var first = series.Points.Count - visible;

        for (var index = 0; index < visible; index++)
        {
            var point = series.Points[first + index];
            var ratio = ChartRenderer.Ratio(context.Range, point.Value);
            var eighths = ChartRenderer.ScaleEighths(ratio, plot.Height);
            var x = plot.X.Add(index);
            var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex: 0);

            if (fractional)
            {
                canvas.DrawBar(new Point(x, plot.Bottom - 1), BarDirection.Up, eighths, style);
                continue;
            }

            var cells = (int) Math.Round(eighths / 8.0, MidpointRounding.AwayFromZero);

            for (var row = 0; row < cells && row < plot.Height; row++)
            {
                canvas.DrawRune(
                    context.Chart.ActualStyle.Glyphs.Bar,
                    new Point(x, plot.Bottom - 1 - row),
                    style,
                    BackgroundMode.Transparent);
            }
        }
    }
}
