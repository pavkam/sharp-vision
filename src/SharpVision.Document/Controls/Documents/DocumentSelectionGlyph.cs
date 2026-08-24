// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Maps one grapheme in the document's semantic stream to its visible content-relative cells.</summary>
internal readonly struct DocumentSelectionGlyph
{
    /// <summary>Initializes one mapped semantic grapheme.</summary>
    /// <param name="range">The non-empty grapheme-aligned semantic range.</param>
    /// <param name="bounds">The positive content-relative cell rectangle.</param>
    /// <param name="source">The embedded source that owns the grapheme, or null for document text.</param>
    internal DocumentSelectionGlyph(Selection range, Rect bounds, DocumentSelectionSource? source = null)
    {
        Debug.Assert(!range.IsEmpty, "A document selection glyph covers one semantic grapheme.");
        Debug.Assert(bounds.Width > 0 && bounds.Height > 0, "A document selection glyph occupies positive cells.");

        Range = range;
        Bounds = bounds;
        Source = source;
    }

    /// <summary>Gets the grapheme-aligned UTF-16 range in the complete document stream.</summary>
    internal Selection Range { get; }

    /// <summary>Gets the visible rectangle in document-content cell coordinates.</summary>
    internal Rect Bounds { get; }

    /// <summary>Gets the originating embedded source, or null for document-owned text.</summary>
    internal DocumentSelectionSource? Source { get; }
}
