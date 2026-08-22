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
    /// <param name="content">The source following the marker and required spacing.</param>
    internal MarkdownListMarker(int indent, bool isOrdered, int start, string content)
    {
        Indent = indent;
        IsOrdered = isOrdered;
        Start = start;
        Content = content;
    }

    /// <summary>Gets the leading-space count.</summary>
    internal int Indent { get; }

    /// <summary>Gets whether the marker is numeric.</summary>
    internal bool IsOrdered { get; }

    /// <summary>Gets the first numeric ordinal, or one for bullets.</summary>
    internal int Start { get; }

    /// <summary>Gets the source after the marker.</summary>
    internal string Content { get; }
}
