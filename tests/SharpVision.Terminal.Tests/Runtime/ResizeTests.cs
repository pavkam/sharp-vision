using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using CellMetrics = SharpVision.Terminal.Geometry.Metrics;

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies cell/pixel resize geometry, suspension, and asynchronous delivery.
/// </summary>
public sealed class ResizeTests
{
    /// <summary>
    /// Verifies positive cell and pixel sizes derive positive cell metrics.
    /// </summary>
    [Fact]
    public void Constructor_WhenCellsAndPixelsArePositive_DerivesMetrics()
    {
        var dimensions = new Dimensions(new Size(80, 24), new Size(800, 480));

        dimensions.CellMetrics.ShouldBe(new CellMetrics(10, 20));
        dimensions.IsSuspended.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies zero-cell geometry is a valid suspended state without metrics.
    /// </summary>
    [Fact]
    public void Constructor_WhenCellsAreZero_CreatesSuspendedDimensions()
    {
        var dimensions = new Dimensions(new Size(0, 0), new Size(800, 480));

        dimensions.CellMetrics.ShouldBeNull();
        dimensions.IsSuspended.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies resize sources wait without spinning and preserve newest queued values.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenResizeArrives_DeliversDimensionsAsync()
    {
        await using var source = new FakeResizeSource();
        var pending = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        pending.IsCompleted.ShouldBeFalse();
        var expected = new Dimensions(new Size(120, 40), new Size(1200, 800));
        source.Resize(expected);

        (await pending).ShouldBe(expected);
    }
}
