// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one tokenized source line; the default value is an empty line.</summary>
[PublicAPI]
public readonly record struct SyntaxHighlightedLine
{
#pragma warning disable IDE0032 // Default structs need null-coalescing getters over nullable backing storage.
    private readonly IReadOnlyList<SyntaxToken>? _tokens;
#pragma warning restore IDE0032

    /// <summary>Initializes a tokenized line.</summary>
    /// <param name="tokens">
    /// The non-null, ordered, non-overlapping tokens covering the line; adjacent tokens with the
    /// same style are already merged into one.
    /// </param>
    internal SyntaxHighlightedLine(IReadOnlyList<SyntaxToken> tokens) =>
        _tokens = new SyntaxReadOnlyList<SyntaxToken>(tokens);

    /// <summary>Gets the ordered, non-overlapping tokens covering the line.</summary>
    public IReadOnlyList<SyntaxToken> Tokens => _tokens ?? SyntaxReadOnlyList<SyntaxToken>.Empty;
}
