// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Identifies one half-open changed cell run within a frame row.
/// </summary>
public readonly record struct DamageSpan
{
    /// <summary>Initializes a validated changed run.</summary>
    /// <param name="row">The non-negative row.</param>
    /// <param name="start">The non-negative starting column.</param>
    /// <param name="length">The positive cell length.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is negative or length is not positive.</exception>
    public DamageSpan(int row, int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        Row = row;
        Start = start;
        Length = length;
    }

    /// <summary>Gets the zero-based row.</summary>
    public int Row { get; }

    /// <summary>Gets the zero-based starting column.</summary>
    public int Start { get; }

    /// <summary>Gets the positive cell length.</summary>
    public int Length { get; }
}
