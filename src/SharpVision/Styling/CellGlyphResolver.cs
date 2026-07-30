// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates and resolves fixed-cell control glyphs against Unicode width policies.</summary>
internal static class CellGlyphResolver
{
    /// <summary>Returns the requested Rune when it is one cell, otherwise a portable fallback.</summary>
    /// <param name="value">The previously validated printable Rune requested by the control.</param>
    /// <param name="fallback">A printable Rune that occupies one cell under every policy.</param>
    /// <param name="ambiguousWidth">The inherited East Asian Ambiguous width policy.</param>
    /// <returns>The Rune safe to draw into one physical control cell.</returns>
    public static Rune Resolve(Rune value, Rune fallback, Ambiguous ambiguousWidth)
    {
        Span<char> valueBuffer = stackalloc char[2];
        var valueLength = value.EncodeToUtf16(valueBuffer);
        var measurement = Width.Measure(valueBuffer[..valueLength], ambiguousWidth);

        if (measurement is { Cells: 1, Controls: 0 })
        {
            return value;
        }

        // A glyph that only fails under the ambient policy passed ValidateSingleCell's
        // Narrow-only check at set-time, so the fallback below is otherwise silent (see #121).
        Debug.Assert(
            ambiguousWidth == Ambiguous.Narrow ||
                Width.Measure(valueBuffer[..valueLength], Ambiguous.Narrow) is not { Cells: 1, Controls: 0 },
            "This glyph passed ValidateSingleCell under Narrow but is falling back under the ambient policy.");

        Span<char> fallbackBuffer = stackalloc char[2];
        var fallbackLength = fallback.EncodeToUtf16(fallbackBuffer);
        var fallbackMeasurement = Width.Measure(fallbackBuffer[..fallbackLength], ambiguousWidth);
        Debug.Assert(
            fallbackMeasurement is { Cells: 1, Controls: 0 },
            "Control chrome fallbacks must remain one printable cell under every policy.");
        return fallback;
    }

    /// <summary>Validates that a Rune is printable and occupies exactly one terminal cell under
    /// the East Asian Ambiguous Narrow policy.</summary>
    /// <param name="value">The Rune to validate.</param>
    /// <param name="parameterName">The public parameter name for diagnostics.</param>
    /// <returns>The validated Rune.</returns>
    /// <exception cref="ArgumentException">The rune is not printable or not one cell wide.</exception>
    public static Rune ValidateSingleCell(Rune value, string parameterName) =>
        ValidateSingleCell(value, parameterName, Ambiguous.Narrow);

    /// <summary>Validates that a Rune is printable and occupies exactly one terminal cell under an
    /// explicit East Asian Ambiguous width policy.</summary>
    /// <param name="value">The Rune to validate.</param>
    /// <param name="parameterName">The public parameter name for diagnostics.</param>
    /// <param name="ambiguousWidth">The width policy to validate against.</param>
    /// <returns>The validated Rune.</returns>
    /// <exception cref="ArgumentException">The rune is not printable or not one cell wide.</exception>
    public static Rune ValidateSingleCell(Rune value, string parameterName, Ambiguous ambiguousWidth)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length], ambiguousWidth);
        return measurement is { Cells: 1, Controls: 0 }
            ? value
            : throw new ArgumentException("The rune must be printable and one cell wide.", parameterName);
    }
}
