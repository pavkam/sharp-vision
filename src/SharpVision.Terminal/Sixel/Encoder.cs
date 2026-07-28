// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Sixel;

using Graphics;

/// <summary>Samples one indexed raster and emits bounded DEC sixel band planes.</summary>
internal static class Encoder
{
    #region Transaction encoding

    /// <summary>Samples and encodes one already validated RGBA proposal.</summary>
    /// <param name="image">The RGBA image.</param>
    /// <param name="source">The contained source rectangle.</param>
    /// <param name="destination">The output raster size.</param>
    /// <param name="mode">The fitting mode.</param>
    /// <param name="output">The bounded transaction writer.</param>
    /// <param name="maxOutputBytes">The positive caller byte policy.</param>
    /// <returns>The exact number of source sampling operations.</returns>
    /// <exception cref="InvalidOperationException">The checked worst-case output exceeds policy.</exception>
    public static int Encode(
        Image image,
        Rect source,
        Size destination,
        PlacementMode mode,
        IBufferWriter<byte> output,
        int maxOutputBytes)
    {
        var pixelCount = checked(destination.Width * destination.Height);
        var rentedRaster = ArrayPool<byte>.Shared.Rent(pixelCount);

        try
        {
            var raster = rentedRaster.AsSpan(0, pixelCount);
            var samples = Sample(image, source, destination, mode, raster);
            var palette = new Palette(raster);
            var maximum = CalculateMaximumBytes(destination, palette.Count);

            if (maximum > maxOutputBytes)
            {
                throw new InvalidOperationException("The sixel worst-case output exceeds policy.");
            }

            WriteRaster(raster, destination, palette, output);
            return samples;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedRaster, clearArray: true);
        }
    }

    /// <summary>Calculates a checked conservative transaction bound before plane emission.</summary>
    /// <param name="destination">The positive raster size.</param>
    /// <param name="colorCount">The palette color count from zero through 216.</param>
    /// <returns>The checked finite maximum encoded byte count.</returns>
    public static long CalculateMaximumBytes(Size destination, int colorCount)
    {
        Debug.Assert(destination.Width > 0 && destination.Height > 0, "Validated rasters are positive.");
        Debug.Assert(colorCount is >= 0 and <= 216, "The fixed cube bounds the palette.");
        var bands = ((long) destination.Height + 5) / 6;

        // Header and numeric fields fit 64 bytes. Every palette definition fits 20 bytes.
        // Each band can select every color (4 bytes), emit one byte per column, rewind (1),
        // and advance (1). Repeat introducers only replace runs of at least four bytes.
        return checked(
            64L +
            (colorCount * 20L) +
            (bands * ((colorCount * (destination.Width + 5L)) + 1L)) +
            2L);
    }

    #endregion

    #region Sampling

    private static int Sample(
        Image image,
        Rect source,
        Size destination,
        PlacementMode mode,
        Span<byte> raster)
    {
        var samples = 0;

        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                var index = checked((y * destination.Width) + x);

                if (!TryMapPixel(source, destination, mode, x, y, out var sourceX, out var sourceY))
                {
                    raster[index] = Quantizer.Transparent;
                    samples++;
                    continue;
                }

                var offset = checked(((sourceY * image.Size.Width) + sourceX) * 4);
                var pixels = image.Source;
                raster[index] = Quantizer.Quantize(
                    pixels[offset],
                    pixels[offset + 1],
                    pixels[offset + 2],
                    pixels[offset + 3]);
                samples++;
            }
        }

        return samples;
    }

    private static bool TryMapPixel(
        Rect source,
        Size destination,
        PlacementMode mode,
        int x,
        int y,
        out int sourceX,
        out int sourceY)
    {
        var targetX = 0;
        var targetY = 0;
        var targetWidth = destination.Width;
        var targetHeight = destination.Height;
        var cropX = 0;
        var cropY = 0;
        var cropWidth = source.Width;
        var cropHeight = source.Height;

        if (mode == PlacementMode.Contain)
        {
            if ((long) destination.Width * source.Height <= (long) destination.Height * source.Width)
            {
                targetHeight = Math.Max(1, checked((int) ((long) source.Height * destination.Width / source.Width)));
                targetY = (destination.Height - targetHeight) / 2;
            }
            else
            {
                targetWidth = Math.Max(1, checked((int) ((long) source.Width * destination.Height / source.Height)));
                targetX = (destination.Width - targetWidth) / 2;
            }

            if (x < targetX || x >= targetX + targetWidth || y < targetY || y >= targetY + targetHeight)
            {
                sourceX = 0;
                sourceY = 0;
                return false;
            }
        }
        else if (mode == PlacementMode.Cover)
        {
            if ((long) destination.Width * source.Height >= (long) destination.Height * source.Width)
            {
                cropHeight = Math.Max(1, checked((int) ((long) source.Width * destination.Height / destination.Width)));
                cropY = (source.Height - cropHeight) / 2;
            }
            else
            {
                cropWidth = Math.Max(1, checked((int) ((long) source.Height * destination.Width / destination.Height)));
                cropX = (source.Width - cropWidth) / 2;
            }
        }

        sourceX = source.X + cropX + checked((int) ((long) (x - targetX) * cropWidth / targetWidth));
        sourceY = source.Y + cropY + checked((int) ((long) (y - targetY) * cropHeight / targetHeight));
        return true;
    }

    #endregion

    #region Band emission

    private static void WriteRaster(
        ReadOnlySpan<byte> raster,
        Size destination,
        Palette palette,
        IBufferWriter<byte> output)
    {
        WriteBytes(output, "\u001bP0;1;0q\"1;1;"u8);
        WriteInteger(output, destination.Width);
        WriteByte(output, (byte) ';');
        WriteInteger(output, destination.Height);

        for (var identifier = 0; identifier < palette.Count; identifier++)
        {
            WritePalette(output, identifier, palette.GetColor(identifier));
        }

        var planeLength = checked(Math.Max(1, palette.Count) * destination.Width);
        var rentedPlanes = ArrayPool<byte>.Shared.Rent(planeLength);
        var rentedUsed = ArrayPool<bool>.Shared.Rent(Math.Max(1, palette.Count));

        try
        {
            var planes = rentedPlanes.AsSpan(0, planeLength);
            var bandUsed = rentedUsed.AsSpan(0, palette.Count);
            var bands = checked((destination.Height + 5) / 6);

            for (var band = 0; band < bands; band++)
            {
                planes.Clear();
                bandUsed.Clear();
                PopulateBand(raster, destination, palette, band, planes, bandUsed);

                if (band != 0)
                {
                    WriteByte(output, (byte) '-');
                }

                var wrotePlane = false;

                for (var identifier = 0; identifier < palette.Count; identifier++)
                {
                    if (!bandUsed[identifier])
                    {
                        continue;
                    }

                    if (wrotePlane)
                    {
                        WriteByte(output, (byte) '$');
                    }

                    WriteByte(output, (byte) '#');
                    WriteInteger(output, identifier);
                    WritePlane(
                        planes.Slice(identifier * destination.Width, destination.Width),
                        output);
                    wrotePlane = true;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedPlanes, clearArray: true);
            ArrayPool<bool>.Shared.Return(rentedUsed, clearArray: true);
        }

        WriteBytes(output, "\u001b\\"u8);
    }

    private static void PopulateBand(
        ReadOnlySpan<byte> raster,
        Size destination,
        Palette palette,
        int band,
        Span<byte> planes,
        Span<bool> bandUsed)
    {
        var origin = checked(band * 6);
        var rows = Math.Min(6, destination.Height - origin);

        for (var bit = 0; bit < rows; bit++)
        {
            var row = raster.Slice(checked((origin + bit) * destination.Width), destination.Width);

            for (var x = 0; x < row.Length; x++)
            {
                var color = row[x];

                if (color == Quantizer.Transparent)
                {
                    continue;
                }

                var identifier = palette.GetIdentifier(color);
                planes[(identifier * destination.Width) + x] |= checked((byte) (1 << bit));
                bandUsed[identifier] = true;
            }
        }
    }

    private static void WritePlane(ReadOnlySpan<byte> plane, IBufferWriter<byte> output)
    {
        var last = plane.Length - 1;

        while (last >= 0 && plane[last] == 0)
        {
            last--;
        }

        var x = 0;

        while (x <= last)
        {
            var value = plane[x];
            var run = 1;

            while (x + run <= last && plane[x + run] == value)
            {
                run++;
            }

            if (run >= 4)
            {
                WriteByte(output, (byte) '!');
                WriteInteger(output, run);
            }

            var repetitions = run >= 4 ? 1 : run;

            for (var index = 0; index < repetitions; index++)
            {
                WriteByte(output, checked((byte) (0x3f + value)));
            }

            x += run;
        }
    }

    #endregion

    #region Byte writing

    private static void WritePalette(IBufferWriter<byte> output, int identifier, int color)
    {
        var red = color / 36;
        var green = color / 6 % 6;
        var blue = color % 6;
        WriteByte(output, (byte) '#');
        WriteInteger(output, identifier);
        WriteBytes(output, ";2;"u8);
        WriteInteger(output, red * 20);
        WriteByte(output, (byte) ';');
        WriteInteger(output, green * 20);
        WriteByte(output, (byte) ';');
        WriteInteger(output, blue * 20);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value)
    {
        var destination = output.GetSpan(1);
        destination[0] = value;
        output.Advance(1);
    }

    private static void WriteBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        var destination = output.GetSpan(value.Length);
        value.CopyTo(destination);
        output.Advance(value.Length);
    }

    private static void WriteInteger(IBufferWriter<byte> output, int value)
    {
        Span<byte> buffer = stackalloc byte[16];

        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            throw new InvalidOperationException("A validated sixel integer failed formatting.");
        }

        WriteBytes(output, buffer[..written]);
    }

    #endregion
}
