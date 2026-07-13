// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Represents a signed origin with non-negative extents.
/// </summary>
public readonly record struct Rect
{
    /// <summary>Initializes a validated rectangle.</summary>
    /// <param name="x">The signed horizontal origin.</param>
    /// <param name="y">The signed vertical origin.</param>
    /// <param name="width">The non-negative width.</param>
    /// <param name="height">The non-negative height.</param>
    /// <exception cref="ArgumentOutOfRangeException">An extent is negative.</exception>
    public Rect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the horizontal origin.</summary>
    public int X { get; }

    /// <summary>Gets the vertical origin.</summary>
    public int Y { get; }

    /// <summary>Gets the horizontal extent.</summary>
    public int Width { get; }

    /// <summary>Gets the vertical extent.</summary>
    public int Height { get; }

    /// <summary>Gets the exclusive right edge, saturated at <see cref="int.MaxValue"/>.</summary>
    public int Right => SaturatingAdd(X, Width);

    /// <summary>Gets the exclusive bottom edge, saturated at <see cref="int.MaxValue"/>.</summary>
    public int Bottom => SaturatingAdd(Y, Height);

    /// <summary>Gets whether a point lies within the half-open rectangle.</summary>
    /// <param name="point">The point to test.</param>
    /// <returns>Whether the point is inside.</returns>
    public bool Contains(Point point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>Returns the geometric intersection with another rectangle.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>A possibly empty intersection.</returns>
    public Rect Intersect(Rect other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);

        return new Rect(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top));
    }

    private static int SaturatingAdd(int value, int extent)
    {
        long result = (long) value + extent;
        return result > int.MaxValue ? int.MaxValue : (int) result;
    }
}
