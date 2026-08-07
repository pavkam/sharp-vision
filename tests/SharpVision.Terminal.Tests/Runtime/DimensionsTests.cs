// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;


/// <summary>
/// Verifies cell/pixel resize geometry, suspension, and asynchronous delivery.
/// </summary>
public sealed class DimensionsTests
{
    /// <summary>
    /// Verifies positive cell and pixel sizes derive positive cell metrics.
    /// </summary>
    [Fact]
    public void Constructor_WhenCellsAndPixelsArePositive_DerivesMetrics()
    {
        var dimensions = new Dimensions(new Size(80, 24), new Size(800, 480));

        dimensions.CellMetrics.ShouldBe(new CellMetrics(
            new Size(80, 24),
            new Size(800, 480)));
        dimensions.Suspended.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies zero-cell geometry is a valid suspended state without metrics.
    /// </summary>
    [Fact]
    public void Constructor_WhenCellsAreZero_CreatesSuspendedDimensions()
    {
        var dimensions = new Dimensions(new Size(0, 0), new Size(800, 480));

        dimensions.CellMetrics.ShouldBeNull();
        dimensions.Suspended.ShouldBeTrue();
    }

    /// <summary>Verifies a pixel grid smaller than its cell grid is unavailable.</summary>
    [Fact]
    public void Constructor_WhenPixelsCannotRepresentEveryCell_OmitsMetrics()
    {
        var dimensions = new Dimensions(new Size(80, 24), new Size(79, 23));

        dimensions.CellMetrics.ShouldBeNull();
        dimensions.Pixels.ShouldBe(new Size(79, 23));
    }

    /// <summary>
    /// Verifies resize sources wait without spinning and preserve newest queued values.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenResizeArrives_DeliversDimensionsAsync()
    {
        await using FakeResizeSource source = new();
        var pending = source.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        pending.IsCompleted.ShouldBeFalse();
        var expected = new Dimensions(new Size(120, 40), new Size(1200, 800));
        source.Resize(expected);

        (await pending).ShouldBe(expected);
    }
}
