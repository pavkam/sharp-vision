using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Unicode;

namespace SharpVision.Controls;

/// <summary>Resolves fixed-cell control chrome against an inherited Unicode width policy.</summary>
internal static class CellGlyph
{
    /// <summary>Returns the requested Rune when it is one cell, otherwise a portable fallback.</summary>
    /// <param name="value">The previously validated printable Rune requested by the control.</param>
    /// <param name="fallback">A printable Rune that occupies one cell under every policy.</param>
    /// <param name="ambiguousWidth">The inherited East Asian Ambiguous width policy.</param>
    /// <returns>The Rune safe to draw into one physical control cell.</returns>
    internal static Rune Resolve(Rune value, Rune fallback, Ambiguous ambiguousWidth)
    {
        Span<char> valueBuffer = stackalloc char[2];
        var valueLength = value.EncodeToUtf16(valueBuffer);
        var measurement = Width.Measure(valueBuffer[..valueLength], ambiguousWidth);

        if (measurement.Cells == 1 && measurement.Controls == 0)
        {
            return value;
        }

        Span<char> fallbackBuffer = stackalloc char[2];
        var fallbackLength = fallback.EncodeToUtf16(fallbackBuffer);
        var fallbackMeasurement = Width.Measure(fallbackBuffer[..fallbackLength], ambiguousWidth);
        Debug.Assert(
            fallbackMeasurement.Cells == 1 && fallbackMeasurement.Controls == 0,
            "Control chrome fallbacks must remain one printable cell under every policy.");
        return fallback;
    }
}
