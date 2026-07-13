// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

using System.Buffers.Binary;

using SharpVision.Terminal.Geometry;

/// <summary>Validates the bounded PNG container subset required for owned transmission.</summary>
internal static class Png
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Reads positive IHDR dimensions after validating bounded chunk structure.</summary>
    /// <param name="source">The complete borrowed encoded PNG.</param>
    /// <returns>The validated pixel dimensions.</returns>
    /// <exception cref="ArgumentException">The PNG structure or required header fields are invalid.</exception>
    internal static Size ReadSize(ReadOnlySpan<byte> source)
    {
        if (source.Length < 57 || !source.StartsWith(Signature))
        {
            throw Invalid();
        }

        int offset = Signature.Length;
        bool first = true;
        bool hasData = false;
        Size size = default;

        while (offset <= source.Length - 12)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);

            if (length > int.MaxValue || length > (uint) (source.Length - offset - 12))
            {
                throw Invalid();
            }

            int count = (int) length;
            ReadOnlySpan<byte> type = source.Slice(offset + 4, 4);
            ReadOnlySpan<byte> data = source.Slice(offset + 8, count);
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

    private static Size ReadHeader(ReadOnlySpan<byte> value)
    {
        uint width = BinaryPrimitives.ReadUInt32BigEndian(value);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(value[4..]);
        byte bitDepth = value[8];
        byte colorType = value[9];

        return width is 0 or > int.MaxValue ||
            height is 0 or > int.MaxValue ||
            !IsValidDepth(colorType, bitDepth) ||
            value[10] != 0 ||
            value[11] != 0 ||
            value[12] > 1
            ? throw Invalid()
            : new Size((int) width, (int) height);
    }

    private static bool IsValidDepth(byte colorType, byte bitDepth) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,
        2 => bitDepth is 8 or 16,
        3 => bitDepth is 1 or 2 or 4 or 8,
        4 => bitDepth is 8 or 16,
        6 => bitDepth is 8 or 16,
        _ => false,
    };

    private static ArgumentException Invalid() =>
        new("The source is not a structurally supported PNG.", "source");
}
