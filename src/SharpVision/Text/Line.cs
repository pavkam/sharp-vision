// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Describes one formatted source slice and its terminal-cell placement.</summary>
public readonly record struct Line
{
    /// <summary>Initializes validated immutable line metrics.</summary>
    /// <param name="offset">The non-negative UTF-16 source offset.</param>
    /// <param name="length">The non-negative UTF-16 source length.</param>
    /// <param name="cells">The non-negative rendered width including ellipsis.</param>
    /// <param name="leading">The non-negative leading alignment cells.</param>
    /// <param name="hasEllipsis">Whether rendering appends one narrow ellipsis.</param>
    /// <exception cref="ArgumentOutOfRangeException">A numeric value is negative.</exception>
    public Line(int offset, int length, int cells, int leading, bool hasEllipsis)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(cells);
        ArgumentOutOfRangeException.ThrowIfNegative(leading);
        Offset = offset;
        Length = length;
        Cells = cells;
        Leading = leading;
        HasEllipsis = hasEllipsis;
    }

    /// <summary>Gets the zero-based UTF-16 source offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the UTF-16 source length excluding a logical newline.</summary>
    public int Length { get; }

    /// <summary>Gets the rendered cells including an optional ellipsis.</summary>
    public int Cells { get; }

    /// <summary>Gets the leading alignment cells within the finite width.</summary>
    public int Leading { get; }

    /// <summary>Gets whether rendering appends one narrow ellipsis.</summary>
    public bool HasEllipsis { get; }
}
