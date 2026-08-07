// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;

using SharpVision.DataBinding;

/// <summary>Verifies one-way chart-series binding and observable collection updates.</summary>
public sealed class ChartBindingTests
{
    /// <summary>Verifies binding copies the initial series snapshot into every chart family.</summary>
    [Theory]
    [MemberData(nameof(Charts))]
    public void Bind_WhenStarted_AppliesInitialSeries(ControlBase control)
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var model = new ChartBindingModel { Series = new ObservableCollection<ChartSeries> { series } };
        var chart = control.ShouldBeAssignableTo<IChartControl>();

        // Act
        using var binding = Bind(control, model);

        // Assert
        chart.Series.ShouldBe([series]);
    }

    /// <summary>Verifies observable membership changes refresh the chart series snapshot.</summary>
    [Fact]
    public void Bind_WhenObservableSeriesAddsItem_UpdatesChart()
    {
        // Arrange
        var model = new ChartBindingModel();
        var chart = new LineChart();
        using var binding = chart.Bind(model, source => source.Series);
        var series = new ChartSeries("Live");

        // Act
        model.ObservableSeries.Add(series);

        // Assert
        chart.Series.ShouldBe([series]);
    }

    /// <summary>Verifies replacing the source property atomically replaces chart membership.</summary>
    [Fact]
    public void Bind_WhenSourcePropertyIsReplaced_ReplacesChartSeries()
    {
        // Arrange
        var original = new ChartSeries("Original");
        var replacement = new ChartSeries("Replacement");
        var model = new ChartBindingModel
        {
            Series = new ObservableCollection<ChartSeries> { original }
        };
        var chart = new AreaChart();
        using var binding = chart.Bind(model, source => source.Series);

        // Act
        model.Series = new ObservableCollection<ChartSeries> { replacement };

        // Assert
        chart.Series.ShouldBe([replacement]);
    }

    /// <summary>Verifies a null source maps to an empty chart rather than a null target.</summary>
    [Fact]
    public void Bind_WhenSourceBecomesNull_ClearsChartSeries()
    {
        // Arrange
        var model = new ChartBindingModel
        {
            Series = new ObservableCollection<ChartSeries> { new("Original") }
        };
        var chart = new VerticalBarChart();
        using var binding = chart.Bind(model, source => source.Series);

        // Act
        model.Series = null;

        // Assert
        chart.Series.ShouldBeEmpty();
    }

    /// <summary>Supplies all public chart target types.</summary>
    public static TheoryData<ControlBase> Charts =>
    [
        new HorizontalBarChart(),
        new VerticalBarChart(),
        new LineChart(),
        new AreaChart(),
        new Sparkline()
    ];

    private static Binding Bind(ControlBase control, ChartBindingModel model) => control switch
    {
        HorizontalBarChart chart => chart.Bind(model, source => source.Series),
        VerticalBarChart chart => chart.Bind(model, source => source.Series),
        LineChart chart => chart.Bind(model, source => source.Series),
        AreaChart chart => chart.Bind(model, source => source.Series),
        Sparkline chart => chart.Bind(model, source => source.Series),
        _ => throw new ArgumentOutOfRangeException(nameof(control), control, "The chart target is unknown.")
    };
}
