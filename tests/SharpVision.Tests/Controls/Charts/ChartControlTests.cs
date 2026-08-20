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
        control.IsHitTestVisible.ShouldBeFalse();
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
        chart.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies a local Style overrides the chart's default palette and clearing it
    /// restores the theme-owned default, exercising ChartControlBase's own Style/ActualStyle
    /// round trip shared by every concrete chart family.</summary>
    [Fact]
    public void Style_WhenCustomized_OverridesDefaultsAndClearingRestores()
    {
        // Arrange
        var chart = new LineChart();
        var defaultStyle = chart.ActualStyle;

        // Act
        chart.Style = defaultStyle with { PrimaryColor = Color.Rgb(10, 20, 30) };

        // Assert custom
        _ = chart.Style.ShouldNotBeNull();
        chart.ActualStyle.PrimaryColor.ShouldBe((ControlColor) Color.Rgb(10, 20, 30));

        // Act reset
        chart.Style = null;

        // Assert restored
        chart.Style.ShouldBeNull();
        chart.ActualStyle.ShouldBe(defaultStyle);
    }

    /// <summary>Verifies assigning a genuinely different Scale round-trips and invalidates
    /// rendering, and that re-assigning the identical value afterward is a no-op notification.</summary>
    [Fact]
    public void Scale_WhenChangedAtRuntime_RoundTripsAndInvalidatesRender()
    {
        // Arrange
        var chart = new LineChart();
        new LayoutEngine().Layout(chart, new Size(10, 5));
        using (var frame = new Frame(new Size(10, 5)))
        {
            chart.Render(frame.Canvas);
        }

        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        var newScale = new ChartScale(0, 100, includeZero: false);

        // Act
        chart.Scale = newScale;

        // Assert
        chart.Scale.ShouldBe(newScale);
        chart.Pending.ShouldBe(Invalidation.Render);
        notifications.ShouldBe([nameof(ChartControlBase.Scale)]);

        // Act: re-assigning the identical value is a no-op.
        chart.Scale = newScale;

        // Assert
        notifications.ShouldBe([nameof(ChartControlBase.Scale)]);
    }

    /// <summary>Verifies each full chart's own settable LegendPlacement - not just the shared
    /// IChartControl-reported value every family forwards it through - rejects an unknown value
    /// before changing state.</summary>
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void LegendPlacement_WhenAssignedUnknownValue_RejectsBeforeMutation(ControlBase control)
    {
        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => SetLegendPlacement(control, (ChartLegendPlacement) 99));
        GetLegendPlacement(control).ShouldBe(ChartLegendPlacement.Automatic);
    }

    /// <summary>Verifies each full chart's own settable LegendPlacement round-trips a valid value
    /// and invalidates the documented measure phase - not just the shared IChartControl-reported
    /// snapshot every family forwards it through.</summary>
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void LegendPlacement_WhenChangedToValidValue_RoundTripsAndInvalidatesMeasure(ControlBase control)
    {
        // Arrange
        new LayoutEngine().Layout(control, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            control.Render(frame.Canvas);
        }

        // Act
        SetLegendPlacement(control, ChartLegendPlacement.Hidden);

        // Assert
        GetLegendPlacement(control).ShouldBe(ChartLegendPlacement.Hidden);
        control.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies each full chart's own settable ShowCategoryLabels round-trips and
    /// invalidates the documented measure phase.</summary>
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void ShowCategoryLabels_WhenChanged_RoundTripsAndInvalidatesMeasure(ControlBase control)
    {
        // Arrange
        new LayoutEngine().Layout(control, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            control.Render(frame.Canvas);
        }

        // Act
        SetShowCategoryLabels(control, false);

        // Assert
        GetShowCategoryLabels(control).ShouldBeFalse();
        control.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies each full chart's own settable ShowValueLabels round-trips and
    /// invalidates the documented measure phase.</summary>
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void ShowValueLabels_WhenChanged_RoundTripsAndInvalidatesMeasure(ControlBase control)
    {
        // Arrange
        new LayoutEngine().Layout(control, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            control.Render(frame.Canvas);
        }

        // Act
        SetShowValueLabels(control, true);

        // Assert
        GetShowValueLabels(control).ShouldBeTrue();
        control.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies a null series assignment fails before replacing visible data.</summary>
    [Fact]
    public void Series_WhenAssignedNull_RejectsBeforeMutation()
    {
        // Arrange
        var original = new ChartSeries("Original");
        var chart = new VerticalBarChart { Series = new[] { original } };

        // Act
        _ = Should.Throw<ArgumentNullException>(() => chart.Series = null!);

        // Assert
        chart.Series.ShouldBe([original]);
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

    /// <summary>Verifies a direct assignment of more than one series to a sparkline is rejected
    /// before replacing its previously visible single-series data, exercising Sparkline's own
    /// narrower ValidateSeriesCore override synchronously rather than through an observable source.</summary>
    [Fact]
    public void Series_WhenSparklineAssignedMoreThanOneSeries_RejectsBeforeMutation()
    {
        // Arrange
        var original = new ChartSeries("Original");
        var chart = new Sparkline { Series = new[] { original } };

        // Act
        _ = Should.Throw<ArgumentException>(
            () => chart.Series = new[] { new ChartSeries("A"), new ChartSeries("B") });

        // Assert
        chart.Series.ShouldBe([original]);
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

    /// <summary>Verifies full charts compute the effective disabled state directly and through an
    /// ancestor, the same axis their mounted Disabled contract exercises.</summary>
    [ComponentUnitEvidence(typeof(HorizontalBarChart), ComponentBehavior.Disabled)]
    [ComponentUnitEvidence(typeof(VerticalBarChart), ComponentBehavior.Disabled)]
    [ComponentUnitEvidence(typeof(LineChart), ComponentBehavior.Disabled)]
    [ComponentUnitEvidence(typeof(AreaChart), ComponentBehavior.Disabled)]
    [Theory]
    [MemberData(nameof(FullCharts))]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState(ControlBase chart)
    {
        // Arrange and act — direct disable
        chart.EffectiveIsEnabled.ShouldBeTrue();
        chart.IsEnabled = false;

        // Assert
        chart.EffectiveIsEnabled.ShouldBeFalse();

        // Arrange and act — ancestor-inherited disable
        chart.IsEnabled = true;
        _ = new Stack { Children = { chart }, IsEnabled = false };

        // Assert
        chart.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies a sparkline computes the same direct and ancestor-inherited disabled state.</summary>
    [ComponentUnitEvidence(typeof(Sparkline), ComponentBehavior.Disabled)]
    [Fact]
    public void IsEnabled_WhenSparklineIsDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        // Arrange and act — direct disable
        var chart = new Sparkline();
        chart.EffectiveIsEnabled.ShouldBeTrue();
        chart.IsEnabled = false;

        // Assert
        chart.EffectiveIsEnabled.ShouldBeFalse();

        // Arrange and act — ancestor-inherited disable
        chart.IsEnabled = true;
        _ = new Stack { Children = { chart }, IsEnabled = false };

        // Assert
        chart.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Supplies every full chart as its concrete control type.</summary>
    public static TheoryData<ControlBase> FullCharts =>
    [
        new HorizontalBarChart(),
        new VerticalBarChart(),
        new LineChart(),
        new AreaChart()
    ];

    private static void SetLegendPlacement(ControlBase control, ChartLegendPlacement value) => _ = control switch
    {
        HorizontalBarChart chart => chart.LegendPlacement = value,
        VerticalBarChart chart => chart.LegendPlacement = value,
        LineChart chart => chart.LegendPlacement = value,
        AreaChart chart => chart.LegendPlacement = value,
        _ => throw new UnreachableException()
    };

    private static ChartLegendPlacement GetLegendPlacement(ControlBase control) => control switch
    {
        HorizontalBarChart chart => chart.LegendPlacement,
        VerticalBarChart chart => chart.LegendPlacement,
        LineChart chart => chart.LegendPlacement,
        AreaChart chart => chart.LegendPlacement,
        _ => throw new UnreachableException()
    };

    private static void SetShowCategoryLabels(ControlBase control, bool value) => _ = control switch
    {
        HorizontalBarChart chart => chart.ShowCategoryLabels = value,
        VerticalBarChart chart => chart.ShowCategoryLabels = value,
        LineChart chart => chart.ShowCategoryLabels = value,
        AreaChart chart => chart.ShowCategoryLabels = value,
        _ => throw new UnreachableException()
    };

    private static bool GetShowCategoryLabels(ControlBase control) => control switch
    {
        HorizontalBarChart chart => chart.ShowCategoryLabels,
        VerticalBarChart chart => chart.ShowCategoryLabels,
        LineChart chart => chart.ShowCategoryLabels,
        AreaChart chart => chart.ShowCategoryLabels,
        _ => throw new UnreachableException()
    };

    private static void SetShowValueLabels(ControlBase control, bool value) => _ = control switch
    {
        HorizontalBarChart chart => chart.ShowValueLabels = value,
        VerticalBarChart chart => chart.ShowValueLabels = value,
        LineChart chart => chart.ShowValueLabels = value,
        AreaChart chart => chart.ShowValueLabels = value,
        _ => throw new UnreachableException()
    };

    private static bool GetShowValueLabels(ControlBase control) => control switch
    {
        HorizontalBarChart chart => chart.ShowValueLabels,
        VerticalBarChart chart => chart.ShowValueLabels,
        LineChart chart => chart.ShowValueLabels,
        AreaChart chart => chart.ShowValueLabels,
        _ => throw new UnreachableException()
    };
}
