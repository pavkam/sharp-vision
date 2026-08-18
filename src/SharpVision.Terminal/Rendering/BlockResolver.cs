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

    extension(BarDirection direction)
    {
        /// <summary>Gets how many eighths one partial cell can actually express in this
        /// direction.</summary>
        /// <remarks>Block Elements is asymmetric: the lower (U+2581..U+2587) and left
        /// (U+258F..U+2589) families cover every eighth, while the upper and right families define
        /// only the half blocks (U+2580, U+2590) - so a bar growing down or left snaps its partial
        /// cell to half-cell precision rather than pretending to a resolution the repertoire cannot
        /// draw.</remarks>
        public int CapResolution => direction is BarDirection.Up or BarDirection.Right ? 1 : 4;

        /// <summary>Resolves the partial end-cap cell of a fractional bar.</summary>
        /// <param name="eighths">The filled portion of the cap cell, from 0 to 8 inclusive.</param>
        /// <param name="ambiguousWidth">The active frame width policy.</param>
        /// <returns>The exact Unicode Rune, or a portable ASCII fallback under a wide policy; a
        /// space when the rounded portion is empty.</returns>
        public Rune ResolveCap(int eighths, Ambiguous ambiguousWidth)
        {
            if (ambiguousWidth == Ambiguous.Wide)
            {
                // The eighth and half blocks are East Asian Ambiguous, so under a wide policy the
                // cap degrades to the same all-or-nothing '#' the solid fill uses, rounding the
                // partial cell to whichever state is nearer.
                return new Rune(eighths >= 4 ? '#' : ' ');
            }

            var resolution = direction.CapResolution;
            var snapped = (int) Math.Round(eighths / (double) resolution, MidpointRounding.AwayFromZero) * resolution;

            return new Rune(snapped switch
            {
                <= 0 => ' ',
                >= 8 => '█',
                _ => direction switch
                {
                    BarDirection.Up => (char) ('▀' + snapped),
                    BarDirection.Right => (char) ('▐' - snapped),
                    BarDirection.Down => '▀',
                    BarDirection.Left or _ => '▐'
                }
            });
        }
    }
}
