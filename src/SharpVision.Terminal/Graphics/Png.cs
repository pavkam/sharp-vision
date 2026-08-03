// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

/// <summary>Validates the bounded PNG container subset required for owned transmission, and
/// decodes a conservative non-interlaced 8-bit-depth subset to straight RGBA8888.</summary>
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
        /// Decodes a non-interlaced, 8-bit-depth PNG to straight (non-premultiplied) RGBA8888,
        /// four bytes per pixel in row-major order. Supports every PNG color type (grayscale, RGB,
        /// indexed, grayscale with alpha, RGBA); an indexed source without a required <c>PLTE</c>
        /// chunk is rejected. Interlaced sources and depths other than 8 bits per channel are
        /// outside this decoder's scope and are reported through
        /// <see cref="NotSupportedException"/> rather than approximated.
        /// </summary>
        /// <returns>Owned RGBA8888 pixel bytes, exactly width times height times four in length.</returns>
        /// <exception cref="ArgumentException">The PNG structure or required header fields are invalid.</exception>
        /// <exception cref="NotSupportedException">
        /// The source is interlaced, uses a bit depth other than 8, or is indexed without a
        /// <c>PLTE</c> chunk.
        /// </exception>
        public byte[] DecodeRgba()
        {
            var size = source.ReadSize();
            var (bitDepth, colorType, interlace) = ReadDecodeHeader(source);

            if (interlace != 0)
            {
                throw new NotSupportedException("Interlaced PNG sources are not supported.");
            }

            if (bitDepth != 8)
            {
                throw new NotSupportedException(
                    $"PNG bit depth {bitDepth} is not supported; only 8 bits per channel is decoded.");
            }

            var channels = ChannelsFor(colorType);
            byte[]? palette = null;
            byte[]? paletteAlpha = null;
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
                    paletteAlpha = data.ToArray();
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

            var stride = checked(size.Width * channels);
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

            var pixels = Defilter(raw, size.Height, stride, channels);
            return ToRgba(pixels, size, colorType, channels, palette, paletteAlpha);
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
        byte[]? palette,
        byte[]? paletteAlpha)
    {
        var rgba = new byte[checked(size.Width * size.Height * 4)];

        for (var pixel = 0; pixel < size.Width * size.Height; pixel++)
        {
            var source = pixels.Slice(pixel * channels, channels);
            var destination = rgba.AsSpan(pixel * 4, 4);

            switch (colorType)
            {
                case 0:
                    destination[0] = destination[1] = destination[2] = source[0];
                    destination[3] = 255;
                    break;
                case 2:
                    source.CopyTo(destination);
                    destination[3] = 255;
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
                    destination[3] = paletteAlpha is not null && index < paletteAlpha.Length
                        ? paletteAlpha[index]
                        : (byte) 255;
                    break;
                case 4:
                    destination[0] = destination[1] = destination[2] = source[0];
                    destination[3] = source[1];
                    break;
                default:
                    source.CopyTo(destination);
                    break;
            }
        }

        return rgba;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static ArgumentException Invalid() =>
        new("The source is not a structurally supported PNG.", "source");
}
