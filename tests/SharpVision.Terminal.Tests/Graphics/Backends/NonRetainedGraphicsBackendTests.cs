// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Backends;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>Proves the shared non-retained sixel/iTerm backend degrades deterministically instead
/// of crashing when placements combine to exceed the prepared-transaction byte budget.</summary>
public sealed class NonRetainedGraphicsBackendTests
{
    /// <summary>Verifies placements that each individually fit the full budget in isolation, but
    /// whose combined encoded bytes exceed it once written into the shared frame buffer, are
    /// skipped instead of crashing Prepare() partway through the frame.</summary>
    [Fact]
    public void Prepare_WhenCombinedPlacementsExceedRemainingBudget_SkipsOverflowingPlacementsInstead()
    {
        var image = GradientImage();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, maxPreparedBytes: 600);
        using var frame = new Frame(new Size(16, 1));

        for (var index = 0; index < 4; index++)
        {
            frame.Canvas.DrawImage(image, new Rect(index * 4, 0, 4, 1), PlacementMode.Stretch);
        }

        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        var context = new GraphicsContext(profile, new CellMetrics(1, 24));

        var result = Should.NotThrow(() => backend.Prepare(null, frame, full: true, context));

        result.Placements.ShouldBeLessThan(4);
        result.Placements.ShouldBeGreaterThan(0);
        result.SkippedPlacements.Count.ShouldBe(4 - result.Placements);
        result.SkippedPlacements.ShouldAllBe(
            diagnostic => diagnostic.Reason == GraphicsPlacementSkipReason.OutputLimitExceeded);
    }

    /// <summary>Verifies that when a placement's sixel encoding fails outright (budget too small
    /// for even the smallest frame) and no fallback succeeds, WritePlacements produces exactly
    /// zero bytes rather than an orphaned cursor-move escape left over from the attempt that never
    /// actually wrote its sixel data.</summary>
    [Fact]
    public void Prepare_WhenSixelWriteFailsAndNoPlacementSucceeds_WritePlacementsProducesNoBytes()
    {
        var image = GradientImage();
        using var backend = new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false, maxPreparedBytes: 20);
        using var frame = new Frame(new Size(4, 1));
        frame.Canvas.DrawImage(image, new Rect(0, 0, 4, 1), PlacementMode.Stretch);

        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        var context = new GraphicsContext(profile, new CellMetrics(1, 24));

        var result = Should.NotThrow(() => backend.Prepare(null, frame, full: true, context));

        result.Placements.ShouldBe(0);
        var destination = new ArrayBufferWriter<byte>();
        backend.WritePlacements(destination);
        destination.WrittenCount.ShouldBe(0);
    }

    // A gradient (rather than solid) image keeps sixel run-length compression from collapsing
    // each placement to a handful of bytes, so a small handful of placements can reach a shared
    // budget that Writer.Write's own conservative worst-case check would clear individually.
    private static GraphicsImage GradientImage()
    {
        var bytes = new byte[4 * 4 * 4];

        for (var index = 0; index < 16; index++)
        {
            bytes[(index * 4) + 0] = (byte) (index * 17);
            bytes[(index * 4) + 1] = (byte) (index * 7);
            bytes[(index * 4) + 2] = (byte) (index * 3);
            bytes[(index * 4) + 3] = 255;
        }

        return GraphicsImage.FromRgba(new Size(4, 4), bytes);
    }
}
