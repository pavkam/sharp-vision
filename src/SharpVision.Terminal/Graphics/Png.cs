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
                offset = checked(offset + count + 12);

                if (first)
                {
                    if (count != 13 || !type.SequenceEqual("IHDR"u8))
                    {
                        throw Invalid();
                    }

                    size = ReadHeader(data);
                    first = false;
                    continue;
                }

                if (type.SequenceEqual("IDAT"u8))
                {
                    hasData = true;
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
        /// (grayscale, RGB, indexed, grayscale with alpha, RGBA); an indexed source without a
        /// required <c>PLTE</c> chunk is rejected. A 16-bit-per-channel sample narrows to 8 bits
        /// by keeping its most significant byte. Interlaced sources and depths other than 8 or 16
        /// bits per channel are outside this decoder's scope and are reported through
        /// <see cref="NotSupportedException"/> rather than approximated.
        /// </summary>
        /// <returns>Owned RGBA8888 pixel bytes, exactly width times height times four in length.</returns>
        /// <exception cref="ArgumentException">The PNG structure or required header fields are invalid.</exception>
        /// <exception cref="NotSupportedException">
        /// The source is interlaced, uses a bit depth other than 8 or 16, or is indexed without a
        /// <c>PLTE</c> chunk.
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
}
