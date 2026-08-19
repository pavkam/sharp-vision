// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Converts deterministic RGB and HSV values for color controls.</summary>
internal static class ColorMath
{
    extension(Color)
    {
        /// <summary>Creates one RGB color from normalized saturation and value.</summary>
        /// <param name="hue">The hue from zero through 359.</param>
        /// <param name="saturation">The saturation from zero through one.</param>
        /// <param name="value">The value from zero through one.</param>
        /// <returns>The nearest 8-bit RGB color.</returns>
        [Pure]
        public static Color FromHsv([ValueRange(0, 359)] int hue, double saturation, double value)
        {
            Debug.Assert(hue is >= 0 and < 360, "Hue is normalized before conversion.");
            Debug.Assert(saturation is >= 0 and <= 1, "Saturation is normalized before conversion.");
            Debug.Assert(value is >= 0 and <= 1, "Value is normalized before conversion.");
            var chroma = value * saturation;
            var sector = hue / 60d;
            var secondary = chroma * (1 - Math.Abs((sector % 2) - 1));
            var red = 0d;
            var green = 0d;
            var blue = 0d;

            switch ((int) sector)
            {
                case 0:
                    red = chroma;
                    green = secondary;
                    break;
                case 1:
                    red = secondary;
                    green = chroma;
                    break;
                case 2:
                    green = chroma;
                    blue = secondary;
                    break;
                case 3:
                    green = secondary;
                    blue = chroma;
                    break;
                case 4:
                    red = secondary;
                    blue = chroma;
                    break;
                default:
                    red = chroma;
                    blue = secondary;
                    break;
            }

            var match = value - chroma;
            return Color.Rgb(
                Math.Clamp((int) Math.Round((red + match) * 255, MidpointRounding.AwayFromZero), 0, 255),
                Math.Clamp((int) Math.Round((green + match) * 255, MidpointRounding.AwayFromZero), 0, 255),
                Math.Clamp((int) Math.Round((blue + match) * 255, MidpointRounding.AwayFromZero), 0, 255));
        }
    }

    extension(Color color)
    {
        /// <summary>Converts one RGB color to normalized HSV components.</summary>
        /// <param name="hue">Receives hue from zero through 359.</param>
        /// <param name="saturation">Receives saturation from zero through one.</param>
        /// <param name="value">Receives value from zero through one.</param>
        public void ToHsv([ValueRange(0, 359)] out int hue, out double saturation, out double value)
        {
            Debug.Assert(color.IsRgb, "Palette colors are resolved before HSV conversion.");
            var red = color.Red / 255d;
            var green = color.Green / 255d;
            var blue = color.Blue / 255d;
            var maximum = Math.Max(red, Math.Max(green, blue));
            var minimum = Math.Min(red, Math.Min(green, blue));
            var delta = maximum - minimum;
            var degrees = 0d;

            if (delta != 0)
            {
                degrees = maximum == red
                    ? 60 * ((green - blue) / delta % 6)
                    : maximum == green
                        ? 60 * (((blue - red) / delta) + 2)
                        : 60 * (((red - green) / delta) + 4);
            }

            if (degrees < 0)
            {
                degrees += 360;
            }

            hue = Math.Clamp((int) Math.Round(degrees, MidpointRounding.AwayFromZero), 0, 359);
            saturation = maximum == 0 ? 0 : delta / maximum;
            value = maximum;
        }

        /// <summary>Chooses black or white foreground for one RGB background.</summary>
        /// <returns>Black for light colors; otherwise white.</returns>
        [Pure]
        public Color Contrast()
        {
            Debug.Assert(color.IsRgb, "Contrast receives resolved RGB.");
            var luminance = (color.Red * 299) + (color.Green * 587) + (color.Blue * 114);
            return luminance >= 128_000 ? Color.Rgb(0, 0, 0) : Color.Rgb(255, 255, 255);
        }
    }

}
