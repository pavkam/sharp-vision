// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

/// <summary>Validates the bounded PNG container subset required for owned transmission, and
/// decodes a conservative non-interlaced, 8- or 16-bit-depth subset to straight RGBA8888.</summary>
internal static class Png
{
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Read only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    extension(ReadOnlySpan<byte> source)
    {
        /// <summary>Reads positive IHDR dimensions after validating bounded chunk structure.</summary>
        /// <returns>The validated pixel dimensions.</returns>
        /// <exception cref="ArgumentException">The PNG structure or required header fields are invalid.</exception>
        [Pure]
        public Size ReadSize()
        {
            if (source.Length < 57 || !source.StartsWith(Signature))
            {
                throw Invalid();
            }

            var offset = Signature.Length;
            var first = true;
            var hasData = false;
            var dataEnded = false;
            var hasPalette = false;
            var hasTransparency = false;
            var bitDepth = (byte) 0;
            var colorType = (byte) 0;
            var paletteEntries = 0;
            Size size = default;

            while (offset <= source.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);

                if (length > int.MaxValue || length > (uint) (source.Length - offset - 12))
                {
                    throw Invalid();
                }

                var count = (int) length;
                var type = source.Slice(offset + 4, 4);
                var data = source.Slice(offset + 8, count);
                var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(source.Slice(offset + 8 + count, 4));
                offset = checked(offset + count + 12);

                if (!IsValidChunkType(type) || ComputeCrc32(type, data) != storedCrc)
                {
                    throw Invalid();
                }

                if (first)
                {
                    if (count != 13 || !type.SequenceEqual("IHDR"u8))
                    {
                        throw Invalid();
                    }

                    size = ReadHeader(data);
                    bitDepth = data[8];
                    colorType = data[9];
                    first = false;
                    continue;
                }

                if (type.SequenceEqual("IHDR"u8) || IsUnknownCriticalChunk(type))
                {
                    throw Invalid();
                }

                if (type.SequenceEqual("IDAT"u8))
                {
                    if (dataEnded || (colorType == 3 && !hasPalette))
                    {
                        throw Invalid();
                    }

                    hasData = true;
                    continue;
                }

                dataEnded = hasData;

                if (type.SequenceEqual("PLTE"u8))
                {
                    if (hasPalette || hasData)
                    {
                        throw Invalid();
                    }

                    paletteEntries = ValidatePalette(data, colorType, bitDepth);
                    hasPalette = true;
                    continue;
                }

                if (type.SequenceEqual("tRNS"u8))
                {
                    if (hasTransparency || hasData ||
                        !IsValidTransparency(data, colorType, hasPalette, paletteEntries))
                    {
                        throw Invalid();
                    }

                    hasTransparency = true;
                    continue;
                }

                if (type.SequenceEqual("IEND"u8))
                {
                    return count != 0 || !hasData || offset != source.Length
                        ? throw Invalid()
                        : size;
                }
            }

            throw Invalid();
        }

        /// <summary>
        /// Decodes a non-interlaced, 8- or 16-bit-depth PNG to straight (non-premultiplied)
        /// RGBA8888, four bytes per pixel in row-major order. Supports every PNG color type
        /// (grayscale, RGB, indexed, grayscale with alpha, RGBA); an indexed source without its
        /// required <c>PLTE</c> chunk is structurally invalid. A 16-bit-per-channel sample narrows to 8 bits
        /// by keeping its most significant byte. Interlaced sources and depths other than 8 or 16
        /// bits per channel are outside this decoder's scope and are reported through
        /// <see cref="NotSupportedException"/> rather than approximated.
        /// </summary>
        /// <returns>Owned RGBA8888 pixel bytes, exactly width times height times four in length.</returns>
        /// <exception cref="ArgumentException">The PNG structure or required header fields are invalid.</exception>
        /// <exception cref="NotSupportedException">
        /// The source is interlaced or uses a bit depth other than 8 or 16.
        /// </exception>
        [Pure]
        public byte[] DecodeRgba()
        {
            var size = source.ReadSize();
            var (bitDepth, colorType, interlace) = ReadDecodeHeader(source);

            if (interlace != 0)
            {
                throw new NotSupportedException("Interlaced PNG sources are not supported.");
            }

            if (bitDepth is not (8 or 16))
            {
                throw new NotSupportedException(
                    $"PNG bit depth {bitDepth} is not supported; only 8 or 16 bits per channel is decoded.");
            }

            var channels = ChannelsFor(colorType);
            byte[]? palette = null;
            byte[]? transparency = null;
            var idat = new ArrayBufferWriter<byte>();
            var offset = Signature.Length;

            while (offset <= source.Length - 12)
            {
                var length = (int) BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
                var type = source.Slice(offset + 4, 4);
                var data = source.Slice(offset + 8, length);
                offset = checked(offset + length + 12);

                if (type.SequenceEqual("PLTE"u8))
                {
                    palette = data.ToArray();
                }
                else if (type.SequenceEqual("tRNS"u8))
                {
                    transparency = data.ToArray();
                }
                else if (type.SequenceEqual("IDAT"u8))
                {
                    idat.Write(data);
                }
                else if (type.SequenceEqual("IEND"u8))
                {
                    break;
                }
            }

            if (colorType == 3 && palette is null)
            {
                throw new NotSupportedException("An indexed PNG source has no PLTE chunk.");
            }

            ValidateTransparency(colorType, palette, transparency);

            var bytesPerSample = bitDepth / 8;
            var stride = checked(size.Width * channels * bytesPerSample);
            var rawLength = checked((long) size.Height * (stride + 1));

            if (rawLength > int.MaxValue)
            {
                throw new NotSupportedException("The decoded PNG scanline data exceeds the supported bound.");
            }

            var raw = new byte[(int) rawLength];

            using (var compressed = new MemoryStream(idat.WrittenSpan.ToArray(), writable: false))
            using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            {
                try
                {
                    zlib.ReadExactly(raw);

                    if (zlib.ReadByte() != -1)
                    {
                        throw new ArgumentException(
                            "The PNG compressed scanline data exceeds its declared dimensions.",
                            "source");
                    }
                }
                catch (EndOfStreamException exception)
                {
                    throw new ArgumentException(
                        "The PNG compressed scanline data is shorter than its declared dimensions require.",
                        "source",
                        exception);
                }
                catch (InvalidDataException exception)
                {
                    throw new ArgumentException(
                        "The PNG compressed scanline data is invalid.",
                        "source",
                        exception);
                }

            }

            var pixels = Defilter(raw, size.Height, stride, channels * bytesPerSample);
            return ToRgba(pixels, size, colorType, channels, bytesPerSample, palette, transparency);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Size ReadHeader(ReadOnlySpan<byte> value)
    {
        var width = BinaryPrimitives.ReadUInt32BigEndian(value);
        var height = BinaryPrimitives.ReadUInt32BigEndian(value[4..]);
        var bitDepth = value[8];
        var colorType = value[9];

        return width is 0 or > int.MaxValue ||
               height is 0 or > int.MaxValue ||
               !IsValidDepth(colorType, bitDepth) ||
               value[10] != 0 ||
               value[11] != 0 ||
               value[12] > 1
            ? throw Invalid()
            : new Size((int) width, (int) height);
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static (byte BitDepth, byte ColorType, byte Interlace) ReadDecodeHeader(ReadOnlySpan<byte> source)
    {
        var data = source.Slice(Signature.Length + 8, 13);
        return (data[8], data[9], data[12]);
    }

    private static bool IsValidDepth(byte colorType, byte bitDepth) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,
        2 => bitDepth is 8 or 16,
        3 => bitDepth is 1 or 2 or 4 or 8,
        4 => bitDepth is 8 or 16,
        6 => bitDepth is 8 or 16,
        _ => false
    };

    /// <summary>Validates the PNG chunk-type alphabet and the uppercase reserved third byte.</summary>
    private static bool IsValidChunkType(ReadOnlySpan<byte> type) =>
        type.Length == 4 &&
        IsAsciiLetter(type[0]) &&
        IsAsciiLetter(type[1]) &&
        type[2] is >= (byte) 'A' and <= (byte) 'Z' &&
        IsAsciiLetter(type[3]);

    private static bool IsAsciiLetter(byte value) =>
        value is (>= (byte) 'A' and <= (byte) 'Z') or (>= (byte) 'a' and <= (byte) 'z');

    /// <summary>Validates palette shape and its relationship to the IHDR color type and depth.</summary>
    private static int ValidatePalette(ReadOnlySpan<byte> data, byte colorType, byte bitDepth)
    {
        if (colorType is not (2 or 3 or 6) || data.Length is < 3 or > 768 || data.Length % 3 != 0)
        {
            throw Invalid();
        }

        var entries = data.Length / 3;

        return colorType == 3 && entries > (1 << bitDepth) ? throw Invalid() : entries;
    }

    private static bool IsValidTransparency(
        ReadOnlySpan<byte> data,
        byte colorType,
        bool hasPalette,
        int paletteEntries) => colorType switch
        {
            0 => data.Length == 2,
            2 => data.Length == 6,
            3 => hasPalette && data.Length <= paletteEntries,
            _ => false
        };

    private static bool IsUnknownCriticalChunk(ReadOnlySpan<byte> type) =>
        (type[0] & 0x20) == 0 &&
        !type.SequenceEqual("IHDR"u8) &&
        !type.SequenceEqual("PLTE"u8) &&
        !type.SequenceEqual("IDAT"u8) &&
        !type.SequenceEqual("IEND"u8);

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static int ChannelsFor(byte colorType) => colorType switch
    {
        0 => 1,
        2 => 3,
        3 => 1,
        4 => 2,
        6 => 4,
        _ => throw Invalid()
    };

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static byte[] Defilter(ReadOnlySpan<byte> raw, int height, int stride, int bytesPerPixel)
    {
        var pixels = new byte[height * stride];
        var previousRow = new byte[stride];
        var currentRow = new byte[stride];

        for (var row = 0; row < height; row++)
        {
            var rowStart = row * (stride + 1);
            var filterType = raw[rowStart];
            var filtered = raw.Slice(rowStart + 1, stride);

            for (var x = 0; x < stride; x++)
            {
                var a = x >= bytesPerPixel ? currentRow[x - bytesPerPixel] : (byte) 0;
                var b = previousRow[x];
                var c = x >= bytesPerPixel ? previousRow[x - bytesPerPixel] : (byte) 0;

                currentRow[x] = filterType switch
                {
                    0 => filtered[x],
                    1 => unchecked((byte) (filtered[x] + a)),
                    2 => unchecked((byte) (filtered[x] + b)),
                    3 => unchecked((byte) (filtered[x] + (byte) ((a + b) / 2))),
                    4 => unchecked((byte) (filtered[x] + PaethPredictor(a, b, c))),
                    _ => throw new ArgumentException(
                        "The PNG source uses an unrecognized scanline filter type.",
                        nameof(raw))
                };
            }

            currentRow.CopyTo(pixels.AsSpan(row * stride, stride));
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return pixels;
    }

    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static byte[] ToRgba(
        ReadOnlySpan<byte> pixels,
        Size size,
        byte colorType,
        int channels,
        int bytesPerSample,
        byte[]? palette,
        byte[]? transparency)
    {
        var rgba = new byte[checked(size.Width * size.Height * 4)];
        var pixelStride = channels * bytesPerSample;

        for (var pixel = 0; pixel < size.Width * size.Height; pixel++)
        {
            var source = pixels.Slice(pixel * pixelStride, pixelStride);
            var destination = rgba.AsSpan(pixel * 4, 4);

            switch (colorType)
            {
                case 0:
                    destination[0] = destination[1] = destination[2] = NarrowSample(source, 0, bytesPerSample);
                    destination[3] = transparency is not null &&
                                     ReadSample(source, 0, bytesPerSample) == ReadTransparencySample(transparency)
                        ? (byte) 0
                        : (byte) 255;
                    break;
                case 2:
                    destination[0] = NarrowSample(source, 0, bytesPerSample);
                    destination[1] = NarrowSample(source, 1, bytesPerSample);
                    destination[2] = NarrowSample(source, 2, bytesPerSample);
                    destination[3] = transparency is not null &&
                                     ReadSample(source, 0, bytesPerSample) == ReadTransparencySample(transparency) &&
                                     ReadSample(source, 1, bytesPerSample) ==
                                     ReadTransparencySample(transparency.AsSpan(2)) &&
                                     ReadSample(source, 2, bytesPerSample) ==
                                     ReadTransparencySample(transparency.AsSpan(4))
                        ? (byte) 0
                        : (byte) 255;
                    break;
                case 3:
                    var index = source[0];
                    var paletteOffset = index * 3;

                    if (palette is null || paletteOffset + 3 > palette.Length)
                    {
                        throw new ArgumentException(
                            "The PNG source references a palette index outside PLTE.",
                            nameof(pixels));
                    }

                    destination[0] = palette[paletteOffset];
                    destination[1] = palette[paletteOffset + 1];
                    destination[2] = palette[paletteOffset + 2];
                    destination[3] = transparency is not null && index < transparency.Length
                        ? transparency[index]
                        : (byte) 255;
                    break;
                case 4:
                    destination[0] = destination[1] = destination[2] = NarrowSample(source, 0, bytesPerSample);
                    destination[3] = NarrowSample(source, 1, bytesPerSample);
                    break;
                default:
                    destination[0] = NarrowSample(source, 0, bytesPerSample);
                    destination[1] = NarrowSample(source, 1, bytesPerSample);
                    destination[2] = NarrowSample(source, 2, bytesPerSample);
                    destination[3] = NarrowSample(source, 3, bytesPerSample);
                    break;
            }
        }

        return rgba;
    }

    /// <summary>Narrows a scanline sample to 8 bits by keeping its most significant byte, which is
    /// the whole sample when the source is already 8 bits per channel.</summary>
    private static byte NarrowSample(ReadOnlySpan<byte> source, int channelIndex, int bytesPerSample) =>
        source[channelIndex * bytesPerSample];

    /// <summary>Reads a scanline sample at its native bit depth, before any narrowing, so it can be
    /// compared against a <c>tRNS</c> value at the same width.</summary>
    private static ushort ReadSample(ReadOnlySpan<byte> source, int channelIndex, int bytesPerSample)
    {
        var offset = channelIndex * bytesPerSample;
        return bytesPerSample == 2
            ? BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2))
            : source[offset];
    }

    /// <summary>Reads a <c>tRNS</c> transparency-key sample, always stored as a full two-byte
    /// value regardless of the source bit depth, for comparison against <see cref="ReadSample"/>.</summary>
    private static ushort ReadTransparencySample(ReadOnlySpan<byte> value) =>
        BinaryPrimitives.ReadUInt16BigEndian(value);

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void ValidateTransparency(byte colorType, byte[]? palette, byte[]? transparency)
    {
        if (transparency is null)
        {
            return;
        }

        var valid = colorType switch
        {
            0 => transparency.Length == 2,
            2 => transparency.Length == 6,
            3 => palette is not null && transparency.Length <= palette.Length / 3,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException(
                "The PNG tRNS chunk is invalid for its color type.",
                nameof(transparency));
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static ArgumentException Invalid() =>
        new("The source is not a structurally supported PNG.", "source");

    // ----------------------------------------------------------------------------------------
    // PNG encoding. Independent of every decode member above: builds a minimal single-IDAT PNG
    // container from already-owned RGBA8888 pixels rather than approximating or reusing decode
    // state, and is never called from the decode path.
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Encodes straight (non-premultiplied) RGBA8888 pixels to a minimal non-interlaced, 8-bit,
    /// color-type-6 PNG container: one IHDR chunk, exactly one IDAT chunk holding every scanline
    /// prefixed with the "None" filter type, and one empty IEND chunk. The zlib compression level
    /// is fixed, so encoding identical pixels always produces byte-identical output.
    /// </summary>
    /// <param name="size">The positive pixel dimensions matching <paramref name="rgba"/>.</param>
    /// <param name="rgba">Exactly four bytes per pixel in row-major RGBA order.</param>
    /// <param name="destination">The non-null destination receiving the complete PNG container.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> is not positive, or its encoded scanline data would exceed the
    /// supported bound.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="rgba"/>'s length does not equal width times height times four.
    /// </exception>
    public static void Encode(Size size, ReadOnlySpan<byte> rgba, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "PNG encode dimensions must be positive.");
        }

        var expected = checked((long) size.Width * size.Height * 4);

        if (rgba.Length != expected)
        {
            throw new ArgumentException(
                "RGBA source length must equal width times height times four.",
                nameof(rgba));
        }

        destination.Write(Signature);
        WriteIhdrChunk(destination, size);
        WriteIdatChunk(destination, size, rgba);
        WriteChunk(destination, "IEND"u8, []);
    }

    private static void WriteIhdrChunk(IBufferWriter<byte> destination, Size size)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint) size.Width);
        BinaryPrimitives.WriteUInt32BigEndian(data.Slice(4, 4), (uint) size.Height);
        data[8] = 8; // Bit depth: eight bits per channel.
        data[9] = 6; // Color type: truecolor with alpha (RGBA).
        data[10] = 0; // Compression method: zlib, the only defined value.
        data[11] = 0; // Filter method: adaptive, the only defined value.
        data[12] = 0; // Interlace method: none.
        WriteChunk(destination, "IHDR"u8, data);
    }

    private static void WriteIdatChunk(IBufferWriter<byte> destination, Size size, ReadOnlySpan<byte> rgba)
    {
        var stride = checked(size.Width * 4);
        var rawLength = checked((long) size.Height * (stride + 1));

        if (rawLength > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "The encoded PNG scanline data exceeds the supported bound.");
        }

        var raw = new byte[(int) rawLength];

        for (var row = 0; row < size.Height; row++)
        {
            var rawOffset = row * (stride + 1);
            raw[rawOffset] = 0; // Filter type: None, for every scanline.
            rgba.Slice(row * stride, stride).CopyTo(raw.AsSpan(rawOffset + 1, stride));
        }

        using var compressed = new MemoryStream();

        // A fixed compression level, rather than one derived from input size or content, is what
        // makes encoding the same pixels always produce byte-identical PNG bytes.
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WriteChunk(destination, "IDAT"u8, compressed.ToArray());
    }

    private static void WriteChunk(IBufferWriter<byte> destination, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var totalLength = checked(data.Length + 12);
        var span = destination.GetSpan(totalLength);
        BinaryPrimitives.WriteUInt32BigEndian(span, (uint) data.Length);
        type.CopyTo(span.Slice(4, 4));
        data.CopyTo(span.Slice(8, data.Length));
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(8 + data.Length, 4), ComputeCrc32(type, data));
        destination.Advance(totalLength);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = UpdateCrc32(uint.MaxValue, type);
        crc = UpdateCrc32(crc, data);
        return crc ^ uint.MaxValue;
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = _crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    // The standard PNG/zlib/gzip CRC-32 table (IEEE 802.3 polynomial 0xEDB88320), built once and
    // shared by every chunk this encoder writes.
    private static readonly uint[] _crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            var c = n;

            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
