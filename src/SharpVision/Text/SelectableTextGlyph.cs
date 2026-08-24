// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Maps one intended semantic grapheme range to its visible source-local cell bounds.</summary>
[PublicAPI]
public sealed class SelectableTextGlyph
{
    /// <summary>Initializes one visible grapheme projection.</summary>
    /// <param name="range">The non-empty, grapheme-aligned semantic UTF-16 range.</param>
    /// <param name="bounds">The positive source-local cell rectangle occupied by the grapheme.</param>
    /// <exception cref="ArgumentException"><paramref name="range"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bounds"/> has a non-positive width or height.
    /// </exception>
    public SelectableTextGlyph(Selection range, Rect bounds)
    {
        if (range.IsEmpty)
        {
            throw new ArgumentException(
                "A selectable-text glyph must cover a non-empty semantic range.",
                nameof(range));
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Selectable-text glyph bounds must have positive cell extents.");
        }

        Range = range;
        Bounds = bounds;
    }

    /// <summary>
    /// Gets the non-empty semantic UTF-16 range that a snapshot validates as exactly one grapheme.
    /// </summary>
    public Selection Range { get; }

    /// <summary>Gets the positive source-local cell rectangle occupied by the grapheme.</summary>
    public Rect Bounds { get; }
}
