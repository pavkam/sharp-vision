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
    /// <param name="background">
    /// An opaque background to blend partial alpha against. <see cref="Color.Default"/> (the
    /// default) and any other non-RGB color keep the threshold behavior: every nonzero alpha is
    /// opaque and the source component is quantized unchanged.
    /// </param>
    /// <returns>A cube index from zero through 215, or <see cref="Transparent"/>.</returns>
    [Pure]
    public static byte Quantize(byte red, byte green, byte blue, byte alpha, Color background = default)
    {
        if (alpha == 0)
        {
            return Transparent;
        }

        if (alpha != byte.MaxValue && background.IsRgb)
        {
            red = BlendComponent(red, background.Red, alpha);
            green = BlendComponent(green, background.Green, alpha);
            blue = BlendComponent(blue, background.Blue, alpha);
        }

        var quantizedRed = QuantizeComponent(red);
        var quantizedGreen = QuantizeComponent(green);
        var quantizedBlue = QuantizeComponent(blue);
        return checked((byte) ((((quantizedRed * 6) + quantizedGreen) * 6) + quantizedBlue));
    }

    private static int QuantizeComponent(byte component) => ((component * 5) + 127) / 255;

    private static byte BlendComponent(byte source, byte background, byte alpha) =>
        (byte) (((source * alpha) + (background * (255 - alpha))) / 255);
}
