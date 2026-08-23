// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one contiguous, uniformly styled run of UTF-16 code units within one line.</summary>
[PublicAPI]
public readonly record struct SyntaxToken
{
    /// <summary>Initializes a token.</summary>
    /// <param name="start">The non-negative UTF-16 start offset within the owning line.</param>
    /// <param name="length">The non-negative UTF-16 length.</param>
    /// <param name="style">The style role the whole run is painted with.</param>
    internal SyntaxToken(int start, int length, SyntaxDefaultStyle style)
    {
        Debug.Assert(start >= 0, "The tokenizer never records a token starting before the line begins.");
        Debug.Assert(length >= 0, "The tokenizer never records a token with negative length.");

        Start = start;
        Length = length;
        Style = style;
    }

    /// <summary>Gets the UTF-16 start offset within the owning line.</summary>
    public int Start { get; }

    /// <summary>Gets the UTF-16 length.</summary>
    public int Length { get; }

    /// <summary>Gets the style role the whole run is painted with.</summary>
    public SyntaxDefaultStyle Style { get; }
}
