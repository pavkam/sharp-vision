// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

using System.Buffers.Binary;

/// <summary>Builds structurally valid minimal PNG containers for protocol tests that do not need
/// decodable pixel data.</summary>
internal static class PngTestData
{
    /// <summary>Creates an IHDR, arbitrary IDAT, and IEND container with valid chunk CRCs.</summary>
    /// <param name="dataBytes">The number of arbitrary IDAT bytes.</param>
    /// <param name="width">The positive IHDR width.</param>
    /// <param name="height">The positive IHDR height.</param>
    /// <returns>The complete owned PNG bytes.</returns>
    internal static byte[] CreateContainer(int dataBytes = 0, int width = 1, int height = 1)
    {
        using var buffer = new MemoryStream();
        buffer.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(buffer, "IHDR"u8, header);
        WriteChunk(buffer, "IDAT"u8, new byte[dataBytes]);
        WriteChunk(buffer, "IEND"u8, []);
        return buffer.ToArray();
    }

    private static void WriteChunk(Stream destination, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
        destination.Write(value);
        destination.Write(type);
        destination.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(value, ComputeCrc32(type, data));
        destination.Write(value);
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
}
