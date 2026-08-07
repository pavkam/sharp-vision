// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Provides common scale, color, legend, and coordinate chart rendering.</summary>
internal static class ChartRenderer
{
    /// <summary>Creates resolved shared inputs and draws a fitting legend.</summary>
    internal static ChartRenderContext CreateContext(
        IChartControl chart,
        TerminalCanvas canvas,
        TerminalStyle inheritedStyle)
    {
        var layout = ResolveLayout(chart);
        var values = CollectValues(chart.Series);
        var range = ChartScaleResolver.Resolve(chart.Scale, values);
        var context = new ChartRenderContext(chart, layout, range, inheritedStyle);
        RenderLegend(context, canvas);
        return context;
    }

    /// <summary>Resolves the authored point, series, or palette color in precedence order.</summary>
    internal static ControlColor ResolveColor(
        ChartStyle style,
        ChartSeries series,
        ChartDataPoint point,
        int seriesIndex) => point.Color ?? series.Color ?? style.GetSeriesColor(seriesIndex);

    /// <summary>Maps a finite value to an inclusive vertical plot cell.</summary>
    internal static int MapY(ChartScaleRange range, double value, Rect plot)
    {
        if (plot.Height <= 1)
        {
            return plot.Y;
        }

        var ratio = Math.Clamp((value - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
        return plot.Bottom - 1 - (int) Math.Round(ratio * (plot.Height - 1), MidpointRounding.AwayFromZero);
    }

    /// <summary>Maps one ordered point index to an inclusive horizontal plot cell.</summary>
    internal static int MapX(int index, int count, Rect plot)
    {
        return plot.Width <= 1 || count <= 1
            ? plot.X
            : plot.X + (int) Math.Round(
                (double) index / (count - 1) * (plot.Width - 1),
                MidpointRounding.AwayFromZero);
    }

    /// <summary>Maps one ordered point to inclusive half-cell coordinates, doubling both axes'
    /// resolution for the quadrant rasterizer. The extrema land on the same cells MapX and MapY
    /// choose, so markers and labels agree with the drawn line.</summary>
    internal static Point MapHalf(ChartScaleRange range, int index, int count, double value, Rect plot)
    {
        var x = plot.Width <= 1 || count <= 1
            ? plot.X * 2
            : (plot.X * 2) + (int) Math.Round(
                (double) index / (count - 1) * ((plot.Width * 2) - 1),
                MidpointRounding.AwayFromZero);
        var ratio = Math.Clamp((value - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
        var y = plot.Height <= 1
            ? plot.Y * 2
            : (plot.Bottom * 2) - 1 - (int) Math.Round(
                ratio * ((plot.Height * 2) - 1),
                MidpointRounding.AwayFromZero);
        return new Point(x, y);
    }

    /// <summary>Creates a terminal style using the resolved authored series color.</summary>
    internal static TerminalStyle ResolveSeriesStyle(
        ChartRenderContext context,
        ChartSeries series,
        ChartDataPoint point,
        int seriesIndex)
    {
        var value = ResolveColor(context.Chart.ActualStyle, series, point, seriesIndex);
        var color = ControlBase.ResolveColor(value, context.Chart.Control.Theme);
        return context.InheritedStyle.WithForeground(color);
    }

    /// <summary>Creates a terminal style using the chart's resolved label color.</summary>
    internal static TerminalStyle ResolveLabelStyle(ChartRenderContext context) =>
        context.InheritedStyle.WithForeground(
            ControlBase.ResolveColor(context.Chart.ActualStyle.LabelColor, context.Chart.Control.Theme));

    /// <summary>Creates a terminal style using the chart's resolved axis color.</summary>
    internal static TerminalStyle ResolveAxisStyle(ChartRenderContext context) =>
        context.InheritedStyle.WithForeground(
            ControlBase.ResolveColor(context.Chart.ActualStyle.AxisColor, context.Chart.Control.Theme));

    // The axis rules sit beside AxisColor, which already made this part theme-owned; only the glyph
    // half was still a literal, so a theme could recolour an axis it could not replace.
    internal static Rune ResolveVerticalAxisGlyph(ChartRenderContext context) =>
        context.Chart.ActualStyle.Glyphs.VerticalAxis;

    internal static Rune ResolveHorizontalAxisGlyph(ChartRenderContext context) =>
        context.Chart.ActualStyle.Glyphs.HorizontalAxis;

    /// <summary>Renders one complete label centered within a bounded row.</summary>
    internal static void RenderCenteredLabel(
        ChartRenderContext context,
        TerminalCanvas canvas,
        string label,
        int centerX,
        int y,
        Rect bounds)
    {
        if (label.Length == 0 || bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var width = context.Chart.Control.MeasureCells(label.AsSpan());
        var x = Math.Clamp(
            centerX - (width / 2),
            bounds.X,
            Math.Max(bounds.X, bounds.Right - width));
        _ = canvas.Clip(bounds).Draw(
            label.AsSpan(),
            new Point(x, y),
            ResolveLabelStyle(context),
            background: BackgroundMode.Transparent);
    }

    /// <summary>Reserves and renders a one-row horizontal category domain when it fits.</summary>
    internal static Rect ReserveHorizontalCategoryLabels(ChartRenderContext context, TerminalCanvas canvas)
    {
        var plot = context.Layout.Plot;

        if (!context.Chart.ShowCategoryLabels || plot.Height < 2 || context.Chart.Series.Count == 0 ||
            context.Chart.Series[0].Points.Count == 0)
        {
            return plot;
        }

        var points = context.Chart.Series[0].Points;
        for (var index = 0; index < points.Count; index++)
        {
            var x = MapX(index, points.Count, plot);
            RenderCenteredLabel(
                context,
                canvas,
                points[index].Label,
                x,
                plot.Bottom - 1,
                new Rect(plot.X, plot.Bottom - 1, plot.Width, 1));
        }

        return new Rect(plot.X, plot.Y, plot.Width, plot.Height - 1);
    }

    /// <summary>Renders one finite value beside a point without replacing its marker.</summary>
    internal static void RenderValueLabel(
        ChartRenderContext context,
        TerminalCanvas canvas,
        ChartDataPoint point,
        Point marker,
        Rect plot)
    {
        if (!context.Chart.ShowValueLabels)
        {
            return;
        }

        var value = point.Value.ToString("G", CultureInfo.InvariantCulture);
        var width = context.Chart.Control.MeasureCells(value.AsSpan());
        var x = marker.X + 1;

        if (x + width > plot.Right)
        {
            x = marker.X - width;
        }

        if (x < plot.X || width == 0)
        {
            return;
        }

        _ = canvas.Clip(plot).Draw(
            value.AsSpan(),
            new Point(x, marker.Y),
            ResolveLabelStyle(context),
            background: BackgroundMode.Transparent);
    }

    private static double[] CollectValues(IReadOnlyList<ChartSeries> series)
    {
        var count = 0;

        foreach (var item in series)
        {
            count += item.Points.Count;
        }

        var values = new double[count];
        var offset = 0;

        foreach (var item in series)
        {
            foreach (var point in item.Points)
            {
                values[offset++] = point.Value;
            }
        }

        return values;
    }

    private static ChartPlotLayout ResolveLayout(IChartControl chart)
    {
        var bounds = chart.Control.Bounds;
        var placement = ResolveLegendPlacement(chart);

        return placement == ChartLegendPlacement.Hidden || bounds.Width < 4 || bounds.Height < 2
            ? new ChartPlotLayout(bounds, default)
            : placement switch
            {
                ChartLegendPlacement.Top => new ChartPlotLayout(
                    new Rect(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 1),
                    new Rect(bounds.X, bounds.Y, bounds.Width, 1)),
                ChartLegendPlacement.Left when bounds.Width >= 8 => new ChartPlotLayout(
                    new Rect(bounds.X + Math.Min(12, bounds.Width / 3), bounds.Y, bounds.Width - Math.Min(12, bounds.Width / 3), bounds.Height),
                    new Rect(bounds.X, bounds.Y, Math.Min(12, bounds.Width / 3), bounds.Height)),
                ChartLegendPlacement.Right when bounds.Width >= 8 => new ChartPlotLayout(
                    new Rect(bounds.X, bounds.Y, bounds.Width - Math.Min(12, bounds.Width / 3), bounds.Height),
                    new Rect(bounds.Right - Math.Min(12, bounds.Width / 3), bounds.Y, Math.Min(12, bounds.Width / 3), bounds.Height)),
                ChartLegendPlacement.Automatic or ChartLegendPlacement.Bottom or
                    ChartLegendPlacement.Left or ChartLegendPlacement.Right => bounds.Height >= 3
                        ? new ChartPlotLayout(
                            new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height - 2),
                            new Rect(bounds.X, bounds.Bottom - 1, bounds.Width, 1))
                        : new ChartPlotLayout(
                            new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height - 1),
                            new Rect(bounds.X, bounds.Bottom - 1, bounds.Width, 1)),
                ChartLegendPlacement.Hidden => new ChartPlotLayout(bounds, default),
                _ => throw new UnreachableException()
            };
    }

    private static ChartLegendPlacement ResolveLegendPlacement(IChartControl chart)
    {
        if (chart.LegendPlacement != ChartLegendPlacement.Automatic)
        {
            return chart.LegendPlacement;
        }

        var named = 0;

        foreach (var series in chart.Series)
        {
            if (series.Name.Length > 0)
            {
                named++;
            }
        }

        return named >= 2 ? ChartLegendPlacement.Bottom : ChartLegendPlacement.Hidden;
    }

    private static void RenderLegend(ChartRenderContext context, TerminalCanvas canvas)
    {
        var bounds = context.Layout.Legend;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        if (bounds.Y > context.Layout.Plot.Bottom)
        {
            canvas.DrawLine(
                new Point(bounds.X, bounds.Y - 1),
                new Point(bounds.Right - 1, bounds.Y - 1),
                ResolveHorizontalAxisGlyph(context),
                ResolveAxisStyle(context));
        }

        var x = bounds.X;
        var y = bounds.Y;

        for (var index = 0; index < context.Chart.Series.Count; index++)
        {
            var series = context.Chart.Series[index];

            if (series.Name.Length == 0 || y >= bounds.Bottom)
            {
                continue;
            }

            var markerColor = series.Color ?? context.Chart.ActualStyle.GetSeriesColor(index);
            var markerStyle = context.InheritedStyle.WithForeground(
                ControlBase.ResolveColor(markerColor, context.Chart.Control.Theme));

            if (x + 3 > bounds.Right)
            {
                x = bounds.X;
                y++;
            }

            if (y >= bounds.Bottom)
            {
                break;
            }

            canvas.DrawRune(context.Chart.ActualStyle.Glyphs.LegendMarker, new Point(x, y), markerStyle);
            x += 2;
            var available = bounds.Right - x;

            if (available <= 0)
            {
                break;
            }

            var written = canvas.Clip(new Rect(x, y, available, 1)).Draw(
                series.Name.AsSpan(),
                new Point(x, y),
                context.InheritedStyle,
                background: BackgroundMode.Transparent);
            x += Math.Min(written.Cells, available) + 1;
        }
    }
}
