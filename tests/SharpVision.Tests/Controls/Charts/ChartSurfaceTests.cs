// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies chart rendering and live data through mounted terminal surfaces.</summary>
public sealed class ChartSurfaceTests
{
    /// <summary>Verifies a horizontal bar fills away from the automatic zero baseline.</summary>
    [ComponentBehaviorEvidence(
        typeof(HorizontalBarChart),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenHorizontalBarHasOnePositiveValue_FillsAvailableWidthAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 5)])],
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("█████");
    }

    /// <summary>Verifies a vertical bar rises from the bottom of its plot.</summary>
    [ComponentBehaviorEvidence(
        typeof(VerticalBarChart),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenVerticalBarHasOnePositiveValue_FillsAvailableHeightAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 5)])],
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             █
                             █
                             █
                             """);
    }

    /// <summary>Verifies line points map to both extrema of an explicit scale.</summary>
    [ComponentBehaviorEvidence(
        typeof(LineChart),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenLineHasThreePoints_MapsExtremaIntoPlotAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 0),
                new ChartDataPoint("B", 2),
                new ChartDataPoint("C", 0)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("●");
    }

    /// <summary>Verifies line charts reserve their final plot row for category labels.</summary>
    [Fact]
    public async Task Render_WhenLineShowsCategoryLabels_LabelsTheHorizontalDomainAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("A");
    }

    /// <summary>Verifies authored chart label colors reach rendered category cells.</summary>
    [Fact]
    public async Task Render_WhenChartStyleHasLabelColor_UsesItForCategoryLabelsAsync()
    {
        // Arrange
        var baseline = ChartStyle.Default;
        var labelColor = ReferenceColors.Get(4);
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 2)])],
            Style = baseline with { LabelColor = labelColor },
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(labelColor);
    }

    /// <summary>Verifies line charts can render finite values beside visible point markers.</summary>
    [Fact]
    public async Task Render_WhenLineShowsValueLabels_LabelsVisiblePointsAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false,
            ShowValueLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("2");
    }

    /// <summary>Verifies an area chart fills cells beneath its visible line.</summary>
    [ComponentBehaviorEvidence(
        typeof(AreaChart),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenAreaHasPositiveValue_FillsTowardBaselineAsync()
    {
        // Arrange
        var chart = new AreaChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ●
                             █
                             █
                             """);
    }

    /// <summary>Verifies connecting later area points does not obscure earlier point markers.</summary>
    [Fact]
    public async Task Render_WhenAreaHasMultiplePoints_KeepsEveryPointVisibleAsync()
    {
        // Arrange
        var chart = new AreaChart
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 0),
                new ChartDataPoint("B", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("●");
    }

    /// <summary>Verifies a sparkline uses sub-cell blocks and the most recent fitting points.</summary>
    [ComponentBehaviorEvidence(
        typeof(Sparkline),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Render_WhenSparklineExceedsWidth_UsesMostRecentPointsAsync()
    {
        // Arrange
        var chart = new Sparkline
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", -1),
                new ChartDataPoint("B", 0),
                new ChartDataPoint("C", 1),
                new ChartDataPoint("D", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(" ▄█");
    }

    /// <summary>Verifies a mounted chart observes deep point mutations and rerenders.</summary>
    [Fact]
    public async Task UpdateAsync_WhenPointValueChanges_RendersNewValueAsync()
    {
        // Arrange
        var point = new ChartDataPoint("Now", 0);
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [point])],
            Scale = new ChartScale(0, 5, includeZero: false),
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("     ");

        // Act
        await surface.UpdateAsync(() => point.Value = 5, "raise the chart point");

        // Assert
        surface.ShouldRender("█████");
    }

    /// <summary>Verifies a point color overrides its containing series color in final cells.</summary>
    [Fact]
    public async Task Render_WhenPointHasExplicitColor_UsesPointColorAsync()
    {
        // Arrange
        var point = new ChartDataPoint("Now", 5) { Color = ReferenceColors.Get(4) };
        var series = new ChartSeries("CPU", [point]) { Color = ReferenceColors.Get(3) };
        var chart = new HorizontalBarChart
        {
            Series = [series],
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(4));
    }

    /// <summary>Verifies automatic legend placement identifies two named series.</summary>
    [Fact]
    public async Task Render_WhenTwoNamedSeriesExist_DrawsAutomaticBottomLegendAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [
                new ChartSeries("A", [new ChartDataPoint("1", 1)]),
                new ChartSeries("B", [new ChartDataPoint("1", 2)])],
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("■");
        surface.Cell(new Point(1, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 2)).Text.ShouldBe("A");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("■");
        surface.Cell(new Point(5, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(6, 2)).Text.ShouldBe("B");
    }

    /// <summary>Verifies bottom legends are separated from horizontal, line, and area plot cells.</summary>
    [Fact]
    public async Task Render_WhenBottomLegendIsVisible_SeparatesItFromThePlotAsync()
    {
        // Arrange
        ControlBase[] charts = [
            new HorizontalBarChart
            {
                Series = CreateNamedSeries(),
                ShowCategoryLabels = false
            },
            new LineChart
            {
                Series = CreateNamedSeries(),
                ShowCategoryLabels = false
            },
            new AreaChart
            {
                Series = CreateNamedSeries(),
                ShowCategoryLabels = false
            }
        ];

        foreach (var chart in charts)
        {
            // Act
            await using var surface = await ComponentSurface.MountAsync(
                chart,
                new Size(12, 4),
                TestContext.Current.CancellationToken);

            // Assert
            surface.Cell(new Point(0, 2)).Text.ShouldBe("─", chart.GetType().Name);
            surface.Cell(new Point(11, 2)).Text.ShouldBe("─", chart.GetType().Name);
            surface.Cell(new Point(0, 3)).Text.ShouldBe("■", chart.GetType().Name);
        }
    }

    /// <summary>Verifies horizontal value labels retain one blank cell after the bar.</summary>
    [Fact]
    public async Task Render_WhenHorizontalBarShowsValueLabel_SeparatesItFromTheBarAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 5)])],
            Scale = new ChartScale(0, 10, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false,
            ShowValueLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("█████ 5   ");
    }

    /// <summary>Verifies horizontal categories have a visible boundary and bars begin at the plot origin.</summary>
    [Fact]
    public async Task Render_WhenHorizontalBarShowsCategoryLabels_SeparatesLabelsFromPlotAsync()
    {
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("Revenue", [new ChartDataPoint("North", 5)])]
        };

        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(15, 1),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(5, 0)).Text.ShouldBe("│");
        surface.Cell(new Point(6, 0)).Text.ShouldBe("█");
    }

    /// <summary>Verifies vertical category labels sit beneath a visible horizontal axis.</summary>
    [Fact]
    public async Task Render_WhenVerticalBarShowsCategoryLabels_SeparatesLabelsFromPlotAsync()
    {
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("Score", [
                new ChartDataPoint("Mon", 2),
                new ChartDataPoint("Tue", 4)])]
        };

        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 3)).Text.ShouldBe("─");
        surface.Cell(new Point(2, 4)).Text.ShouldBe("M");
        surface.Cell(new Point(8, 4)).Text.ShouldBe("T");
    }

    /// <summary>Verifies mixed-sign horizontal bars expose the zero baseline.</summary>
    [Fact]
    public async Task Render_WhenHorizontalRangeHasMixedSigns_DrawsZeroBaselineAsync()
    {
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("Delta", [new ChartDataPoint("Now", -5)])],
            Scale = new ChartScale(-5, 5, includeZero: true),
            ShowCategoryLabels = false
        };

        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(5, 0)).Text.ShouldBe("│");
        surface.Cell(new Point(5, 2)).Text.ShouldBe("│");
    }

    /// <summary>Verifies multi-cell line labels are centered without truncating their text.</summary>
    [Fact]
    public async Task Render_WhenLineLabelsHaveMultipleCells_DrawsCompleteLabelsAsync()
    {
        var chart = new LineChart
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("09", 1),
                new ChartDataPoint("10", 2)])],
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        surface.Cell(new Point(0, 2)).Text.ShouldBe("0");
        surface.Cell(new Point(1, 2)).Text.ShouldBe("9");
        surface.Cell(new Point(10, 2)).Text.ShouldBe("1");
        surface.Cell(new Point(11, 2)).Text.ShouldBe("0");
    }

    private static IReadOnlyList<ChartSeries> CreateNamedSeries() => [
        new ChartSeries("A", [new ChartDataPoint("1", 1)]),
        new ChartSeries("B", [new ChartDataPoint("1", 2)])
    ];
}
