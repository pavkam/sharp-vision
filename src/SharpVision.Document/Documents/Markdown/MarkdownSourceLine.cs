// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Retains one parser line with its zero-based UTF-16 offset in the original source.</summary>
internal readonly record struct MarkdownSourceLine
{
    /// <summary>Initializes a source-aware parser line.</summary>
    /// <param name="text">The non-null line content without its newline terminator.</param>
    /// <param name="offset">The non-negative UTF-16 offset of the content in the original source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    internal MarkdownSourceLine(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Text = text;
        Offset = offset;
    }

    /// <summary>Gets the line content without its newline terminator.</summary>
    internal string Text { get; }

    /// <summary>Gets the line content's zero-based UTF-16 offset in the original source.</summary>
    internal int Offset { get; }

    /// <summary>Creates a suffix whose offset advances by the removed UTF-16 code units.</summary>
    /// <param name="start">The suffix start within <see cref="Text"/>.</param>
    /// <returns>A source-aware suffix.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> lies outside the line.</exception>
    internal MarkdownSourceLine Slice(int start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Text.Length);

        return new MarkdownSourceLine(Text[start..], checked(Offset + start));
    }
}
