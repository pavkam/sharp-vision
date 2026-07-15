// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.GeometryCases;

using Metrics = CellMetrics;



/// <summary>Verifies exact rational and uniform pixel-to-cell grid mapping.</summary>
public sealed class MetricsTests
{
    private const int _seed = 0x0c3115;

    /// <summary>Verifies every uneven-grid pixel maps by the exact rational formula.</summary>
    [Fact]
    public void TryMap_WhenGridIsUneven_MapsEveryPixelExactly()
    {
        // Arrange
        var cells = new Size(10, 3);
        var pixels = new Size(101, 31);
        var metrics = new Metrics(cells, pixels);
        Point previous = default;

        // Act / Assert
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                metrics.TryMap(new Point(x, y), out var actual).ShouldBeTrue();
                actual.ShouldBe(new Point(
                    (int) ((long) x * cells.Width / pixels.Width),
                    (int) ((long) y * cells.Height / pixels.Height)));

                if (x > 0)
                {
                    actual.X.ShouldBeGreaterThanOrEqualTo(previous.X);
                }

                actual.X.ShouldBeInRange(0, cells.Width - 1);
                actual.Y.ShouldBeInRange(0, cells.Height - 1);
                previous = actual;
            }
        }
    }

    /// <summary>Verifies first, last, and outside coordinates remain distinct.</summary>
    [Fact]
    public void TryMap_WhenCoordinateReachesBoundary_RejectsOutsideDomain()
    {
        // Arrange
        var metrics = new Metrics(new Size(10, 3), new Size(101, 31));

        // Act / Assert
        metrics.TryMap(new Point(0, 0), out var first).ShouldBeTrue();
        first.ShouldBe(new Point(0, 0));
        metrics.TryMap(new Point(100, 30), out var last).ShouldBeTrue();
        last.ShouldBe(new Point(9, 2));
        metrics.TryMap(new Point(101, 30), out var right).ShouldBeFalse();
        right.ShouldBe(default);
        metrics.TryMap(new Point(100, 31), out var bottom).ShouldBeFalse();
        bottom.ShouldBe(default);
        metrics.TryMap(new Point(-1, 0), out var negative).ShouldBeFalse();
        negative.ShouldBe(default);
    }

    /// <summary>Verifies the compatibility constructor retains uniform unbounded mapping.</summary>
    [Fact]
    public void TryMap_WhenMetricsAreUniform_UsesExactCellExtents()
    {
        // Arrange
        var metrics = new Metrics(8, 16);

        // Act / Assert
        metrics.TryMap(new Point(16, 32), out var cells).ShouldBeTrue();
        cells.ShouldBe(new Point(2, 2));
        metrics.Cells.ShouldBeNull();
        metrics.Pixels.ShouldBeNull();
    }

    /// <summary>Verifies exact totals are validated before state is observable.</summary>
    [Fact]
    public void Constructor_WhenExactGridCannotMapEveryCell_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new Metrics(new Size(0, 1), new Size(8, 16)));
        _ = Should.Throw<ArgumentException>(
            () => new Metrics(new Size(9, 16), new Size(8, 16)));
    }

    /// <summary>Verifies seeded uneven axes agree with an independent formula.</summary>
    [Fact]
    public void TryMap_WhenGridsAreRandomized_RemainsExactAndBounded()
    {
        // Arrange
        var random = new Random(_seed);

        // Act / Assert
        for (var item = 0; item < 1_000; item++)
        {
            var cellWidth = random.Next(1, 21);
            var cellHeight = random.Next(1, 21);
            var pixelWidth = random.Next(cellWidth, (cellWidth * 8) + 1);
            var pixelHeight = random.Next(cellHeight, (cellHeight * 8) + 1);
            var metrics = new Metrics(
                new Size(cellWidth, cellHeight),
                new Size(pixelWidth, pixelHeight));
            var y = random.Next(pixelHeight);

            for (var x = 0; x < pixelWidth; x++)
            {
                metrics.TryMap(new Point(x, y), out var actual).ShouldBeTrue(
                    $"Seed {_seed}, grid {item}, pixel ({x},{y}).");
                actual.ShouldBe(
                    new Point(
                        (int) ((long) x * cellWidth / pixelWidth),
                        (int) ((long) y * cellHeight / pixelHeight)),
                    $"Seed {_seed}, grid {item}, pixel ({x},{y}).");
            }
        }
    }

    /// <summary>Verifies maximum integer totals remain within the 64-bit formula.</summary>
    [Fact]
    public void TryMap_WhenTotalsAreMaximum_DoesNotOverflow()
    {
        // Arrange
        var maximum = new Size(int.MaxValue, int.MaxValue);
        var metrics = new Metrics(maximum, maximum);
        var edge = new Point(int.MaxValue - 1, int.MaxValue - 1);

        // Act / Assert
        metrics.TryMap(edge, out var actual).ShouldBeTrue();
        actual.ShouldBe(edge);
    }
}
