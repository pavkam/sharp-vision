// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Represents a signed origin with non-negative extents.
/// </summary>
[DebuggerDisplay("({X}, {Y}, {Width}×{Height})")]
[PublicAPI]
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
    /// <remarks>
    /// Compares against the true (64-bit) right and bottom edges rather than the saturated
    /// <see cref="Right"/>/<see cref="Bottom"/> properties. A rectangle whose true edge exceeds
    /// <see cref="int.MaxValue"/> can still validly contain the representable coordinate
    /// <see cref="int.MaxValue"/> itself; testing against the saturated exclusive bound would
    /// wrongly exclude it, since that bound is itself <see cref="int.MaxValue"/>.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Point point) =>
        point.X >= X && point.X < (long) X + Width && point.Y >= Y && point.Y < (long) Y + Height;

    /// <summary>Returns the geometric intersection with another rectangle.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>A possibly empty intersection.</returns>
    /// <remarks>
    /// Combines true (64-bit) edges before clamping to the representable int range exactly once.
    /// Clamping each input's edge to <see cref="Right"/>/<see cref="Bottom"/> first and then
    /// subtracting again would compound saturation loss: two rectangles that individually
    /// saturate can still have an exact, fully representable intersection width, and computing
    /// through the already-saturated edges would under-report it.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rect Intersect(Rect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min((long) X + Width, (long) other.X + other.Width);
        var bottom = Math.Min((long) Y + Height, (long) other.Y + other.Height);

        return new Rect(
            left,
            top,
            ClampedExtent(right, left),
            ClampedExtent(bottom, top));
    }

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y}, {Width}×{Height})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SaturatingAdd(int value, int extent)
    {
        var result = (long) value + extent;
        return result > int.MaxValue ? int.MaxValue : (int) result;
    }

    // Accepts the true (already 64-bit) high edge so two edges near opposite ends of the int
    // range, or an edge beyond int.MaxValue, can't wrap around or lose precision before this
    // subtraction; clamps the (possibly huge) true difference into the representable
    // non-negative int range on both ends.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ClampedExtent(long high, int low)
    {
        var extent = high - low;
        return extent <= 0 ? 0 : extent > int.MaxValue ? int.MaxValue : (int) extent;
    }
}
