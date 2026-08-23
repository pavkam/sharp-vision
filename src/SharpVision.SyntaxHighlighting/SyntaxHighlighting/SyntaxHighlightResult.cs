// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one complete document's tokenization: every line's tokens and every fold range.</summary>
[PublicAPI]
public sealed class SyntaxHighlightResult
{
    /// <summary>Initializes a highlight result.</summary>
    /// <param name="lines">The non-null tokenized lines, in source order.</param>
    /// <param name="foldRanges">The non-null fold ranges, ordered by start line then descending length.</param>
    internal SyntaxHighlightResult(IReadOnlyList<SyntaxHighlightedLine> lines, IReadOnlyList<SyntaxFoldRange> foldRanges)
    {
        Lines = lines;
        FoldRanges = foldRanges;
    }

    /// <summary>Gets the tokenized lines, in source order.</summary>
    public IReadOnlyList<SyntaxHighlightedLine> Lines { get; }

    /// <summary>
    /// Gets the fold ranges, ordered by start line and, for ranges sharing a start line, by
    /// descending length so an outer range always precedes the inner ranges it contains.
    /// </summary>
    public IReadOnlyList<SyntaxFoldRange> FoldRanges { get; }
}
