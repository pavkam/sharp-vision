// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

/// <summary>Identifies one zero-based UTF-16 range in serialized source.</summary>
[PublicAPI]
public readonly record struct DocumentSourceSpan
{
    /// <summary>Initializes a source range.</summary>
    /// <param name="offset">The non-negative UTF-16 offset.</param>
    /// <param name="length">The non-negative UTF-16 length.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative.</exception>
    public DocumentSourceSpan(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Offset = offset;
        Length = length;
    }

    /// <summary>Gets the zero-based UTF-16 offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the UTF-16 length.</summary>
    public int Length { get; }
}
