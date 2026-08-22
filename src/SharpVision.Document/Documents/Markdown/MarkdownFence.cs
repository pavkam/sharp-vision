// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Captures one validated fenced-code opener.</summary>
internal readonly struct MarkdownFence
{
    /// <summary>Initializes an opener description.</summary>
    /// <param name="marker">The backtick or tilde marker.</param>
    /// <param name="length">The marker run length.</param>
    /// <param name="indent">The zero-through-three-space indentation.</param>
    /// <param name="info">The trimmed information string.</param>
    internal MarkdownFence(char marker, int length, int indent, string info)
    {
        Marker = marker;
        Length = length;
        Indent = indent;
        Info = info;
    }

    /// <summary>Gets the marker character.</summary>
    internal char Marker { get; }

    /// <summary>Gets the opening run length.</summary>
    internal int Length { get; }

    /// <summary>Gets the opening indentation.</summary>
    internal int Indent { get; }

    /// <summary>Gets the trimmed information string.</summary>
    internal string Info { get; }
}
