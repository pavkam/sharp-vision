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
        var cpu = new ChartSeries("CPU", [
            new ChartDataPoint("09", 41),
            new ChartDataPoint("10", 44),
            new ChartDataPoint("11", 43),
            new ChartDataPoint("12", 48)]);
        model.Add(cpu);
        var bound = new LineChart
        {
            Width = Length.Cells(54),
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
                model.Add(new ChartSeries("Memory", [
                    new ChartDataPoint("09", 35),
                    new ChartDataPoint("10", 39),
                    new ChartDataPoint("11", 42),
                    new ChartDataPoint("12", 46)]));
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
}
