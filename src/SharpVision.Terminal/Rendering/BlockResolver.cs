using System.Text;

namespace SharpVision.Terminal.Rendering;

/// <summary>Maps shade and quadrant values to Unicode Block Elements Runes.</summary>
internal static class BlockResolver
{
    /// <summary>Resolves one shade.</summary>
    /// <param name="value">The validated shade.</param>
    /// <returns>The exact Unicode Rune.</returns>
    internal static Rune Resolve(Shade value) => new(value switch
    {
        Shade.Light => '░',
        Shade.Medium => '▒',
        Shade.Dark => '▓',
        Shade.Solid => '█',
        _ => ' ',
    });

    /// <summary>Resolves one non-empty quadrant mask.</summary>
    /// <param name="value">The validated quadrant mask.</param>
    /// <returns>The exact Unicode Rune.</returns>
    internal static Rune Resolve(Quadrants value) => new(value switch
    {
        Quadrants.None => ' ',
        Quadrants.UpperLeft => '▘',
        Quadrants.UpperRight => '▝',
        Quadrants.Upper => '▀',
        Quadrants.LowerLeft => '▖',
        Quadrants.UpperLeft | Quadrants.LowerLeft => '▌',
        Quadrants.UpperRight | Quadrants.LowerLeft => '▞',
        Quadrants.Upper | Quadrants.LowerLeft => '▛',
        Quadrants.LowerRight => '▗',
        Quadrants.UpperLeft | Quadrants.LowerRight => '▚',
        Quadrants.UpperRight | Quadrants.LowerRight => '▐',
        Quadrants.Upper | Quadrants.LowerRight => '▜',
        Quadrants.Lower => '▄',
        Quadrants.UpperLeft | Quadrants.Lower => '▙',
        Quadrants.UpperRight | Quadrants.Lower => '▟',
        Quadrants.All => '█',
        _ => ' ',
    });

    /// <summary>Attempts to decode one supported quadrant Rune.</summary>
    /// <param name="value">The candidate Rune.</param>
    /// <param name="quadrants">The decoded mask when recognized.</param>
    /// <returns>Whether the Rune represents filled quadrants.</returns>
    internal static bool TryDecode(Rune value, out Quadrants quadrants)
    {
        quadrants = value.Value switch
        {
            '▘' => Quadrants.UpperLeft,
            '▝' => Quadrants.UpperRight,
            '▀' => Quadrants.Upper,
            '▖' => Quadrants.LowerLeft,
            '▌' => Quadrants.UpperLeft | Quadrants.LowerLeft,
            '▞' => Quadrants.UpperRight | Quadrants.LowerLeft,
            '▛' => Quadrants.Upper | Quadrants.LowerLeft,
            '▗' => Quadrants.LowerRight,
            '▚' => Quadrants.UpperLeft | Quadrants.LowerRight,
            '▐' => Quadrants.UpperRight | Quadrants.LowerRight,
            '▜' => Quadrants.Upper | Quadrants.LowerRight,
            '▄' => Quadrants.Lower,
            '▙' => Quadrants.UpperLeft | Quadrants.Lower,
            '▟' => Quadrants.UpperRight | Quadrants.Lower,
            '█' => Quadrants.All,
            _ => Quadrants.None,
        };
        return quadrants != Quadrants.None;
    }
}
