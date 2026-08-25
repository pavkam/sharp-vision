// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Captures one validated list marker and its content.</summary>
internal readonly struct MarkdownListMarker
{
    /// <summary>Initializes a list-marker description.</summary>
    /// <param name="indent">The zero-through-three-space indentation.</param>
    /// <param name="isOrdered">Whether the marker is numeric.</param>
    /// <param name="start">The parsed numeric ordinal, or one for bullets.</param>
    /// <param name="markerWidth">The column width of the marker and its required or implied
    /// trailing space, excluding <paramref name="indent"/>: always 2 for a bullet (<c>"- "</c>),
    /// or the digit count plus 2 for an ordinal (<c>"1. "</c> is 3, <c>"10. "</c> is 4). An empty
    /// item with no physical trailing space retains the implied width needed by later content.</param>
    /// <param name="content">The source following the marker and spacing, or an empty string for
    /// an empty item.</param>
    internal MarkdownListMarker(int indent, bool isOrdered, int start, int markerWidth, string content)
    {
        Indent = indent;
        IsOrdered = isOrdered;
        Start = start;
        MarkerWidth = markerWidth;
        Content = content;
    }

    /// <summary>Gets the leading-space count.</summary>
    internal int Indent { get; }

    /// <summary>Gets whether the marker is numeric.</summary>
    internal bool IsOrdered { get; }

    /// <summary>Gets the first numeric ordinal, or one for bullets.</summary>
    internal int Start { get; }

    /// <summary>Gets the column width of the marker and its required or implied trailing space,
    /// excluding <see cref="Indent"/>. A continuation line's own indentation must be measured
    /// against <see cref="Indent"/> plus this width, not a fixed width: an ordinal marker's prefix
    /// grows with its digit count (<c>"1. "</c> is 3 columns, <c>"10. "</c> is 4).</summary>
    internal int MarkerWidth { get; }

    /// <summary>Gets the source after the marker.</summary>
    internal string Content { get; }
}
