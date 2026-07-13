// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Represents independently bounded or unbounded measure axes in cells.</summary>
public readonly record struct Constraint
{
    /// <summary>Initializes a measure constraint.</summary>
    /// <param name="width">The non-negative width, or null when unbounded.</param>
    /// <param name="height">The non-negative height, or null when unbounded.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bounded axis is negative.</exception>
    public Constraint(int? width, int? height)
    {
        if (width.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(width.Value);
        }

        if (height.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(height.Value);
        }

        Width = width;
        Height = height;
    }

    /// <summary>Gets the bounded width, or null when intrinsic width is requested.</summary>
    public int? Width { get; }

    /// <summary>Gets the bounded height, or null when intrinsic height is requested.</summary>
    public int? Height { get; }

    /// <summary>Gets whether width has a finite cell bound, including zero.</summary>
    public bool IsWidthBounded => Width.HasValue;

    /// <summary>Gets whether height has a finite cell bound, including zero.</summary>
    public bool IsHeightBounded => Height.HasValue;
}
