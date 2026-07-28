// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Represents non-negative physical box edges in terminal cells.</summary>
[DebuggerDisplay("{ToString(),nq}")]
[PublicAPI]
public readonly record struct Thickness
{
    /// <summary>Initializes equal physical edges.</summary>
    /// <param name="uniform">The non-negative edge size.</param>
    /// <exception cref="ArgumentOutOfRangeException">The edge is negative.</exception>
    public Thickness(int uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>Initializes equal horizontal and equal vertical edges.</summary>
    /// <param name="horizontal">The non-negative left and right size.</param>
    /// <param name="vertical">The non-negative top and bottom size.</param>
    /// <exception cref="ArgumentOutOfRangeException">An edge is negative.</exception>
    /// <exception cref="OverflowException">An opposing-edge sum exceeds an integer.</exception>
    public Thickness(int horizontal, int vertical) : this(
        horizontal,
        vertical,
        horizontal,
        vertical)
    {
    }

    /// <summary>Initializes four physical edges.</summary>
    /// <param name="left">The non-negative left edge.</param>
    /// <param name="top">The non-negative top edge.</param>
    /// <param name="right">The non-negative right edge.</param>
    /// <param name="bottom">The non-negative bottom edge.</param>
    /// <exception cref="ArgumentOutOfRangeException">An edge is negative.</exception>
    /// <exception cref="OverflowException">An opposing-edge sum exceeds an integer.</exception>
    public Thickness(int left, int top, int right, int bottom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(right);
        ArgumentOutOfRangeException.ThrowIfNegative(bottom);
        _ = checked(left + right);
        _ = checked(top + bottom);
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Gets the left edge.</summary>
    public int Left { get; }

    /// <summary>Gets the top edge.</summary>
    public int Top { get; }

    /// <summary>Gets the right edge.</summary>
    public int Right { get; }

    /// <summary>Gets the bottom edge.</summary>
    public int Bottom { get; }

    /// <summary>Gets the checked sum of left and right edges.</summary>
    public int Horizontal => checked(Left + Right);

    /// <summary>Gets the checked sum of top and bottom edges.</summary>
    public int Vertical => checked(Top + Bottom);

    /// <summary>Deflates a size and saturates each result axis at zero.</summary>
    /// <param name="value">The non-negative size.</param>
    /// <returns>The deflated size.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Size Deflate(Size value) => new(
        Math.Max(0, value.Width - Horizontal),
        Math.Max(0, value.Height - Vertical));

    /// <summary>Deflates a rectangle and saturates each result axis at zero.</summary>
    /// <param name="value">The rectangle to deflate.</param>
    /// <returns>The deflated rectangle.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rect Deflate(Rect value)
    {
        var consumedLeft = Math.Min(Left, value.Width);
        var consumedTop = Math.Min(Top, value.Height);

        return new Rect(
            SaturatingAdd(value.X, consumedLeft),
            SaturatingAdd(value.Y, consumedTop),
            Math.Max(0, value.Width - Horizontal),
            Math.Max(0, value.Height - Vertical));
    }

    /// <inheritdoc />
    public override string ToString() =>
        Left == Top && Top == Right && Right == Bottom
            ? Left.ToString(CultureInfo.InvariantCulture)
            : $"({Left}, {Top}, {Right}, {Bottom})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SaturatingAdd(int value, int extent)
    {
        var result = (long) value + extent;
        return result > int.MaxValue ? int.MaxValue : (int) result;
    }
}
