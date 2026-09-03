// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies chart selection through public mutation, keyboard, pointer, and live data.</summary>
public sealed class ChartSelectionTests
{
    /// <summary>Verifies negative indices are rejected by the immutable identity itself.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_WhenIndexIsNegative_Throws(int seriesIndex, int pointIndex) =>
        // Act and assert
        Should.Throw<ArgumentOutOfRangeException>(() => new ChartSelection(seriesIndex, pointIndex));

    /// <summary>Verifies a valid public selection commits once and reports both snapshots.</summary>
    [Fact]
    public void Selection_WhenChanged_RoundTripsAndRaisesSelectionChanged()
    {
        // Arrange
        var chart = CreateLineChart();
        var transitions = new List<ChartSelectionChangedEventArgs>();
        chart.SelectionChanged += (_, eventArgs) => transitions.Add(eventArgs);

        // Act
        chart.Selection = new ChartSelection(0, 1);

        // Assert
        chart.Selection.ShouldBe(new ChartSelection(0, 1));
        transitions.Count.ShouldBe(1);
        transitions[0].PreviousSelection.ShouldBeNull();
        transitions[0].Selection.ShouldBe(new ChartSelection(0, 1));
    }

    /// <summary>Verifies an out-of-range public selection fails before replacing visible state.</summary>
    [Fact]
    public void Selection_WhenIndexIsOutsideCurrentData_RejectsBeforeMutation()
    {
        // Arrange
        var chart = CreateLineChart();
        chart.Selection = new ChartSelection(0, 0);

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => chart.Selection = new ChartSelection(1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => chart.Selection = new ChartSelection(0, 3));

        // Assert
        chart.Selection.ShouldBe(new ChartSelection(0, 0));
    }

    /// <summary>Verifies horizontal category navigation and vertical series navigation from focus.</summary>
    [Fact]
    public async Task Keyboard_WhenLineChartIsFocused_MovesSelectionAcrossPointsAndSeriesAsync()
    {
        // Arrange
        var chart = CreateLineChart(includeSecondSeries: true);
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(7, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Right);
        chart.Selection.ShouldBe(new ChartSelection(0, 0));
        await surface.Keyboard.PressAsync(Code.Right);
        chart.Selection.ShouldBe(new ChartSelection(0, 1));
        await surface.Keyboard.PressAsync(Code.Down);
        chart.Selection.ShouldBe(new ChartSelection(1, 1));
        await surface.Keyboard.PressAsync(Code.End);
        chart.Selection.ShouldBe(new ChartSelection(1, 2));
        await surface.Keyboard.PressAsync(Code.Escape);
        chart.Selection.ShouldBeNull();
    }

    /// <summary>Verifies horizontal bars use their vertical visual category axis for arrow input.</summary>
    [Fact]
    public async Task Keyboard_WhenHorizontalBarChartIsFocused_UsesUpAndDownForCategoriesAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 1),
                new ChartDataPoint("B", 2),
                new ChartDataPoint("C", 3)])],
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        chart.Selection.ShouldBe(new ChartSelection(0, 1));
        await surface.Keyboard.PressAsync(Code.Up);
        chart.Selection.ShouldBe(new ChartSelection(0, 0));
    }

    /// <summary>Verifies command modifiers do not turn chart navigation into accidental selection.</summary>
    [Fact]
    public async Task Keyboard_WhenNavigationCarriesModifier_LeavesSelectionUnchangedAsync()
    {
        // Arrange
        var chart = CreateLineChart();
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(7, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right, Modifiers.Shift);

        // Assert
        chart.Selection.ShouldBeNull();
    }

    /// <summary>Verifies Escape remains available to an ancestor when the chart has no selection
    /// to clear.</summary>
    [Fact]
    public void Keyboard_WhenEscapeHasNoSelection_LeavesEventUnhandled()
    {
        // Arrange
        var chart = CreateLineChart();
        var key = new KeyEventArgs(
            new Stroke(Code.Escape, null, 0, Modifiers.None, KeyAction.Press));

        // Act
        var routed = Router.Route(chart, Events.Key, key);

        // Assert
        routed.IsHandled.ShouldBeFalse();
        chart.Selection.ShouldBeNull();
    }

    /// <summary>Verifies an arrow on the series axis remains available to an ancestor when the
    /// chart has no alternate populated series.</summary>
    [Fact]
    public void Keyboard_WhenSeriesAxisHasOnePopulatedSeries_LeavesArrowUnhandled()
    {
        // Arrange
        var chart = CreateLineChart();
        var key = new KeyEventArgs(
            new Stroke(Code.Down, null, 0, Modifiers.None, KeyAction.Press));

        // Act
        var routed = Router.Route(chart, Events.Key, key);

        // Assert
        routed.IsHandled.ShouldBeFalse();
        chart.Selection.ShouldBeNull();
    }

    /// <summary>Verifies a primary pointer click focuses the chart and selects the nearest point.</summary>
    [Fact]
    public async Task Pointer_WhenLinePlotIsClicked_FocusesAndSelectsNearestPointAsync()
    {
        // Arrange
        var chart = CreateLineChart();
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(chart, new Point(6, 2));

        // Assert
        chart.IsFocused.ShouldBeTrue();
        chart.Selection.ShouldBe(new ChartSelection(0, 2));
    }

    /// <summary>Verifies Sparkline hit testing maps visible columns to the retained recent-point window.</summary>
    [Fact]
    public async Task Pointer_WhenSparklineExceedsWidth_SelectsTheVisibleRecentPointAsync()
    {
        // Arrange
        var chart = new Sparkline
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 1),
                new ChartDataPoint("B", 2),
                new ChartDataPoint("C", 3),
                new ChartDataPoint("D", 4)])]
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(chart, new Point(0, 0));

        // Assert
        chart.Selection.ShouldBe(new ChartSelection(0, 1));
    }

    /// <summary>Verifies unavailable charts ignore both keyboard and pointer selection input.</summary>
    [Fact]
    public async Task Input_WhenChartIsDisabled_LeavesSelectionUnchangedAsync()
    {
        // Arrange
        var chart = CreateLineChart();
        chart.IsEnabled = false;
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(chart, new Point(6, 2));
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        chart.Selection.ShouldBeNull();
        chart.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies selection follows the same borrowed point object when its index changes.</summary>
    [Fact]
    public void Selection_WhenSelectedPointMoves_ReindexesInsteadOfSelectingAnotherPoint()
    {
        // Arrange
        var first = new ChartDataPoint("A", 1);
        var second = new ChartDataPoint("B", 2);
        var series = new ChartSeries("CPU", [first, second]);
        var chart = new LineChart { Series = [series], Selection = new ChartSelection(0, 0) };

        // Act
        series.Points.Move(0, 1);

        // Assert
        chart.Selection.ShouldBe(new ChartSelection(0, 1));
        series.Points[chart.Selection.Value.PointIndex].ShouldBeSameAs(first);
    }

    /// <summary>Verifies removing the selected borrowed point clears the committed selection.</summary>
    [Fact]
    public void Selection_WhenSelectedPointIsRemoved_ClearsSelection()
    {
        // Arrange
        var series = new ChartSeries("CPU", [
            new ChartDataPoint("A", 1),
            new ChartDataPoint("B", 2)]);
        var chart = new LineChart { Series = [series], Selection = new ChartSelection(0, 1) };
        var transitions = new List<ChartSelectionChangedEventArgs>();
        chart.SelectionChanged += (_, eventArgs) => transitions.Add(eventArgs);

        // Act
        series.Points.RemoveAt(1);

        // Assert
        chart.Selection.ShouldBeNull();
        transitions.Count.ShouldBe(1);
        transitions[0].PreviousSelection.ShouldBe(new ChartSelection(0, 1));
        transitions[0].Selection.ShouldBeNull();
    }

    /// <summary>Verifies selected data uses the style's resolved selected decoration.</summary>
    [Fact]
    public async Task Render_WhenPointIsSelected_AppliesSelectionDecorationAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 1)])],
            Scale = new ChartScale(0, 1, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false,
            Selection = new ChartSelection(0, 0),
            Style = ChartStyle.Default with { SelectionDecoration = TerminalAttributes.Reverse }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Reverse)
            .ShouldBe(TerminalAttributes.Reverse);
    }

    /// <summary>Verifies a selected bar gets an endpoint marker that remains visible on full blocks.</summary>
    [Fact]
    public async Task Render_WhenHorizontalBarIsSelected_DrawsVisibleEndpointMarkerAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 4)])],
            Scale = new ChartScale(0, 4, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false,
            Selection = new ChartSelection(0, 0)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("███●");
    }

    /// <summary>Verifies a selected sparkline column gets a point marker instead of an invisible
    /// reverse attribute on a full-block glyph.</summary>
    [Fact]
    public async Task Render_WhenSparklineColumnIsSelected_DrawsVisiblePointMarkerAsync()
    {
        // Arrange
        var chart = new Sparkline
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 1),
                new ChartDataPoint("B", 4),
                new ChartDataPoint("C", 8)])],
            Scale = new ChartScale(0, 8, includeZero: false),
            Selection = new ChartSelection(0, 1)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("▁●█");
    }

    private static LineChart CreateLineChart(bool includeSecondSeries = false)
    {
        var points = new[]
        {
            new ChartDataPoint("A", 1),
            new ChartDataPoint("B", 2),
            new ChartDataPoint("C", 3)
        };
        var series = new List<ChartSeries> { new("CPU", points) };

        if (includeSecondSeries)
        {
            series.Add(new ChartSeries("Memory", [
                new ChartDataPoint("A", 3),
                new ChartDataPoint("B", 2),
                new ChartDataPoint("C", 1)]));
        }

        return new LineChart
        {
            Series = series,
            Scale = new ChartScale(0, 3, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };
    }
}
