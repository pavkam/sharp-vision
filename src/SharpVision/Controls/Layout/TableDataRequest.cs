// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Requests one contiguous range of progressively loaded items.</summary>
[PublicAPI]
public readonly record struct TableDataRequest
{
    /// <summary>Initializes a validated contiguous load request.</summary>
    /// <param name="startIndex">The non-negative zero-based starting logical index.</param>
    /// <param name="count">The positive number of items requested.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> is negative, or <paramref name="count"/> is not positive.
    /// </exception>
    public TableDataRequest(int startIndex, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        StartIndex = startIndex;
        Count = count;
    }

    /// <summary>Gets the non-negative zero-based starting logical index.</summary>
    public int StartIndex { get; }

    /// <summary>Gets the positive number of items requested.</summary>
    public int Count { get; }
}
