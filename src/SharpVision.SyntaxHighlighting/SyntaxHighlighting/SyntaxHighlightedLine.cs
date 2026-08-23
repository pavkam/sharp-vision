// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one tokenized source line.</summary>
[PublicAPI]
public readonly record struct SyntaxHighlightedLine
{
    /// <summary>Initializes a tokenized line.</summary>
    /// <param name="tokens">
    /// The non-null, ordered, non-overlapping tokens covering the line; adjacent tokens with the
    /// same style are already merged into one.
    /// </param>
    internal SyntaxHighlightedLine(IReadOnlyList<SyntaxToken> tokens) => Tokens = tokens;

    /// <summary>Gets the ordered, non-overlapping tokens covering the line.</summary>
    public IReadOnlyList<SyntaxToken> Tokens { get; }
}
