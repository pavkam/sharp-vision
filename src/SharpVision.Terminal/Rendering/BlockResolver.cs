// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Maps shade and quadrant values to Unicode Block Elements Runes.</summary>
internal static class BlockResolver
{
    extension(Shade value)
    {
        /// <summary>Resolves one shade.</summary>
        /// <param name="ambiguousWidth">The active frame width policy.</param>
        /// <returns>The exact Unicode Rune or a portable one-cell ASCII fallback.</returns>
        public Rune Resolve(Ambiguous ambiguousWidth) => new(
            ambiguousWidth == Ambiguous.Wide
                ? value switch
                {
                    Shade.Light => '.',
                    Shade.Medium => ':',
                    Shade.Dark or Shade.Solid => '#',
                    _ => ' '
                }
                : value switch
                {
                    Shade.Light => '░',
                    Shade.Medium => '▒',
                    Shade.Dark => '▓',
                    Shade.Solid => '█',
                    _ => ' '
                });
    }

    extension(Quadrants value)
    {
        /// <summary>Resolves one non-empty quadrant mask.</summary>
        /// <param name="ambiguousWidth">The active frame width policy.</param>
        /// <returns>The exact Unicode Rune or a portable one-cell ASCII fallback.</returns>
        public Rune Resolve(Ambiguous ambiguousWidth) => new(
            ambiguousWidth == Ambiguous.Wide && value != Quadrants.None
                ? '#'
                : value switch
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
                    _ => ' '
                });
    }

    extension(Rune value)
    {
        /// <summary>Attempts to decode one supported quadrant Rune.</summary>
        /// <param name="quadrants">The decoded mask when recognized.</param>
        /// <returns>Whether the Rune represents filled quadrants.</returns>
        public bool TryDecode(out Quadrants quadrants)
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

                // The portable ASCII fallback ('#') that Resolve emits for every non-empty mask
                // under Ambiguous.Wide carries no quadrant information of its own. Decoding it
                // conservatively as every quadrant (mirroring LineResolver's '+' -> all four
                // connections) lets a subsequent merge stay a safe superset of what was actually
                // drawn, instead of silently discarding the earlier draw's bits as if the cell
                // had never been touched.
                '#' => Quadrants.All,
                _ => Quadrants.None
            };
            return quadrants != Quadrants.None;
        }
    }
}
