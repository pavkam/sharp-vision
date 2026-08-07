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

    /// <summary>Verifies a squeezed grouped horizontal chart drops a trailing bar instead of
    /// overdrawing a neighbour. Six slots in a five-row plot used to map two adjacent slots onto
    /// one row through the global slot spread, and the later series silently painted over the
    /// earlier one - a bar vanished with no signal, and the independently placed category label
    /// could end up naming a row owned by a different group. Categories now own disjoint bands:
    /// the first category's band is one row, so its first series wins that row and its second is
    /// dropped, while the roomier bands keep both series on their own rows.</summary>
    [Fact]
    public async Task Render_WhenGroupedRowsExceedPlotHeight_DropsInsteadOfOverdrawingAsync()
    {
        // Arrange
        var first = ReferenceColors.Get(1);
        var second = ReferenceColors.Get(2);
        var chart = new HorizontalBarChart
        {
            Series = [
                new ChartSeries("Current", [
                    new ChartDataPoint("N", 4),
                    new ChartDataPoint("S", 4),
                    new ChartDataPoint("W", 4)]) { Color = first },
                new ChartSeries("Target", [
                    new ChartDataPoint("N", 2),
                    new ChartDataPoint("S", 2),
                    new ChartDataPoint("W", 2)]) { Color = second }],
            ShowCategoryLabels = false,
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        // Act: three categories x two series = six slots in a five-row plot.
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 5),
            TestContext.Current.CancellationToken);

        // Assert: bands are rows [0], [1-2], [3-4]; the squeezed first band keeps its first
        // series full-width, and every other row belongs to exactly the expected series.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(3, 1)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(second);
        surface.Cell(new Point(0, 3)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(0, 4)).Style.Foreground.ShouldBe(second);

        // The dropped second series of the squeezed category must not resurface anywhere: the
        // short second-series bar is two cells, so column 3 is empty on second-series rows.
        surface.Cell(new Point(3, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(3, 4)).Text.ShouldBe(" ");
    }

    /// <summary>The vertical twin of the squeezed-group guarantee: columns come from disjoint
    /// category bands, so a later series can never paint over another column.</summary>
    [Fact]
    public async Task Render_WhenGroupedColumnsExceedPlotWidth_DropsInsteadOfOverdrawingAsync()
    {
        // Arrange
        var first = ReferenceColors.Get(1);
        var second = ReferenceColors.Get(2);
        var chart = new VerticalBarChart
        {
            Series = [
                new ChartSeries("Current", [
                    new ChartDataPoint("N", 4),
                    new ChartDataPoint("S", 4),
                    new ChartDataPoint("W", 4)]) { Color = first },
                new ChartSeries("Target", [
                    new ChartDataPoint("N", 2),
                    new ChartDataPoint("S", 2),
                    new ChartDataPoint("W", 2)]) { Color = second }],
            ShowCategoryLabels = false,
            LegendPlacement = ChartLegendPlacement.Hidden
        };

        // Act: six slots in a five-column plot.
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 4),
            TestContext.Current.CancellationToken);

        // Assert: bands are columns [0], [1-2], [3-4]; full-height first series, half-height
        // second, and the squeezed first category keeps only its first series.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(2, 2)).Style.Foreground.ShouldBe(second);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(first);
        surface.Cell(new Point(4, 2)).Style.Foreground.ShouldBe(second);
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(4, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a vertical bar ends on an eighth-cell boundary instead of rounding to a
    /// whole cell: 3 of 8 in a one-cell plot is the exact three-eighths block.</summary>
    [Fact]
    public async Task Render_WhenVerticalValueLandsMidCell_DrawsTheExactEighthBlockAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 3)])],
            Scale = new ChartScale(0, 8, includeZero: false),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("▃");
    }

    /// <summary>Verifies the fractional cap sits on top of full cells in a taller plot: 1.25 of 2
    /// across two rows is one full cell and a quarter-cell cap.</summary>
    [Fact]
    public async Task Render_WhenVerticalValueSpansCells_CapsTheTopCellFractionallyAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 1.25)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▂
                             █
                             """);
    }

    /// <summary>The same for a horizontal bar, whose cap uses the left-eighth family: 5 of 8 in a
    /// one-cell plot is the exact five-eighths block.</summary>
    [Fact]
    public async Task Render_WhenHorizontalValueLandsMidCell_DrawsTheExactEighthBlockAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 5)])],
            Scale = new ChartScale(0, 8, includeZero: false),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("▋");
    }

    /// <summary>Verifies bars fill their category band instead of occupying one centered column:
    /// two categories over eight columns give each a three-cell bar and a one-cell gutter.</summary>
    [Fact]
    public async Task Render_WhenTheBandIsWide_FillsItWithAThickBarAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [
                new ChartDataPoint("A", 2),
                new ChartDataPoint("B", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert: bands are columns [0-3] and [4-7]; each keeps its final cell as a gutter.
        surface.ShouldRender("███ ███ ");
    }

    /// <summary>Verifies the glyph fill mode keeps the historical one-cell lanes and whole-cell
    /// rounding, drawing the authored bar glyph exactly - the contract a theme that replaces the
    /// glyph relies on.</summary>
    [Fact]
    public async Task Render_WhenFillModeIsGlyph_KeepsTheAuthoredGlyphAndThinLanesAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", 2)])],
            Scale = new ChartScale(0, 2, includeZero: false),
            ShowCategoryLabels = false,
            Style = ChartStyle.Default with
            {
                FillMode = ChartFillMode.Glyph,
                Glyphs = ChartGlyphs.Default with { Bar = new Rune('▓') }
            }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Assert: one centered single-cell lane, full cells only.
        surface.ShouldRender("""
                             ▓
                             ▓
                             """.Replace("▓", " ▓ ", StringComparison.Ordinal));
    }

    /// <summary>Verifies a negative vertical bar grows down from the zero baseline and snaps its
    /// cap to the half cell - the finest glyph the upper block family defines.</summary>
    [Fact]
    public async Task Render_WhenTheValueIsNegative_GrowsDownFromTheBaselineAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("A", -1.5)])],
            Scale = new ChartScale(-2, 2, includeZero: true),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(1, 4),
            TestContext.Current.CancellationToken);

        // Assert: zero sits between rows 1 and 2; 1.5 of 2 is one full cell then a half cap.
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("█");
        surface.Cell(new Point(0, 3)).Text.ShouldBe("▀");
    }

    /// <summary>Verifies a bar reaching the plot edge keeps its value label by drawing it over
    /// the bar's tail. It used to vanish entirely, hiding exactly the extreme value a reader
    /// most wants to know.</summary>
    [Fact]
    public async Task Render_WhenTheBarReachesThePlotEdge_ClampsTheValueLabelInsideAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("CPU", [new ChartDataPoint("Now", 8)])],
            Scale = new ChartScale(0, 8, includeZero: false),
            ShowCategoryLabels = false,
            ShowValueLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("█████8");
    }

    private static IReadOnlyList<ChartSeries> CreateNamedSeries() => [
        new ChartSeries("A", [new ChartDataPoint("1", 1)]),
        new ChartSeries("B", [new ChartDataPoint("1", 2)])
    ];
}
