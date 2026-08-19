// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates and resolves fixed-cell control glyphs against Unicode width policies.</summary>
internal static class CellGlyphResolver
{
    extension(Rune value)
    {
        /// <summary>Returns the requested Rune when it is one cell, otherwise a portable fallback.</summary>
        /// <param name="fallback">A printable Rune that occupies one cell under every policy.</param>
        /// <param name="ambiguousWidth">The inherited East Asian Ambiguous width policy.</param>
        /// <returns>The Rune safe to draw into one physical control cell.</returns>
        [Pure]
        public Rune Resolve(Rune fallback, Ambiguous ambiguousWidth)
        {
            Span<char> valueBuffer = stackalloc char[2];
            var valueLength = value.EncodeToUtf16(valueBuffer);
            var measurement = Width.Measure(valueBuffer[..valueLength], ambiguousWidth);

            if (measurement is { Cells: 1, Controls: 0 })
            {
                return value;
            }

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
        /// <param name="parameterName">The public parameter name for diagnostics.</param>
        /// <returns>The validated Rune.</returns>
        /// <exception cref="ArgumentException">The rune is not printable or not one cell wide.</exception>
        [Pure]
        public Rune ValidateSingleCell(string parameterName) =>
            value.ValidateSingleCell(parameterName, Ambiguous.Narrow);

        /// <summary>Validates that a Rune is printable and occupies exactly one terminal cell under an
        /// explicit East Asian Ambiguous width policy.</summary>
        /// <param name="parameterName">The public parameter name for diagnostics.</param>
        /// <param name="ambiguousWidth">The width policy to validate against.</param>
        /// <returns>The validated Rune.</returns>
        /// <exception cref="ArgumentException">The rune is not printable or not one cell wide.</exception>
        [Pure]
        public Rune ValidateSingleCell(string parameterName, Ambiguous ambiguousWidth)
        {
            Span<char> buffer = stackalloc char[2];
            var length = value.EncodeToUtf16(buffer);
            var measurement = Width.Measure(buffer[..length], ambiguousWidth);
            return measurement is { Cells: 1, Controls: 0 }
                ? value
                : throw new ArgumentException("The rune must be printable and one cell wide.", parameterName);
        }
    }
}
