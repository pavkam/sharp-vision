// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

using System.Collections.ObjectModel;

/// <summary>Verifies the shared public contract of concrete chart controls.</summary>
public sealed class ChartControlTests
{
    /// <summary>Verifies full charts share documented passive presentation defaults.</summary>
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

    /// <summary>Verifies a sparkline's own fixed-policy overrides - unlike every full chart, which
    /// forwards a settable property - always report Hidden/false/false through
    /// <see cref="IChartControl"/>, regardless of the base class's own constructor-supplied
    /// state, since Sparkline exposes no public LegendPlacement, ShowCategoryLabels, or
    /// ShowValueLabels surface at all.</summary>
    [Fact]
    public void ResolvePresentation_WhenSparkline_AlwaysReportsHiddenLabelsAndLegend()
    {
        // Arrange and act
        var chart = new Sparkline().ShouldBeAssignableTo<IChartControl>();

        // Assert
        chart.LegendPlacement.ShouldBe(ChartLegendPlacement.Hidden);
        chart.ShowCategoryLabels.ShouldBeFalse();
        chart.ShowValueLabels.ShouldBeFalse();
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

    /// <summary>Verifies direct observable source membership remains reactive without binding,
    /// and that the refreshed membership is reported through the same <see cref="ChartControlBase.Series"/>
    /// property-changed notification and measure-phase invalidation an ordinary Series
    /// reassignment would raise - not merely that the snapshot value happens to update.</summary>
    [Fact]
    public void Series_WhenObservableSourceChanges_RefreshesMembershipAndNotifiesSeriesProperty()
    {
        // Arrange
        var source = new ObservableCollection<ChartSeries>();
        var chart = new HorizontalBarChart { Series = source };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var series = new ChartSeries("Live");
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        source.Add(series);

        // Assert
        chart.Series.ShouldBe([series]);
        chart.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        notifications.ShouldBe([nameof(ChartControlBase.Series)]);
    }

    /// <summary>Verifies mutating an already-owned series' <see cref="ChartSeries.Points"/>
    /// collection is treated identically to a top-level membership change - requiring measure
    /// invalidation and a <see cref="ChartControlBase.Series"/> notification - even though the
    /// series reference itself never left the chart, since a chart's rendered layout depends on
    /// each series' point count and labels just as much as on which series are present.</summary>
    [Fact]
    public void Series_WhenOwnedSeriesPointsCollectionChanges_TreatsMembershipAsChanged()
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var chart = new LineChart { Series = [series] };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        series.Points.Add(new ChartDataPoint("t1", 1));

        // Assert
        chart.Series.ShouldBe([series]);
        chart.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        notifications.ShouldBe([nameof(ChartControlBase.Series)]);
    }

    /// <summary>Verifies every observable chart-model mutation is dispatcher-affine while the
    /// borrowing chart is attached, rather than racing a later posted enumeration.</summary>
    [Theory]
    [InlineData("SeriesMembership")]
    [InlineData("PointMembership")]
    [InlineData("SeriesProperty")]
    [InlineData("PointProperty")]
    public async Task ObservableData_WhenMutatedOffAttachedDispatcher_ThrowsDeterministicallyAsync(
        string mutation)
    {
        // Arrange
        var point = new ChartDataPoint("Initial", 1);
        var series = new ChartSeries("Series", [point]);
        var source = new ObservableCollection<ChartSeries> { series };
        var chart = new LineChart { Series = source };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(
            () => chart.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        try
        {
            // Act and assert
            _ = await Should.ThrowAsync<InvalidOperationException>(() => Task.Run(() =>
            {
                switch (mutation)
                {
                    case "SeriesMembership":
                        source.Add(new ChartSeries("Added"));
                        break;
                    case "PointMembership":
                        series.Points.Add(new ChartDataPoint("Added", 2));
                        break;
                    case "SeriesProperty":
                        series.Name = "Changed";
                        break;
                    case "PointProperty":
                        point.Value = 2;
                        break;
                    default:
                        throw new UnreachableException();
                }
            }, TestContext.Current.CancellationToken));
        }
        finally
        {
            await dispatcher.InvokeAsync(chart.Dispose, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies detachment removes chart-model affinity and disposal releases every
    /// observable subscription, with no queued refresh left to race either transition.</summary>
    [Fact]
    public async Task ObservableData_WhenChartDetachesThenDisposes_AllowsDetachedMutationAndReleasesSubscriptionsAsync()
    {
        // Arrange
        var first = new ChartSeries("First");
        var source = new ObservableCollection<ChartSeries> { first };
        var chart = new LineChart { Series = source };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(
            () => chart.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(chart.Detach, TestContext.Current.CancellationToken);
        var second = new ChartSeries("Second");

        // Act - detached borrowed data may be updated by its current owner thread.
        await Task.Run(() => source.Add(second), TestContext.Current.CancellationToken);

        // Assert and act - the detached snapshot updates, then disposal removes subscriptions.
        chart.Series.ShouldBe([first, second]);
        chart.Dispose();
        await Should.NotThrowAsync(() => Task.Run(
            () => source.Add(new ChartSeries("After disposal")),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a changed series <see cref="ChartSeries.Name"/> invalidates measure but
    /// publishes no <see cref="ChartControlBase.Series"/> (or any other) property notification,
    /// since the observer forwards non-membership property changes straight to
    /// <c>Invalidate</c> rather than through <c>NotifyPropertyChanged</c>.</summary>
    [Fact]
    public void Series_WhenOwnedSeriesNameChanges_InvalidatesMeasureWithoutPropertyNotification()
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var chart = new LineChart { Series = [series] };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        series.Name = "Memory";

        // Assert
        chart.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies a changed series <see cref="ChartSeries.Color"/> - unlike its Name -
    /// invalidates only rendering, since color is presentation rather than layout-affecting.</summary>
    [Fact]
    public void Series_WhenOwnedSeriesColorChanges_InvalidatesRenderOnly()
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var chart = new LineChart { Series = [series] };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        series.Color = Color.Rgb(10, 20, 30);

        // Assert
        chart.Pending.ShouldBe(Invalidation.Render);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies a changed point <see cref="ChartDataPoint.Label"/> invalidates measure
    /// without any property notification, mirroring a series' own Name.</summary>
    [Fact]
    public void Series_WhenOwnedPointLabelChanges_InvalidatesMeasureWithoutPropertyNotification()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 1);
        var chart = new LineChart { Series = [new ChartSeries("Host", [point])] };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        point.Label = "Memory";

        // Assert
        chart.Pending.ShouldBe(Invalidation.Measure | Invalidation.Arrange | Invalidation.Render);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies a changed point <see cref="ChartDataPoint.Value"/> - unlike its Label -
    /// invalidates only rendering.</summary>
    [Fact]
    public void Series_WhenOwnedPointValueChanges_InvalidatesRenderOnly()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 1);
        var chart = new LineChart { Series = [new ChartSeries("Host", [point])] };
        new LayoutEngine().Layout(chart, new Size(30, 10));
        using (var frame = new Frame(new Size(30, 10)))
        {
            chart.Render(frame.Canvas);
        }
        var notifications = new List<string?>();
        chart.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        point.Value = 42;

        // Assert
        chart.Pending.ShouldBe(Invalidation.Render);
        notifications.ShouldBeEmpty();
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
