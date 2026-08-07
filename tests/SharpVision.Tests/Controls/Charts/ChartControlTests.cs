// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

using System.Collections.ObjectModel;

/// <summary>Verifies the shared public contract of concrete chart controls.</summary>
public sealed class ChartControlTests
{
    /// <summary>Verifies full charts share documented passive presentation defaults.</summary>
    [ComponentUnitEvidence(typeof(HorizontalBarChart))]
    [ComponentUnitEvidence(typeof(VerticalBarChart))]
    [ComponentUnitEvidence(typeof(LineChart))]
    [ComponentUnitEvidence(typeof(AreaChart))]
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void Constructor_WhenFullChartIsCreated_UsesSharedDefaults(ControlBase control)
    {
        // Arrange and act
        var chart = control.ShouldBeAssignableTo<IChartControl>();

        // Assert
        chart.Series.ShouldBeEmpty();
        chart.Scale.Minimum.ShouldBeNull();
        chart.Scale.Maximum.ShouldBeNull();
        chart.Scale.IncludeZero.ShouldBe(control is HorizontalBarChart or VerticalBarChart);
        chart.LegendPlacement.ShouldBe(ChartLegendPlacement.Automatic);
        chart.ShowCategoryLabels.ShouldBeTrue();
        chart.ShowValueLabels.ShouldBeFalse();
        control.CanFocus.ShouldBeFalse();
        control.HitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies a sparkline uses compact one-series trend defaults.</summary>
    [ComponentUnitEvidence(typeof(Sparkline))]
    [Fact]
    public void Constructor_WhenSparklineIsCreated_UsesCompactDefaults()
    {
        // Arrange and act
        var chart = new Sparkline();

        // Assert
        chart.Series.ShouldBeEmpty();
        chart.Scale.IncludeZero.ShouldBeFalse();
        chart.CanFocus.ShouldBeFalse();
        chart.HitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies an invalid legend value fails before changing state.</summary>
    [Fact]
    public void LegendPlacement_WhenAssignedUnknownValue_RejectsBeforeMutation()
    {
        // Arrange
        var chart = new LineChart();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => chart.LegendPlacement = (ChartLegendPlacement) 99);

        // Assert
        chart.LegendPlacement.ShouldBe(ChartLegendPlacement.Automatic);
    }

    /// <summary>Verifies duplicate series references fail before replacing visible data.</summary>
    [Fact]
    public void Series_WhenReplacementContainsDuplicateReference_RejectsBeforeMutation()
    {
        // Arrange
        var original = new ChartSeries("Original");
        var duplicate = new ChartSeries("Duplicate");
        var chart = new VerticalBarChart { Series = new[] { original } };

        // Act
        _ = Should.Throw<ArgumentException>(() => chart.Series = new[] { duplicate, duplicate });

        // Assert
        chart.Series.ShouldBe([original]);
    }

    /// <summary>Verifies direct observable source membership remains reactive without binding.</summary>
    [Fact]
    public void Series_WhenObservableSourceChanges_RefreshesMembership()
    {
        // Arrange
        var source = new ObservableCollection<ChartSeries>();
        var chart = new HorizontalBarChart { Series = source };
        var series = new ChartSeries("Live");

        // Act
        source.Add(series);

        // Assert
        chart.Series.ShouldBe([series]);
    }

    /// <summary>Verifies an observable sparkline source retains its last valid one-series snapshot.</summary>
    [Fact]
    public void Series_WhenObservableSparklineSourceAddsSecondSeries_RetainsLastValidSnapshot()
    {
        // Arrange
        var first = new ChartSeries("First");
        var source = new ObservableCollection<ChartSeries> { first };
        var chart = new Sparkline { Series = source };

        // Act
        source.Add(new ChartSeries("Second"));

        // Assert
        chart.Series.ShouldBe([first]);
    }

    /// <summary>Supplies every full chart as its concrete control type.</summary>
    public static TheoryData<ControlBase> FullCharts =>
    [
        new HorizontalBarChart(),
        new VerticalBarChart(),
        new LineChart(),
        new AreaChart()
    ];
}
