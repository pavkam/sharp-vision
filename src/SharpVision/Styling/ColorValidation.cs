// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates unresolved UI colors before observable state changes.</summary>
internal static class ColorValidation
{
    /// <summary>Rejects transparency for a channel that paints terminal glyphs.</summary>
    /// <param name="color">The optional terminal-default or RGB color.</param>
    /// <param name="parameterName">The public parameter name used by diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="color"/> is transparent.</exception>
    public static void ValidatePaint(Color? color, string parameterName)
    {
        if (color is { IsTransparent: true })
        {
            throw new ArgumentException("Transparent is valid only for background composition.", parameterName);
        }
    }
}
