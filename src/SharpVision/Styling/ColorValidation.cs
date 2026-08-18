// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates unresolved UI colors before observable state changes.</summary>
internal static class ColorValidation
{
    extension(ArgumentException)
    {
        /// <summary>Rejects transparency for a channel that paints terminal glyphs.</summary>
        /// <param name="color">The candidate color.</param>
        /// <param name="paramName">The public parameter name used by diagnostics.</param>
        /// <exception cref="ArgumentException"><paramref name="color"/> is transparent.</exception>
        public static void ThrowIfTransparent(Color? color, string paramName)
        {
            if (color is { IsTransparent: true })
            {
                throw new ArgumentException("Transparent is valid only for background composition.", paramName);
            }
        }
    }
}
