// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using System.Buffers.Binary;
using System.IO.Compression;

using SharpVision.Terminal.Graphics;

/// <summary>Verifies bounded structural PNG scanline decoding and recovery.</summary>
public sealed class PngTests
{
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

    /// <summary>Verifies sub-8-bit depths are reported rather than misdecoded; Adam7 interlacing
    /// and depths below 8 bits per channel remain outside this decoder's scope.</summary>
    [Theory]
    [InlineData((byte) 1)]
    [InlineData((byte) 2)]
    [InlineData((byte) 4)]
    public void DecodeRgba_WhenBitDepthIsSubByte_ThrowsNotSupported(byte bitDepth)
    {
        var source = CreateDecodablePng(8, 1, colorType: 0, bitDepth: bitDepth, [0], [[0xFF]]);

        _ = Should.Throw<NotSupportedException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies 16-bit grayscale samples narrow to 8 bits by keeping the most significant
    /// byte of each big-endian sample.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsGrayscaleAndBitDepthIsSixteen_NarrowsToMostSignificantByte()
    {
        var row = Samples16(0x0000, 0x8042, 0xFFFF);
        var source = CreateDecodablePng(3, 1, colorType: 0, bitDepth: 16, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0, 0, 0, 255, 0x80, 0x80, 0x80, 255, 255, 255, 255, 255]);
    }

    /// <summary>Verifies 16-bit RGB samples each narrow independently to their most significant
    /// byte.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsRgbAndBitDepthIsSixteen_NarrowsEachChannel()
    {
        var row = Samples16(0x1020, 0x3040, 0x5060);
        var source = CreateDecodablePng(1, 1, colorType: 2, bitDepth: 16, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0x10, 0x30, 0x50, 255]);
    }

    /// <summary>Verifies 16-bit RGBA samples each narrow independently, including the alpha
    /// channel.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsRgbaAndBitDepthIsSixteen_NarrowsEachChannel()
    {
        var row = Samples16(0x1122, 0x3344, 0x5566, 0x7788);
        var source = CreateDecodablePng(1, 1, colorType: 6, bitDepth: 16, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0x11, 0x33, 0x55, 0x77]);
    }

    /// <summary>Verifies 16-bit grayscale-with-alpha samples narrow both the gray and alpha
    /// channels independently.</summary>
    [Fact]
    public void DecodeRgba_WhenColorTypeIsGrayscaleAlphaAndBitDepthIsSixteen_NarrowsBothChannels()
    {
        var row = Samples16(0xAB10, 0xCD20);
        var source = CreateDecodablePng(1, 1, colorType: 4, bitDepth: 16, [0], [row]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0xAB, 0xAB, 0xAB, 0xCD]);
    }

    /// <summary>Verifies a 16-bit tRNS comparison uses the full 16-bit sample rather than its
    /// narrowed 8-bit value: two distinct 16-bit gray levels that narrow to the same most
    /// significant byte must not both match a tRNS key that only equals one of them exactly.</summary>
    [Fact]
    public void DecodeRgba_WhenGrayscaleBitDepthIsSixteen_ComparesTrnsAtFullSampleWidth()
    {
        // 0x2A00 and 0x2A7F both narrow to the most significant byte 0x2A, but only 0x2A00
        // exactly matches the 16-bit tRNS key; a post-narrowing comparison would incorrectly
        // treat both as transparent.
        var row = Samples16(0x2A00, 0x2A7F);
        var source = CreateDecodablePng(2, 1, colorType: 0, bitDepth: 16, [0], [row], trns: [0x2A, 0x00]);

        var decoded = source.AsSpan().DecodeRgba();

        decoded.ShouldBe([0x2A, 0x2A, 0x2A, 0, 0x2A, 0x2A, 0x2A, 255]);
    }

    /// <summary>Verifies an indexed source without its required PLTE chunk is structurally rejected.</summary>
    [Fact]
    public void DecodeRgba_WhenIndexedSourceHasNoPalette_ThrowsArgumentException()
    {
        var source = CreateDecodablePng(1, 1, colorType: 3, bitDepth: 8, [0], [[0]]);

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
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

    /// <summary>Verifies every critical and decoded ancillary chunk is protected by its stored
    /// CRC before either structural publication or pixel decoding can consume it.</summary>
    /// <param name="chunkType">The chunk whose protected bytes are corrupted.</param>
    /// <param name="dataOffset">The data-byte offset to corrupt, or null to corrupt the stored CRC.</param>
    [Theory]
    [InlineData("IHDR", 2)]
    [InlineData("PLTE", 0)]
    [InlineData("tRNS", 0)]
    [InlineData("IDAT", 0)]
    [InlineData("IEND", null)]
    public void ReadSize_WhenAnyConsumedChunkCrcDoesNotMatch_RejectsContainer(
        string chunkType,
        int? dataOffset)
    {
        var source = CreateDecodablePng(
            1,
            1,
            colorType: 3,
            bitDepth: 8,
            [0],
            [[0]],
            palette: [10, 20, 30],
            trns: [255]);
        CorruptChunk(source, chunkType, dataOffset);

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().ReadSize());
        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies an unknown critical chunk is fatal at the shared ownership boundary,
    /// while an unknown ancillary chunk remains safely skippable.</summary>
    [Fact]
    public void ReadSize_WhenUnknownChunkCriticalityVaries_RejectsOnlyCriticalChunk()
    {
        var source = CreateDecodablePng(1, 1, colorType: 0, bitDepth: 8, [0], [[42]]);
        var critical = InsertBeforeIdat(source, "ABCD"u8, [1]);
        var ancillary = InsertBeforeIdat(source, "aBCD"u8, [1]);

        _ = Should.Throw<ArgumentException>(() => critical.AsSpan().ReadSize());
        _ = Should.Throw<ArgumentException>(() => critical.AsSpan().DecodeRgba());
        ancillary.AsSpan().ReadSize().ShouldBe(new Size(1, 1));
        ancillary.AsSpan().DecodeRgba().ShouldBe([42, 42, 42, 255]);
    }

    /// <summary>Verifies the shared container boundary rejects every forbidden critical-chunk
    /// transition before either dimensions or decoded pixels are published.</summary>
    /// <param name="violation">The critical-chunk ordering violation to construct.</param>
    [Theory]
    [InlineData("duplicate-header")]
    [InlineData("duplicate-palette")]
    [InlineData("late-palette")]
    [InlineData("late-transparency")]
    [InlineData("separated-data")]
    public void ReadSize_WhenCriticalChunkOrderIsInvalid_RejectsContainer(string violation)
    {
        var grayscale = CreateDecodablePng(1, 1, colorType: 0, bitDepth: 8, [0], [[42]]);
        var indexed = CreateDecodablePng(
            1,
            1,
            colorType: 3,
            bitDepth: 8,
            [0],
            [[0]],
            palette: [10, 20, 30]);
        var source = violation switch
        {
            "duplicate-header" => InsertBeforeChunk(
                grayscale,
                "IDAT"u8,
                "IHDR"u8,
                ReadChunkData(grayscale, "IHDR"u8)),
            "duplicate-palette" => InsertBeforeChunk(indexed, "IDAT"u8, "PLTE"u8, [40, 50, 60]),
            "late-palette" => InsertBeforeChunk(grayscale, "IEND"u8, "PLTE"u8, [10, 20, 30]),
            "late-transparency" => InsertBeforeChunk(grayscale, "IEND"u8, "tRNS"u8, [0, 42]),
            "separated-data" => InsertBeforeChunk(
                InsertBeforeChunk(grayscale, "IEND"u8, "aBCD"u8, [1]),
                "IEND"u8,
                "IDAT"u8,
                []),
            _ => throw new ArgumentOutOfRangeException(nameof(violation))
        };

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().ReadSize());
        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies consecutive IDAT chunks remain valid and decode as one compressed
    /// stream after enforcing the ordering state machine.</summary>
    [Fact]
    public void DecodeRgba_WhenDataChunksAreConsecutive_DecodesCombinedStream()
    {
        var source = CreateDecodablePng(1, 1, colorType: 0, bitDepth: 8, [0], [[42]]);
        var withEmptyLeadingData = InsertBeforeChunk(source, "IDAT"u8, "IDAT"u8, []);

        withEmptyLeadingData.AsSpan().ReadSize().ShouldBe(new Size(1, 1));
        withEmptyLeadingData.AsSpan().DecodeRgba().ShouldBe([42, 42, 42, 255]);
    }

    /// <summary>Verifies malformed PLTE shapes, forbidden color types, and indexed bit-depth
    /// limits are rejected by the structural ownership boundary.</summary>
    /// <param name="violation">The palette violation to construct.</param>
    [Theory]
    [InlineData("empty")]
    [InlineData("partial-entry")]
    [InlineData("too-many-entries")]
    [InlineData("grayscale")]
    [InlineData("grayscale-alpha")]
    [InlineData("indexed-depth")]
    public void ReadSize_WhenPaletteViolatesHeaderRules_RejectsContainer(string violation)
    {
        var colorType = violation == "grayscale-alpha" ? (byte) 4 : violation == "indexed-depth" ? (byte) 3 : (byte) 0;
        var bitDepth = violation == "indexed-depth" ? (byte) 1 : (byte) 8;
        var channels = colorType == 4 ? 2 : 1;
        var source = CreateDecodablePng(1, 1, colorType, bitDepth, [0], [new byte[channels]]);
        var palette = violation switch
        {
            "empty" => [],
            "partial-entry" => new byte[4],
            "too-many-entries" => new byte[257 * 3],
            "indexed-depth" => new byte[3 * 3],
            _ => new byte[3]
        };
        source = InsertBeforeChunk(source, "IDAT"u8, "PLTE"u8, palette);

        _ = Should.Throw<ArgumentException>(() => source.AsSpan().ReadSize());
        _ = Should.Throw<ArgumentException>(() => source.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies optional palettes remain valid for truecolor sources, including the
    /// maximum 256-entry palette.</summary>
    /// <param name="colorType">The truecolor PNG color type.</param>
    /// <param name="row">The decoded source samples.</param>
    [Theory]
    [InlineData((byte) 2, new byte[] { 1, 2, 3 })]
    [InlineData((byte) 6, new byte[] { 1, 2, 3, 4 })]
    public void ReadSize_WhenOptionalPaletteIsValid_AcceptsTruecolorSource(byte colorType, byte[] row)
    {
        var source = CreateDecodablePng(
            1,
            1,
            colorType,
            bitDepth: 8,
            [0],
            [row],
            palette: new byte[256 * 3]);

        source.AsSpan().ReadSize().ShouldBe(new Size(1, 1));
        source.AsSpan().DecodeRgba().Length.ShouldBe(4);
    }

    /// <summary>Verifies chunk type bytes must be ASCII letters and must keep the reserved third
    /// letter uppercase before unknown ancillary handling is considered.</summary>
    /// <param name="invalidType">The invalid four-byte chunk type.</param>
    [Theory]
    [InlineData("a1CD")]
    [InlineData("aBC0")]
    [InlineData("aBcD")]
    public void ReadSize_WhenChunkTypeCodeIsInvalid_RejectsContainer(string invalidType)
    {
        var source = CreateDecodablePng(1, 1, colorType: 0, bitDepth: 8, [0], [[42]]);
        var malformed = InsertBeforeIdat(source, Encoding.ASCII.GetBytes(invalidType), [1]);

        _ = Should.Throw<ArgumentException>(() => malformed.AsSpan().ReadSize());
        _ = Should.Throw<ArgumentException>(() => malformed.AsSpan().DecodeRgba());
    }

    /// <summary>Verifies encoding then decoding arbitrary RGBA pixels, including varied alpha and
    /// multiple rows, reproduces the exact original bytes; this repository has no external
    /// reference PNG decoder to check against, so round-tripping through the existing, unmodified
    /// <c>DecodeRgba</c> is the available proof the encoder emits a structurally valid container
    /// with correctly filtered and compressed scanlines.</summary>
    [Fact]
    public void Encode_WhenPixelsAreArbitrary_RoundTripsThroughDecodeRgbaExactly()
    {
        byte[] pixels =
        [
            10, 20, 30, 255, 40, 50, 60, 128, 70, 80, 90, 0,
            255, 255, 255, 255, 0, 0, 0, 1, 200, 150, 100, 64
        ];
        var size = new Size(3, 2);
        var buffer = new ArrayBufferWriter<byte>();

        Png.Encode(size, pixels, buffer);
        var decoded = buffer.WrittenSpan.DecodeRgba();

        decoded.ShouldBe(pixels);
    }

    /// <summary>Verifies a single-pixel round trip, the smallest possible encode.</summary>
    [Fact]
    public void Encode_WhenImageIsOnePixel_RoundTripsThroughDecodeRgbaExactly()
    {
        byte[] pixels = [5, 6, 7, 8];
        var buffer = new ArrayBufferWriter<byte>();

        Png.Encode(new Size(1, 1), pixels, buffer);
        var decoded = buffer.WrittenSpan.DecodeRgba();

        decoded.ShouldBe(pixels);
    }

    /// <summary>Verifies encoding the same pixels twice produces byte-identical PNG containers,
    /// proving the fixed compression level (rather than one derived from input) makes the encoder
    /// deterministic.</summary>
    [Fact]
    public void Encode_WhenCalledTwiceWithIdenticalPixels_ProducesByteIdenticalOutput()
    {
        byte[] pixels =
        [
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16
        ];
        var size = new Size(2, 2);
        var first = new ArrayBufferWriter<byte>();
        var second = new ArrayBufferWriter<byte>();

        Png.Encode(size, pixels, first);
        Png.Encode(size, pixels, second);

        first.WrittenSpan.ToArray().ShouldBe(second.WrittenSpan.ToArray());
    }

    /// <summary>Verifies the emitted IEND chunk carries the fixed, well-known CRC-32 for an empty
    /// "IEND" chunk type.</summary>
    [Fact]
    public void Encode_Always_EmitsTheWellKnownIendCrc()
    {
        var buffer = new ArrayBufferWriter<byte>();

        Png.Encode(new Size(1, 1), [1, 2, 3, 4], buffer);

        buffer.WrittenSpan[^12..].ToArray().ShouldBe(
        [
            0, 0, 0, 0,
            (byte) 'I', (byte) 'E', (byte) 'N', (byte) 'D',
            0xAE, 0x42, 0x60, 0x82
        ]);
    }

    /// <summary>Verifies non-positive dimensions are reported rather than producing a malformed container.</summary>
    [Fact]
    public void Encode_WhenSizeIsNotPositive_ThrowsArgumentOutOfRange()
    {
        var buffer = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Png.Encode(new Size(0, 1), [], buffer));
    }

    /// <summary>Verifies a pixel buffer whose length disagrees with the declared dimensions is
    /// reported rather than silently truncated or overrun.</summary>
    [Fact]
    public void Encode_WhenRgbaLengthDisagreesWithSize_ThrowsArgumentException()
    {
        var buffer = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentException>(
            () => Png.Encode(new Size(1, 1), [1, 2, 3], buffer));
    }

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
        WriteInt32(length, unchecked((int) ComputeCrc32(type, data)));
        buffer.Write(length);
    }

    private static void CorruptChunk(byte[] source, string chunkType, int? dataOffset)
    {
        var offset = 8;

        while (offset <= source.Length - 12)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));

            if (Encoding.ASCII.GetString(source, offset + 4, 4) == chunkType)
            {
                var corruptOffset = dataOffset.HasValue
                    ? offset + 8 + dataOffset.Value
                    : offset + 8 + length;
                source[corruptOffset] ^= 1;
                return;
            }

            offset += length + 12;
        }

        throw new InvalidOperationException($"The PNG fixture has no {chunkType} chunk.");
    }

    private static byte[] InsertBeforeIdat(
        byte[] source,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data) =>
        InsertBeforeChunk(source, "IDAT"u8, type, data);

    private static byte[] InsertBeforeChunk(
        byte[] source,
        ReadOnlySpan<byte> beforeType,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        var offset = 8;

        while (!source.AsSpan(offset + 4, 4).SequenceEqual(beforeType))
        {
            offset += BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4)) + 12;
        }

        using var result = new MemoryStream();
        result.Write(source.AsSpan(0, offset));
        WriteRawChunk(result, type, data);
        result.Write(source.AsSpan(offset));
        return result.ToArray();
    }

    private static byte[] ReadChunkData(byte[] source, ReadOnlySpan<byte> type)
    {
        var offset = 8;

        while (!source.AsSpan(offset + 4, 4).SequenceEqual(type))
        {
            offset += BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4)) + 12;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
        return source.AsSpan(offset + 8, length).ToArray();
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;

        foreach (var value in type)
        {
            crc = UpdateCrc32(crc, value);
        }

        foreach (var value in data)
        {
            crc = UpdateCrc32(crc, value);
        }

        return crc ^ uint.MaxValue;
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;

        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return crc;
    }

    private static void WriteInt32(Span<byte> destination, int value)
    {
        destination[0] = (byte) (value >> 24);
        destination[1] = (byte) (value >> 16);
        destination[2] = (byte) (value >> 8);
        destination[3] = (byte) value;
    }

    /// <summary>Converts 16-bit sample values to a big-endian byte row, as PNG stores multi-byte
    /// samples.</summary>
    private static byte[] Samples16(params ushort[] samples)
    {
        var row = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            row[i * 2] = (byte) (samples[i] >> 8);
            row[(i * 2) + 1] = (byte) samples[i];
        }

        return row;
    }
}
