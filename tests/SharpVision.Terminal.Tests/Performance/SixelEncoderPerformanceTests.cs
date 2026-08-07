// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Performance;

using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Sixel;

/// <summary>Gates the cost of a sixel write that is guaranteed to exceed its output policy.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class SixelEncoderPerformanceTests
{
    /// <summary>Verifies a write whose zero-color worst-case bound already exceeds a tiny policy is
    /// rejected without renting or sampling the destination raster — the bound is monotone in
    /// palette size, so the zero-color bound is a valid lower bound for any actual content and
    /// proves rejection before any raster work happens.</summary>
    [Fact]
    public void Write_WhenBoundGuaranteesRejection_RejectsWithoutRentingOrSamplingTheRaster()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var output = new ArrayBufferWriter<byte>();
        var destination = new Size(2_048, 2_048);

        var before = GC.GetAllocatedBytesForCurrentThread();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            destination,
            PlacementMode.Stretch,
            output,
            maxOutputBytes: 1));

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WrittenCount.ShouldBe(0);

        // Renting or clearing the 2048x2048 raster this destination would otherwise require
        // touches over 4 million bytes; a guaranteed rejection should stay far under that.
        allocated.ShouldBeLessThan(4_096);
    }
}
