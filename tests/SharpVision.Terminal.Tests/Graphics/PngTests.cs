// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using System.IO.Compression;

using SharpVision.Terminal.Graphics;

/// <summary>Verifies bounded structural PNG ownership and recovery.</summary>
public sealed class PngTests
{
    /// <summary>Verifies a structurally valid PNG is copied with IHDR dimensions.</summary>
    [Fact]
    public void FromPng_WhenStructureIsValid_OwnsBytesAndDimensions()
    {
        var source = CreatePng(3, 2);

        var image = GraphicsImage.FromPng(source);
        source.AsSpan().Clear();
        var copied = new byte[image.ByteCount];
        _ = image.CopyTo(copied);

        image.Size.ShouldBe(new Size(3, 2));
        image.Format.ShouldBe(Format.Png);
        copied[0].ShouldBe((byte) 137);
        copied.AsSpan(12, 4).SequenceEqual("IHDR"u8).ShouldBeTrue();
        copied.AsSpan(49, 4).SequenceEqual("IEND"u8).ShouldBeTrue();
    }

    /// <summary>Verifies malformed and truncated PNG structures are rejected.</summary>
    [Fact]
    public void FromPng_WhenStructureIsMalformed_ThrowsDocumentedException()
    {
        var invalidSignature = CreatePng(1, 1);
        invalidSignature[0] = 0;
        var invalidHeader = CreatePng(1, 1);
        invalidHeader[24] = 1;
        var missingEnd = CreatePng(1, 1)[..45];
        var oversizedChunk = CreatePng(1, 1);
        oversizedChunk[33] = 127;

        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(invalidSignature));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(invalidHeader));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(missingEnd));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(oversizedChunk));
    }

    /// <summary>Verifies PNG dimensions and source bytes obey caller policy.</summary>
    [Fact]
    public void FromPng_WhenPolicyIsExceeded_ThrowsBeforeOwnership()
    {
        var source = CreatePng(2, 2);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromPng(
                source,
                new Limits(maxDimension: 1, maxPixels: 1, maxSourceBytes: 57)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromPng(
                source,
                new Limits(maxDimension: 2, maxPixels: 4, maxSourceBytes: 56)));
    }

    #region DecodeRgba

    /// <summary>Verifies all five PNG scanline filter types reconstruct exact raw bytes, using a
    /// hand-computed RGB fixture covering None, Sub, Up, Average, and Paeth on successive rows.</summary>
    [Fact]
    public void DecodeRgba_WhenScanlinesUseEveryFilterType_ReconstructsExactBytes()
    {
        byte[][] filtered =
        [
            [10, 20, 30, 40, 50, 60],
            [15, 25, 35, 30, 30, 30],
            [246, 246, 246, 246, 246, 246],
            [18, 3, 244, 63, 53, 43],
            [80, 80, 80, 226, 226, 226]
        ];
        byte[] filterTypes = [0, 1, 2, 3, 4];
        var source = CreateDecodablePng(2, 5, colorType: 2, bitDepth: 8, filterTypes, filtered);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe(
        [
            10, 20, 30, 255, 40, 50, 60, 255,
            15, 25, 35, 255, 45, 55, 65, 255,
            5, 15, 25, 255, 35, 45, 55, 255,
            20, 10, 0, 255, 90, 80, 70, 255,
            100, 90, 80, 255, 70, 60, 50, 255
        ]);
    }

    /// <summary>Verifies grayscale pixels expand to opaque RGB triples.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsGrayscale_ExpandsToOpaqueRgb()
    {
        byte[] row = [0, 128, 255];
        var source = CreateDecodablePng(3, 1, colorType: 0, bitDepth: 8, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0, 0, 0, 255, 128, 128, 128, 255, 255, 255, 255, 255]);
    }

    /// <summary>Verifies grayscale and truecolor tRNS samples become transparent while adjacent
    /// colors remain opaque.</summary>
    [Fact]
    public void DecodeRgba_WhenGrayscaleOrTruecolorMatchesTrns_SetsAlphaToTransparent()
    {
        var grayscale = CreateDecodablePng(
            2,
            1,
            colorType: 0,
            bitDepth: 8,
            [0],
            [[42, 43]],
            trns: [0, 42]);
        var truecolor = CreateDecodablePng(
            2,
            1,
            colorType: 2,
            bitDepth: 8,
            [0],
            [[1, 2, 3, 1, 2, 4]],
            trns: [0, 1, 0, 2, 0, 3]);

        var grayscaleDecoded = grayscale.AsSpan().DecodeRgba();
        var truecolorDecoded = truecolor.AsSpan().DecodeRgba();

        grayscaleDecoded.ShouldBe([42, 42, 42, 0, 43, 43, 43, 255]);
        truecolorDecoded.ShouldBe([1, 2, 3, 0, 1, 2, 4, 255]);
    }

    /// <summary>Verifies grayscale-with-alpha pixels expand to RGB while preserving alpha.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsGrayscaleAlpha_ExpandsGrayAndPreservesAlpha()
    {
        byte[] row = [10, 200, 250, 5];
        var source = CreateDecodablePng(2, 1, colorType: 4, bitDepth: 8, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([10, 10, 10, 200, 250, 250, 250, 5]);
    }

    /// <summary>Verifies indexed pixels resolve through PLTE, with tRNS supplying per-index alpha
    /// and any index beyond tRNS defaulting to opaque.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsIndexed_ResolvesPaletteAndTrnsAlpha()
    {
        byte[] palette = [10, 20, 30, 40, 50, 60, 70, 80, 90];
        byte[] trns = [0, 128];
        byte[] row = [0, 1, 2];
        var source = CreateDecodablePng(3, 1, colorType: 3, bitDepth: 8, [0], [row], palette, trns);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([10, 20, 30, 0, 40, 50, 60, 128, 70, 80, 90, 255]);
    }

    /// <summary>Verifies RGBA pixels pass through unchanged.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsRgba_PassesThroughBytes()
    {
        byte[] row = [1, 2, 3, 4, 5, 6, 7, 8];
        var source = CreateDecodablePng(2, 1, colorType: 6, bitDepth: 8, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe(row);
    }

    /// <summary>Verifies an interlaced source is reported rather than misdecoded.</summary>
    [Fact]
    public void DecodeRgba_WhenSourceIsInterlaced_ThrowsNotSupported()
    {
        var source = CreateDecodablePng(1, 1, colorType: 2, bitDepth: 8, [0], [[1, 2, 3]], interlace: 1);

        _ = Should.Throw<NotSupportedException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies a bit depth other than 8 is reported rather than misdecoded.</summary>
    [Fact]
    public void DecodeRgba_WhenBitDepthIsNotEight_ThrowsNotSupported()
    {
        var source = CreateDecodablePng(8, 1, colorType: 0, bitDepth: 1, [0], [[0xFF]]);

        _ = Should.Throw<NotSupportedException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies an indexed source without a PLTE chunk is reported rather than misdecoded.</summary>
    [Fact]
    public void DecodeRgba_WhenIndexedSourceHasNoPalette_ThrowsNotSupported()
    {
        var source = CreateDecodablePng(1, 1, colorType: 3, bitDepth: 8, [0], [[0]]);

        _ = Should.Throw<NotSupportedException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies decompressed bytes beyond the declared scanlines are rejected.</summary>
    [Fact]
    public void DecodeRgba_WhenCompressedDataExceedsDeclaredDimensions_Throws()
    {
        var source = CreateDecodablePng(
            1,
            1,
            colorType: 0,
            bitDepth: 8,
            [0],
            [[42]],
            trailingScanlineData: [99]);

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies malformed zlib data is normalized to the documented PNG exception.</summary>
    [Fact]
    public void DecodeRgba_WhenCompressedDataIsInvalid_ThrowsArgumentException()
    {
        var source = CreateDecodablePng(1, 1, colorType: 0, bitDepth: 8, [0], [[42]]);
        source[41] = 0;

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
    }

    #endregion

    private static byte[] CreateDecodablePng(
        int width,
        int height,
        byte colorType,
        byte bitDepth,
        byte[] filterTypes,
        byte[][] filteredRows,
        byte[]? palette = null,
        byte[]? trns = null,
        byte interlace = 0,
        byte[]? trailingScanlineData = null)
    {
        using var buffer = new MemoryStream();
        buffer.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        WriteChunk(buffer, "IHDR"u8, ihdr =>
        {
            WriteInt32(ihdr, width);
            WriteInt32(ihdr[4..], height);
            ihdr[8] = bitDepth;
            ihdr[9] = colorType;
            ihdr[10] = 0;
            ihdr[11] = 0;
            ihdr[12] = interlace;
        }, 13);

        if (palette is not null)
        {
            WriteRawChunk(buffer, "PLTE"u8, palette);
        }

        if (trns is not null)
        {
            WriteRawChunk(buffer, "tRNS"u8, trns);
        }

        using (var scanlines = new MemoryStream())
        {
            for (var row = 0; row < filteredRows.Length; row++)
            {
                scanlines.WriteByte(filterTypes[row]);
                scanlines.Write(filteredRows[row]);
            }

            if (trailingScanlineData is not null)
            {
                scanlines.Write(trailingScanlineData);
            }

            using var compressed = new MemoryStream();

            using (var zlib = new ZLibStream(compressed, CompressionLevel.NoCompression, leaveOpen: true))
            {
                scanlines.Position = 0;
                scanlines.CopyTo(zlib);
            }

            WriteRawChunk(buffer, "IDAT"u8, compressed.ToArray());
        }

        WriteRawChunk(buffer, "IEND"u8, []);
        return buffer.ToArray();
    }

    private static void WriteChunk(MemoryStream buffer, ReadOnlySpan<byte> type, Action<Span<byte>> fill, int length)
    {
        Span<byte> data = new byte[length];
        fill(data);
        WriteRawChunk(buffer, type, data);
    }

    private static void WriteRawChunk(MemoryStream buffer, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteInt32(length, data.Length);
        buffer.Write(length);
        buffer.Write(type);
        buffer.Write(data);
        buffer.Write([0, 0, 0, 0]);
    }

    private static byte[] CreatePng(int width, int height)
    {
        byte[] result =
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 0, 0, 0, 0, 0,
            8, 6, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, 73, 68, 65, 84,
            0, 0, 0, 0,
            0, 0, 0, 0, 73, 69, 78, 68,
            0, 0, 0, 0
        ];
        WriteInt32(result.AsSpan(16, 4), width);
        WriteInt32(result.AsSpan(20, 4), height);
        return result;
    }

    private static void WriteInt32(Span<byte> destination, int value)
    {
        destination[0] = (byte) (value >> 24);
        destination[1] = (byte) (value >> 16);
        destination[2] = (byte) (value >> 8);
        destination[3] = (byte) value;
    }
}
