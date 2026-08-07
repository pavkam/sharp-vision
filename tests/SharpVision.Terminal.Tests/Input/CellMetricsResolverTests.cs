// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using SharpVision.Terminal.Xterm;

using Metrics = CellMetrics;

/// <summary>Verifies the cell-metrics inference resolver extracted from InputDecoder.</summary>
public sealed class CellMetricsResolverTests
{
    /// <summary>Verifies exact local geometry is preferred over a queried uniform cell size.</summary>
    [Fact]
    public void SetGeometry_WhenExactLocalGeometryIsSupplied_YieldsExactCellMetrics()
    {
        var resolver = new CellMetricsResolver(new Metrics(10, 20));

        resolver.SetGeometry(new Size(80, 24), new Size(800, 480));

        resolver.Current.ShouldBe(new Metrics(new Size(80, 24), new Size(800, 480)));
    }

    /// <summary>Verifies non-positive local dimensions are rejected and fall back to any queried
    /// uniform cell-pixel measurement instead of producing degenerate metrics.</summary>
    [Fact]
    public void SetGeometry_WhenDimensionsAreNotPositive_FallsBackToQueriedUniformMetrics()
    {
        var resolver = new CellMetricsResolver(new Metrics(10, 20));

        resolver.SetGeometry(new Size(0, 24), new Size(800, 480));

        resolver.Current.ShouldBe(new Metrics(10, 20));
    }

    /// <summary>Verifies a queried window-cells and window-pixels pair together yields exact
    /// per-cell metrics once both families have arrived.</summary>
    [Fact]
    public void Apply_WhenWindowCellsAndPixelsBothArrive_YieldsExactCellMetrics()
    {
        var resolver = new CellMetricsResolver(null);

        resolver.Apply(new MetricsResponse(ResponseKind.WindowCells, new Size(80, 24)));
        resolver.Current.ShouldBeNull();

        resolver.Apply(new MetricsResponse(ResponseKind.WindowPixels, new Size(800, 480)));

        resolver.Current.ShouldBe(new Metrics(new Size(80, 24), new Size(800, 480)));
    }

    /// <summary>Verifies local geometry set via SetGeometry supersedes a stale queried window
    /// measurement rather than being combined with it.</summary>
    [Fact]
    public void SetGeometry_AfterQueriedWindowMetricsWereApplied_SupersedesTheQueriedValue()
    {
        var resolver = new CellMetricsResolver(null);
        resolver.Apply(new MetricsResponse(ResponseKind.WindowCells, new Size(80, 24)));
        resolver.Apply(new MetricsResponse(ResponseKind.WindowPixels, new Size(800, 480)));
        resolver.Current.ShouldBe(new Metrics(new Size(80, 24), new Size(800, 480)));

        resolver.SetGeometry(new Size(100, 30), null);

        resolver.Current.ShouldBeNull();
    }
}
