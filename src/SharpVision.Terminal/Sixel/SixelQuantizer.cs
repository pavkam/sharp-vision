// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Sixel;

/// <summary>Maps sRGB RGBA pixels deterministically into the DEC 216-color cube.</summary>
internal static class SixelQuantizer
{
    /// <summary>Identifies a fully transparent raster sample.</summary>
    public const byte Transparent = byte.MaxValue;

    /// <summary>Quantizes one RGBA sample without dithering or allocation.</summary>
    /// <param name="red">The sRGB red component.</param>
    /// <param name="green">The sRGB green component.</param>
    /// <param name="blue">The sRGB blue component.</param>
    /// <param name="alpha">Zero for transparency; every other value is opaque.</param>
    /// <returns>A cube index from zero through 215, or <see cref="Transparent"/>.</returns>
    [Pure]
    public static byte Quantize(byte red, byte green, byte blue, byte alpha)
    {
        if (alpha == 0)
        {
            return Transparent;
        }

        var quantizedRed = QuantizeComponent(red);
        var quantizedGreen = QuantizeComponent(green);
        var quantizedBlue = QuantizeComponent(blue);
        return checked((byte) ((((quantizedRed * 6) + quantizedGreen) * 6) + quantizedBlue));
    }

    private static int QuantizeComponent(byte component) => ((component * 5) + 127) / 255;
}
