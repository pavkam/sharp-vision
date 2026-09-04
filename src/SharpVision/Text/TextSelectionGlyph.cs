// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Maps one grapheme in the document's semantic stream to its visible content-relative cells.</summary>
[PublicAPI]
public readonly struct TextSelectionGlyph
{
    /// <summary>Initializes one mapped semantic grapheme.</summary>
    /// <param name="range">The non-empty grapheme-aligned semantic range.</param>
    /// <param name="bounds">The positive content-relative cell rectangle.</param>
    /// <param name="source">The embedded source that owns the grapheme, or null for document text.</param>
    /// <exception cref="ArgumentException"><paramref name="range"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bounds"/> has a non-positive width or height.
    /// </exception>
    public TextSelectionGlyph(Selection range, Rect bounds, TextSelectionSource? source = null)
    {
        if (range.IsEmpty)
        {
            throw new ArgumentException(
                "A text-selection glyph must cover a non-empty semantic range.",
                nameof(range));
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Text-selection glyph bounds must have positive cell extents.");
        }

        Range = range;
        Bounds = bounds;
        Source = source;
    }

    /// <summary>Gets the grapheme-aligned UTF-16 range in the complete document stream.</summary>
    public Selection Range { get; }

    /// <summary>Gets the visible rectangle in document-content cell coordinates.</summary>
    public Rect Bounds { get; }

    /// <summary>Gets the originating embedded source, or null for document-owned text.</summary>
    public TextSelectionSource? Source { get; }
}
