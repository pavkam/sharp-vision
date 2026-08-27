// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Sixel;

using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Sixel;
using SharpVision.Terminal.Tests.Support;

/// <summary>Proves deterministic bounded sixel encoding from owned RGBA pixels.</summary>
public sealed class SixelWriterTests
{
    /// <summary>Verifies raster attributes, palette definition, transparency, and canonical framing.</summary>
    [Fact]
    public void Write_WhenPixelsIncludeTransparency_EmitsExactCanonicalBytes()
    {
        var pixels = new byte[2 * 6 * 4];

        for (var y = 0; y < 6; y++)
        {
            pixels[y * 8] = 255;
            pixels[(y * 8) + 3] = 255;
        }

        var image = GraphicsImage.FromRgba(new Size(2, 6), pixels);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 2, 6),
            new Size(2, 6),
            PlacementMode.Stretch,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;2;6#0;2;100;0;0#0~\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies repeated sixels and graphics newlines use the DEC grammar.</summary>
    [Fact]
    public void Write_WhenBandsContainRuns_UsesRepeatAndGraphicsNewline()
    {
        var pixels = new byte[5 * 7 * 4];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
            pixels[index + 3] = 255;
        }

        var image = GraphicsImage.FromRgba(new Size(5, 7), pixels);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 5, 7),
            new Size(5, 7),
            PlacementMode.Stretch,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;5;7#0;2;100;0;0#0!5~-#0!5@\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies palette assignment is independent of pixel discovery order.</summary>
    [Fact]
    public void Write_WhenColorsAreDiscoveredInReverseOrder_SortsPaletteDeterministically()
    {
        byte[] pixels =
        [
            255, 0, 0, 255,
            0, 0, 255, 255
        ];
        var image = GraphicsImage.FromRgba(new Size(2, 1), pixels);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 2, 1),
            new Size(2, 1),
            PlacementMode.Stretch,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;2;1#0;2;0;0;100#1;2;100;0;0#0?@\u0024#1@\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies the explicit source rectangle is sampled instead of decoding unrelated formats.</summary>
    [Fact]
    public void Write_WhenSourceIsClipped_SamplesOnlyTheRequestedPixels()
    {
        byte[] pixels =
        [
            255, 0, 0, 255,
            0, 0, 255, 255
        ];
        var image = GraphicsImage.FromRgba(new Size(2, 1), pixels);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(1, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;1;1#0;2;0;0;100#0@\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies contain preserves aspect ratio with transparent centered padding.</summary>
    [Fact]
    public void Write_WhenModeIsContain_CentersCompleteSourceWithTransparentPadding()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(3, 6),
            PlacementMode.Contain,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;3;6#0;2;100;0;0#0MMM\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies cover preserves aspect ratio and crops excess source pixels.</summary>
    [Fact]
    public void Write_WhenModeIsCover_CropsSourceToFillRaster()
    {
        var image = GraphicsImage.FromRgba(
            new Size(1, 2),
            [255, 0, 0, 255, 0, 0, 255, 255]);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 2),
            new Size(1, 1),
            PlacementMode.Cover,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001bP0;1;0q\"1;1;1;1#0;2;100;0;0#0@\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies PNG input is rejected without attempting a decode or mutating output.</summary>
    [Fact]
    public void Write_WhenImageIsPng_RejectsWithoutWriting()
    {
        var image = GraphicsImage.FromPng(PngTestData.CreateContainer());
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<NotSupportedException>(() => SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies a finite output policy fails atomically before exposing partial DCS bytes.</summary>
    [Fact]
    public void Write_WhenOutputLimitIsExceeded_RejectsWithoutWriting()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output,
            maxOutputBytes: 8));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies destination failures retain their original type and message.</summary>
    [Fact]
    public void Write_WhenDestinationFails_DoesNotMisreportOutputPolicy()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var output = new SixelFailingBufferWriter();

        var exception = Should.Throw<InvalidOperationException>(() => SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output));

        exception.Message.ShouldBe("destination failure");
    }

    /// <summary>Verifies rasterization samples each destination pixel exactly once before emission.</summary>
    [Fact]
    public void Encode_WhenRasterIsLarge_SamplesEachDestinationPixelOnce()
    {
        var pixels = new byte[100 * 60 * 4];
        pixels.AsSpan().Fill(255);
        var image = GraphicsImage.FromRgba(new Size(100, 60), pixels);
        var output = new ArrayBufferWriter<byte>();

        var samples = SixelEncoder.Encode(
            image,
            new Rect(0, 0, 100, 60),
            new Size(100, 60),
            PlacementMode.Stretch,
            output,
            maxOutputBytes: 2_000);

        samples.ShouldBe(6_000);
        output.WrittenCount.ShouldBeLessThan(2_000);
    }

    /// <summary>Verifies a color's plane bits from an earlier band never leak into a later band that
    /// reuses the same identifier at different bit positions — the targeted per-band clear introduced
    /// to avoid a full-palette-width clear on every band must still zero exactly what the previous
    /// band touched.</summary>
    [Fact]
    public void Write_WhenSameColorReappearsInALaterBand_DoesNotLeakBitsAcrossBands()
    {
        var pixels = new byte[1 * 12 * 4];
        pixels[0] = 255;
        pixels[3] = 255;
        pixels[11 * 4] = 255;
        pixels[(11 * 4) + 3] = 255;

        var image = GraphicsImage.FromRgba(new Size(1, 12), pixels);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 12),
            new Size(1, 12),
            PlacementMode.Stretch,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "P0;1;0q\"1;1;1;12#0;2;100;0;0#0@-#0_\\"u8.ToArray());
    }

    /// <summary>Verifies the conservative plane bound is checked without enumerating a huge raster.</summary>
    [Fact]
    public void CalculateMaximumBytes_WhenPaletteAndRasterAreLarge_RemainsCheckedAndFinite()
    {
        var maximum = SixelEncoder.CalculateMaximumBytes(
            new Size(4_096, 4_096),
            colorCount: 216);

        maximum.ShouldBe(605_017_397L);
        _ = Should.Throw<OverflowException>(() =>
            SixelEncoder.CalculateMaximumBytes(
                new Size(int.MaxValue, int.MaxValue),
                colorCount: 216));
    }

    /// <summary>Verifies a partially transparent pixel is blended against a supplied opaque
    /// background before quantization, rather than thresholded to fully opaque.</summary>
    [Fact]
    public void Write_WhenBackgroundIsSuppliedForPartialAlpha_BlendsBeforeQuantizing()
    {
        // Blend: red = (200*128 + 0*127) / 255 = 100, green = 0, blue = (0*128 + 200*127) / 255 = 99.
        // Quantized: red cube 2, green cube 0, blue cube 2 -> cube index 74 -> palette (40, 0, 40).
        var image = GraphicsImage.FromRgba(new Size(1, 1), [200, 0, 0, 128]);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output,
            background: Color.Rgb(0, 0, 200));

        output.WrittenSpan.ToArray().ShouldBe(
            "P0;1;0q\"1;1;1;1#0;2;40;0;40#0@\\"u8.ToArray());
    }

    /// <summary>Verifies fully transparent pixels stay transparent even when a background is
    /// supplied, since a real background never blends against a meaningless zero-alpha sample.</summary>
    [Fact]
    public void Write_WhenAlphaIsZero_RemainsTransparentEvenWithBackground()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [200, 0, 0, 0]);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output,
            background: Color.Rgb(0, 0, 200));

        output.WrittenSpan.ToArray().ShouldBe(
            "P0;1;0q\"1;1;1;1\\"u8.ToArray());
    }

    /// <summary>Verifies a fully opaque pixel is unaffected by a supplied background, since
    /// blending a fully opaque source against anything is a no-op.</summary>
    [Fact]
    public void Write_WhenAlphaIsFullyOpaque_IgnoresSuppliedBackground()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]);
        var output = new ArrayBufferWriter<byte>();

        SixelWriter.Write(
            image,
            new Rect(0, 0, 1, 1),
            new Size(1, 1),
            PlacementMode.Stretch,
            output,
            background: Color.Rgb(0, 0, 255));

        output.WrittenSpan.ToArray().ShouldBe(
            "P0;1;0q\"1;1;1;1#0;2;100;0;0#0@\\"u8.ToArray());
    }
}
