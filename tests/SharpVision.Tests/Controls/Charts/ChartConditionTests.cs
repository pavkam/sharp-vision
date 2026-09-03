// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies every chart family under boundary data and layout conditions through mounted
/// surfaces: empty series, single or identical points, negative-only values, clamped fixed scales,
/// every legend placement including narrow fallbacks, tiny plots, live data mutation after layout,
/// resize, and style or color changes while mounted.</summary>
public sealed class ChartConditionTests
{
    /// <summary>Verifies every chart family renders nothing, without faulting, when it has no series
    /// or only series without points.</summary>
    [Theory]
    [InlineData("horizontal", false)]
    [InlineData("horizontal", true)]
    [InlineData("vertical", false)]
    [InlineData("vertical", true)]
    [InlineData("line", false)]
    [InlineData("line", true)]
    [InlineData("area", false)]
    [InlineData("area", true)]
    [InlineData("sparkline", false)]
    [InlineData("sparkline", true)]
    public async Task Render_WhenThereIsNoData_DrawsBlankSurfaceAsync(string kind, bool emptySeries)
    {
        // Arrange
        var chart = CreateChart(kind);
        chart.Series = emptySeries ? [new ChartSeries("Empty")] : [];

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        // Assert
        Rows(surface, 8, 4).ShouldAllBe(row => row.Trim().Length == 0);
    }

    /// <summary>Verifies a single line point lands on exactly one cell in the vertical middle of the
    /// symmetric automatic expansion around its constant value.</summary>
    [Fact]
    public async Task Render_WhenLineHasSinglePoint_DrawsOneCenteredMarkerAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("only", 7)])],
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 3),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(surface, 5, 3);
        rows[0].Trim().ShouldBeEmpty();
        rows[2].Trim().ShouldBeEmpty();
        rows[1].Count(cell => cell == '●').ShouldBe(1);
    }

    /// <summary>Verifies identical line values share the middle row after symmetric expansion.</summary>
    [Fact]
    public async Task Render_WhenLineValuesAreIdentical_PlacesEveryMarkerOnTheMiddleRowAsync()
    {
        // Arrange
        var chart = new LineChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("a", 4), new ChartDataPoint("b", 4)])],
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(surface, 4, 3);
        rows[0].Trim().ShouldBeEmpty();
        rows[2].Trim().ShouldBeEmpty();
        rows[1].Count(cell => cell == '●').ShouldBe(2);
    }

    /// <summary>Verifies negative-only horizontal bars grow leftward from a zero baseline pinned to the
    /// right plot edge, with no axis glyph drawn inside the plot.</summary>
    [Fact]
    public async Task Render_WhenHorizontalValuesAreNegativeOnly_GrowsLeftFromRightEdgeAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("full", -5), new ChartDataPoint("none", 0)])],
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             █████

                             """);
    }

    /// <summary>Verifies a fixed scale clips values outside its bounds instead of rescaling.</summary>
    [Theory]
    [InlineData(100d, "█████")]
    [InlineData(-100d, "     ")]
    [InlineData(5d, "██▌  ")]
    public async Task Render_WhenScaleIsFixed_ClampsValuesToTheAuthoredRangeAsync(double value, string expected)
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", value)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected);
    }

    /// <summary>Verifies every explicit legend placement reserves its documented region and keeps
    /// the plot markers outside of it.</summary>
    [Theory]
    [InlineData(ChartLegendPlacement.Top, 0, 0, 2, 0, 4, 0, 6, 0)]
    [InlineData(ChartLegendPlacement.Bottom, 0, 4, 2, 4, 4, 4, 6, 4)]
    [InlineData(ChartLegendPlacement.Automatic, 0, 4, 2, 4, 4, 4, 6, 4)]
    [InlineData(ChartLegendPlacement.Left, 0, 0, 2, 0, 0, 1, 2, 1)]
    [InlineData(ChartLegendPlacement.Right, 8, 0, 10, 0, 8, 1, 10, 1)]
    public async Task Render_WhenLegendPlacementIsExplicit_PlacesEntriesInTheDocumentedRegionAsync(
        ChartLegendPlacement placement,
        int firstMarkerX,
        int firstMarkerY,
        int firstNameX,
        int firstNameY,
        int secondMarkerX,
        int secondMarkerY,
        int secondNameX,
        int secondNameY)
    {
        // Arrange
        var chart = new LineChart
        {
            Series =
            [
                new ChartSeries("A", [new ChartDataPoint("1", 1), new ChartDataPoint("2", 3)]),
                new ChartSeries("B", [new ChartDataPoint("1", 3), new ChartDataPoint("2", 1)])
            ],
            LegendPlacement = placement,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(firstMarkerX, firstMarkerY)).Text.ShouldBe("■");
        surface.Cell(new Point(firstNameX, firstNameY)).Text.ShouldBe("A");
        surface.Cell(new Point(secondMarkerX, secondMarkerY)).Text.ShouldBe("■");
        surface.Cell(new Point(secondNameX, secondNameY)).Text.ShouldBe("B");
        var rows = Rows(surface, 12, 5);
        string.Concat(rows).Count(cell => cell == '■').ShouldBe(2);
        string.Concat(rows).Count(cell => cell == '●').ShouldBe(4);

        if (placement is ChartLegendPlacement.Bottom or ChartLegendPlacement.Automatic)
        {
            rows[3].ShouldBe("────────────");
        }
        else if (placement == ChartLegendPlacement.Top)
        {
            rows[1].ShouldBe("────────────");
        }
        else
        {
            string.Concat(rows).ShouldNotContain("─");
        }
    }

    /// <summary>Verifies a hidden legend, or an automatic legend with only one named series, draws no
    /// legend cells at all.</summary>
    [Theory]
    [InlineData(ChartLegendPlacement.Hidden, 2)]
    [InlineData(ChartLegendPlacement.Automatic, 1)]
    public async Task Render_WhenLegendIsHiddenOrNotWarranted_DrawsNoLegendAsync(ChartLegendPlacement placement, int seriesCount)
    {
        // Arrange
        var chart = new LineChart
        {
            Series = Enumerable.Range(0, seriesCount)
                .Select(index => new ChartSeries($"S{index}", [new ChartDataPoint("1", index + 1)]))
                .ToArray(),
            LegendPlacement = placement,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        var text = string.Concat(Rows(surface, 12, 5));
        text.ShouldNotContain("■");
        text.ShouldNotContain("─");
        text.Count(cell => cell == '●').ShouldBe(seriesCount);
    }

    /// <summary>Verifies side legends fall back to a bottom row on narrow bounds and that entries that
    /// no longer fit are dropped rather than overdrawn.</summary>
    [Theory]
    [InlineData(ChartLegendPlacement.Left)]
    [InlineData(ChartLegendPlacement.Right)]
    public async Task Render_WhenSideLegendHasNarrowBounds_FallsBackToBottomAndDropsOverflowAsync(ChartLegendPlacement placement)
    {
        // Arrange
        var chart = new LineChart
        {
            Series =
            [
                new ChartSeries("A", [new ChartDataPoint("1", 1)]),
                new ChartSeries("B", [new ChartDataPoint("1", 3)])
            ],
            LegendPlacement = placement,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(6, 5),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(surface, 6, 5);
        rows[4].ShouldBe("■ A   ");
        rows[3].ShouldBe("──────");
        string.Concat(rows).ShouldNotContain("B");
    }

    /// <summary>Verifies tiny bounds suppress the optional legend and the category label row before
    /// data, while a two-row plot still reserves its label row and clips the label to whole cells.</summary>
    [Theory]
    [InlineData(3, 1, "")]
    [InlineData(1, 1, "")]
    [InlineData(2, 2, "La")]
    public async Task Render_WhenBoundsAreTiny_SuppressesLegendAndLabelsBeforeDataAsync(
        int width,
        int height,
        string expectedLabel)
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series =
            [
                new ChartSeries("Alpha", [new ChartDataPoint("Label", 5)]),
                new ChartSeries("Beta", [new ChartDataPoint("Label", 5)])
            ],
            LegendPlacement = ChartLegendPlacement.Bottom,
            ShowCategoryLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(width, height),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(surface, width, height);
        var text = string.Concat(rows);
        text.ShouldNotContain("■");
        text.ShouldContain("█");

        if (expectedLabel.Length == 0)
        {
            text.ShouldNotContain("L");
        }
        else
        {
            rows[^1].ShouldBe(expectedLabel);
            rows[0].ShouldNotContain("L");
        }
    }

    /// <summary>Verifies a vertical value label sits one row above its bar, is clamped to the top
    /// row for a full bar, slides left to stay whole near the right plot edge, and is dropped
    /// outright - never clipped into a different number - when it is wider than the plot.</summary>
    [Theory]
    [InlineData(5d, 1, 4, " |5|█|█")]
    [InlineData(10d, 1, 4, "█|█|█|█")]
    [InlineData(5d, 1, 1, "5")]
    [InlineData(10d, 2, 3, "10|█ |█ ")]
    public async Task Render_WhenVerticalValueLabelIsRequested_KeepsItWholeOrDropsItAsync(
        double value,
        int width,
        int height,
        string expected)
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", value)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false,
            ShowValueLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(width, height),
            TestContext.Current.CancellationToken);

        // Assert
        string.Join("|", Rows(surface, width, height)).ShouldBe(expected);
    }

    /// <summary>Verifies a value label wider than the plot is suppressed while a fitting one keeps its
    /// blank gap after the bar.</summary>
    [Theory]
    [InlineData(123456789d, "████████")]
    [InlineData(10d, "████████")]
    [InlineData(5d, "████ 5  ")]
    public async Task Render_WhenHorizontalValueLabelIsRequested_ShowsItOnlyWhenItFitsAsync(double value, string expected)
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", value)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false,
            ShowValueLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected);
    }

    /// <summary>Verifies two horizontal series in glyph fill mode occupy their own lanes inside one
    /// category band and each bar ends at its own value boundary.</summary>
    [Fact]
    public async Task Render_WhenHorizontalGlyphModeHasTwoSeries_DrawsOneLanePerSeriesAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series =
            [
                new ChartSeries("Full", [new ChartDataPoint("c", 4)]),
                new ChartSeries("Half", [new ChartDataPoint("c", 2)])
            ],
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };
        chart.Style = chart.ActualStyle with { FillMode = ChartFillMode.Glyph };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ████
                             ██
                             """);
    }

    /// <summary>Verifies glyph-mode area columns grow toward the zero baseline from both sides for
    /// mixed-sign data.</summary>
    [Fact]
    public async Task Render_WhenAreaGlyphModeHasMixedSigns_FillsTowardTheBaselineFromBothSidesAsync()
    {
        // Arrange
        var chart = new AreaChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("neg", -2), new ChartDataPoint("pos", 2)])],
            Scale = new ChartScale(-2, 2, includeZero: true),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };
        chart.Style = chart.ActualStyle with { FillMode = ChartFillMode.Glyph };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("█");
        surface.Cell(new Point(2, 1)).Text.ShouldBe("█");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("•");
    }

    /// <summary>Verifies the fractional area fill hangs negative-only data down from a zero baseline
    /// pinned to the top row, paints every interpolated column, and leaves the bottom row blank
    /// until a value reaches the scale minimum.</summary>
    [Fact]
    public async Task Render_WhenAreaValuesAreNegativeOnly_PaintsEveryColumnBelowTheBaselineAsync()
    {
        // Arrange
        var chart = new AreaChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("a", -3), new ChartDataPoint("b", -1)])],
            Scale = new ChartScale(-4, 0, includeZero: true),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 4),
            TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(surface, 4, 4);
        rows[0].ShouldBe("████");
        rows[3].ShouldBe("    ");
        var depths = Enumerable.Range(0, 4).Select(column => rows.Count(row => row[column] != ' ')).ToArray();
        depths[0].ShouldBe(3);
        depths[3].ShouldBeInRange(1, 2);

        // The fill is interpolated at column centers, so depth can only fall from -3 toward -1.
        for (var column = 1; column < 4; column++)
        {
            depths[column].ShouldBeLessThanOrEqualTo(depths[column - 1]);
        }
    }

    /// <summary>Verifies observable point mutations after layout - add, remove, clear - reflow the
    /// rendered markers on the next frame.</summary>
    [Fact]
    public async Task Points_WhenMutatedAfterLayout_RerendersMarkersAsync()
    {
        // Arrange
        var series = new ChartSeries("A", [new ChartDataPoint("a", 0), new ChartDataPoint("b", 2)]);
        var chart = new LineChart
        {
            Series = [series],
            Scale = new ChartScale(0, 2, includeZero: false),
            LegendPlacement = ChartLegendPlacement.Hidden,
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(5, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("●");

        // Act add a third point back at the bottom
        await surface.UpdateAsync(() => series.Points.Add(new ChartDataPoint("c", 0)), "add a point");

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("●");

        // Act remove the middle point
        await surface.UpdateAsync(() => series.Points.RemoveAt(1), "remove the middle point");

        // Assert
        string.Concat(Rows(surface, 5, 3)).Count(cell => cell == '●').ShouldBe(2);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("●");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("●");

        // Act clear
        await surface.UpdateAsync(series.Points.Clear, "clear every point");

        // Assert
        Rows(surface, 5, 3).ShouldAllBe(row => row.Trim().Length == 0);
    }

    /// <summary>Verifies replacing the Series list after layout swaps the rendered data and that an
    /// empty replacement blanks the plot.</summary>
    [Fact]
    public async Task Series_WhenReplacedAfterLayout_RendersTheNewMembershipAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", 5)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("██  ");

        // Act
        await surface.UpdateAsync(() => chart.Series = [], "drop every series");
        surface.ShouldRender("    ");
        await surface.UpdateAsync(
            () => chart.Series = [new ChartSeries("B", [new ChartDataPoint("v", 10)])],
            "assign a full-value series");

        // Assert
        surface.ShouldRender("████");
    }

    /// <summary>Verifies a series rename after layout repaints the legend text.</summary>
    [Fact]
    public async Task SeriesName_WhenChangedAfterLayout_UpdatesTheLegendAsync()
    {
        // Arrange
        var first = new ChartSeries("A", [new ChartDataPoint("1", 1)]);
        var second = new ChartSeries("B", [new ChartDataPoint("1", 2)]);
        var chart = new LineChart
        {
            Series = [first, second],
            LegendPlacement = ChartLegendPlacement.Top,
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        Rows(surface, 12, 4)[0].ShouldBe("■ A ■ B     ");

        // Act
        await surface.UpdateAsync(() => first.Name = "Zeta", "rename the first series");

        // Assert
        Rows(surface, 12, 4)[0].ShouldBe("■ Zeta ■ B  ");
    }

    /// <summary>Verifies point and series color assignments after layout repaint the affected cells
    /// in precedence order: point over series over palette.</summary>
    [Fact]
    public async Task Colors_WhenAssignedAfterLayout_RepaintInPrecedenceOrderAsync()
    {
        // Arrange
        var point = new ChartDataPoint("v", 10);
        var series = new ChartSeries("A", [point]);
        var chart = new HorizontalBarChart
        {
            Series = [series],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        var palette = surface.Cell(new Point(0, 0)).Style.Foreground;

        // Act and assert series color
        await surface.UpdateAsync(() => series.Color = ReferenceColors.Get(2), "assign a series color");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(2));
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldNotBe(palette);

        // Act and assert point color wins over series color
        await surface.UpdateAsync(() => point.Color = ReferenceColors.Get(4), "assign a point color");
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(4));

        // Act and assert clearing the point color falls back to the series color
        await surface.UpdateAsync(() => point.Color = null, "clear the point color");
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(2));
    }

    /// <summary>Verifies assigning a local chart style after layout repaints the palette color and
    /// clearing it restores the themed color.</summary>
    [Fact]
    public async Task Style_WhenAssignedAfterLayout_RepaintsPaletteColorAndClearingRestoresAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", 10)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        var themed = surface.Cell(new Point(0, 0)).Style.Foreground;
        themed.ShouldNotBe(ReferenceColors.Get(5));

        // Act
        await surface.UpdateAsync(
            () => chart.Style = chart.ActualStyle with { PrimaryColor = ReferenceColors.Get(5) },
            "assign a local primary color");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(5));
        await surface.UpdateAsync(() => chart.Style = null, "clear the local style");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(themed);
    }

    /// <summary>Verifies a resize re-maps the plot onto the new bounds.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSurfaceGrows_RemapsBarsToTheNewWidthAsync()
    {
        // Arrange
        var chart = new HorizontalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", 5)])],
            Scale = new ChartScale(0, 10, includeZero: true),
            ShowCategoryLabels = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("██  ");

        // Act
        await surface.ResizeAsync(new Size(8, 1));

        // Assert
        surface.ShouldRender("████    ");
    }

    /// <summary>Verifies a sparkline with a single point still paints one column.</summary>
    [Fact]
    public async Task Render_WhenSparklineHasSinglePoint_PaintsOneColumnAsync()
    {
        // Arrange
        var chart = new Sparkline
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("v", 3)])]
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        Rows(surface, 4, 1)[0].Count(cell => cell != ' ').ShouldBe(1);
    }

    /// <summary>Verifies a vertical chart with category labels on a one-row surface draws only data.</summary>
    [Fact]
    public async Task Render_WhenVerticalPlotIsOneRow_SuppressesCategoryLabelsAsync()
    {
        // Arrange
        var chart = new VerticalBarChart
        {
            Series = [new ChartSeries("A", [new ChartDataPoint("Label", 5)])],
            ShowCategoryLabels = true
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            chart,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        Rows(surface, 3, 1)[0].ShouldContain("█");
        Rows(surface, 3, 1)[0].ShouldNotContain("L");
    }

    private static ChartControlBase CreateChart(string kind) => kind switch
    {
        "horizontal" => new HorizontalBarChart(),
        "vertical" => new VerticalBarChart(),
        "line" => new LineChart(),
        "area" => new AreaChart(),
        "sparkline" => new Sparkline(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown chart kind.")
    };

    private static string[] Rows(ComponentSurface surface, int width, int height)
    {
        var rows = new string[height];

        for (var y = 0; y < height; y++)
        {
            var builder = new StringBuilder(width);

            for (var x = 0; x < width; x++)
            {
                var cell = surface.Cell(new Point(x, y));
                _ = builder.Append(cell.Continuation ? string.Empty : cell.Text.Length == 0 ? " " : cell.Text);
            }

            rows[y] = builder.ToString();
        }

        return rows;
    }
}
