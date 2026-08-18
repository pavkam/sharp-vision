// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents vertical grouped bars, automatic colors, scaling, and live mutation.</summary>
internal sealed class VerticalBarChartPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "VerticalBarChart";

    /// <summary>Initializes the retained multi-series chart page.</summary>
    internal VerticalBarChartPane()
    {
        var current = new ChartSeries("Current", [
            new ChartDataPoint("Mon", 4),
            new ChartDataPoint("Tue", 7),
            new ChartDataPoint("Wed", 5)]);
        var previous = new ChartSeries("Previous", [
            new ChartDataPoint("Mon", 3),
            new ChartDataPoint("Tue", 5),
            new ChartDataPoint("Wed", 6)])
        {
            Color = new ControlColor(SemanticColor.Info)
        };
        var grouped = new VerticalBarChart
        {
            Width = Length.Cells(46),
            Height = Length.Cells(10),
            Series = [current, previous],
            LegendPlacement = ChartLegendPlacement.Bottom
        };
        var status = new Text("Tuesday: 7");
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            current.Points[1].Value = current.Points[1].Value >= 10 ? 4 : current.Points[1].Value + 1;
            status.Content = FormattableString.Invariant($"Tuesday: {current.Points[1].Value:G}");
        };

        var bounded = new VerticalBarChart
        {
            Width = Length.Cells(46),
            Height = Length.Cells(9),
            Series = [new ChartSeries("Score", [
                new ChartDataPoint("Mon", 2),
                new ChartDataPoint("Tue", 6),
                new ChartDataPoint("Wed", 9)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowValueLabels = true,
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        InitializeContent(new DocPage(
            Title,
            "<info>VerticalBarChart</info> compares grouped category values from a shared scale.",
            new DocSection(
                "🎨",
                "Grouping and scale",
                "Centered bars, complete category labels, and a visible category axis keep the plot readable.",
                new DocExample(
                    "Current and previous week",
                    "Theme colors and the spaced legend distinguish both series; Add data updates Tuesday.",
                    new DocColumn(grouped, new ChartActionRow(add, status)),
                    "var chart = new VerticalBarChart\n{\n    Series = [current, previous],\n    LegendPlacement = ChartLegendPlacement.Bottom,\n};"),
                new DocExample(
                    "Explicit score range",
                    "A fixed 0..10 range and value labels make values directly comparable.",
                    bounded,
                    "chart.Scale = new ChartScale(0, 10, includeZero: true);\nchart.ShowValueLabels = true;"))));
    }
}
