// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Captures one validated list marker and its content.</summary>
internal readonly struct MarkdownListMarker
{
    /// <summary>Initializes a list-marker description.</summary>
    /// <param name="indent">The zero-through-three-space indentation.</param>
    /// <param name="isOrdered">Whether the marker is numeric.</param>
    /// <param name="delimiter">The exact bullet character or ordered punctuation that determines
    /// list identity.</param>
    /// <param name="start">The parsed numeric ordinal, or one for bullets.</param>
    /// <param name="markerWidth">The column width of the marker and its required or implied
    /// trailing spacing, excluding <paramref name="indent"/>. The width includes one through four
    /// structural spaces after the marker; an empty item with no physical trailing space retains
    /// the implied one-space width needed by later content.</param>
    /// <param name="content">The source following the marker and spacing, or an empty string for
    /// an empty item.</param>
    internal MarkdownListMarker(
        int indent,
        bool isOrdered,
        char delimiter,
        int start,
        int markerWidth,
        string content)
    {
        Indent = indent;
        IsOrdered = isOrdered;
        Delimiter = delimiter;
        Start = start;
        MarkerWidth = markerWidth;
        Content = content;
    }

    /// <summary>Gets the leading-space count.</summary>
    internal int Indent { get; }

    /// <summary>Gets whether the marker is numeric.</summary>
    internal bool IsOrdered { get; }

    /// <summary>Gets the exact bullet character or ordered punctuation that determines list
    /// identity.</summary>
    internal char Delimiter { get; }

    /// <summary>Gets the first numeric ordinal, or one for bullets.</summary>
    internal int Start { get; }

    /// <summary>Gets the column width of the marker and its required or implied trailing spacing,
    /// excluding <see cref="Indent"/>. A continuation line's own indentation must be measured
    /// against <see cref="Indent"/> plus this width, not a fixed width: the prefix grows with both
    /// an ordinal's digit count and the one through four structural spaces after the marker.</summary>
    internal int MarkerWidth { get; }

    /// <summary>Gets the source after the marker.</summary>
    internal string Content { get; }
}
