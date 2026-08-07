// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.DataBinding;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents line charts with binding, scales, colors, legends, and deep mutations.</summary>
internal sealed class LineChartPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "LineChart";

    /// <summary>Initializes the retained bound line-chart page.</summary>
    internal LineChartPane()
    {
        var model = new ChartShowcaseModel();
        var cpu = new ChartSeries("CPU", Wave(amplitude: 14, phase: 0, drift: 0.4, offset: 46));
        model.Add(cpu);
        var bound = new LineChart
        {
            Width = Length.Percent(100),
            Height = Length.Cells(11),
            LegendPlacement = ChartLegendPlacement.Bottom
        };
        _ = bound.Bind(model, source => source.Series);
        var status = new Text("1 bound series");
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            if (model.Series?.Count == 1)
            {
                model.Add(new ChartSeries("Memory", Wave(amplitude: 9, phase: 1.9, drift: 0.7, offset: 38)));
                status.Content = "2 bound series · legend added";
            }
            else
            {
                cpu.Points[^1].Value += 2;
                status.Content = FormattableString.Invariant($"CPU latest: {cpu.Points[^1].Value:G}");
            }
        };

        var narrowRange = new LineChart
        {
            Width = Length.Cells(54),
            Height = Length.Cells(8),
            Series = [new ChartSeries("Temperature", [
                new ChartDataPoint("09", 18.4),
                new ChartDataPoint("10", 19.1),
                new ChartDataPoint("11", 18.8),
                new ChartDataPoint("12", 19.7)])],
            Scale = new ChartScale(18, 20, includeZero: false),
            ShowValueLabels = true
        };

        var requests = new ChartSeries("Requests", [
            new ChartDataPoint("09", 82), new ChartDataPoint("10", 91),
            new ChartDataPoint("11", 88), new ChartDataPoint("12", 98)]);
        var errors = new ChartSeries("Errors", [
            new ChartDataPoint("09", 14), new ChartDataPoint("10", 9),
            new ChartDataPoint("11", 12), new ChartDataPoint("12", 6)])
        {
            Color = new ControlColor(SemanticColor.Error)
        };
        var colored = new LineChart
        {
            Width = Length.Cells(54),
            Height = Length.Cells(8),
            Series = [requests, errors],
            LegendPlacement = ChartLegendPlacement.Right
        };

        InitializeContent(new DocPage(
            Title,
            "<info>LineChart</info> shows ordered trends without forcing an automatic zero baseline.",
            new DocSection(
                "🔄",
                "Binding, scale, and color",
                "Complete labels and spaced legends remain readable while bound collections and points update in place.",
                new DocExample(
                    "CPU and memory trend",
                    "Add Memory through the bound collection; later activations append CPU data in place.",
                    new DocColumn(bound, new ChartActionRow(add, status)),
                    "_ = chart.Bind(model, source => source.Series);\nmodel.Add(memory);"),
                new DocExample(
                    "Narrow explicit range",
                    "An 18..20 range preserves small temperature variation and labels each marker.",
                    narrowRange,
                    "chart.Scale = new ChartScale(18, 20, includeZero: false);\nchart.ShowValueLabels = true;"),
                new DocExample(
                    "Semantic error color",
                    "A right legend identifies the request and error lines while errors use the theme's semantic color.",
                    colored,
                    "errors.Color = new ControlColor(SemanticColor.Error);\nchart.LegendPlacement = ChartLegendPlacement.Right;"))));
    }

    // A smooth 24-point waveform: enough density that the half-cell segment rasterization reads
    // as a curve rather than a connect-the-dots. Only every sixth point carries a label, so the
    // category row shows clean hour marks instead of two dozen colliding ones.
    private static ChartDataPoint[] Wave(double amplitude, double phase, double drift, double offset)
    {
        var points = new ChartDataPoint[24];

        for (var index = 0; index < points.Length; index++)
        {
            var label = index % 6 == 0
                ? FormattableString.Invariant($"{9 + (index / 6):00}")
                : string.Empty;
            points[index] = new ChartDataPoint(
                label,
                offset + (amplitude * Math.Sin((index * 0.48) + phase)) + (index * drift));
        }

        return points;
    }
}
