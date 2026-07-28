// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Defines immutable finite image ownership and geometry limits.</summary>
[PublicAPI]
public sealed class Limits
{
    /// <summary>Initializes validated image limits.</summary>
    /// <param name="maxDimension">The maximum positive pixels on either axis.</param>
    /// <param name="maxPixels">The maximum positive pixel count.</param>
    /// <param name="maxSourceBytes">The maximum positive owned source bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="maxPixels"/> exceeds the area addressable by
    /// <paramref name="maxDimension"/>.
    /// </exception>
    public Limits(
        int maxDimension = 16_384,
        int maxPixels = 67_108_864,
        int maxSourceBytes = 256 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDimension);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSourceBytes);

        if (maxPixels > (long) maxDimension * maxDimension)
        {
            throw new ArgumentException(
                "The pixel limit cannot exceed the area addressable by the dimension limit.",
                nameof(maxPixels));
        }

        MaxDimension = maxDimension;
        MaxPixels = maxPixels;
        MaxSourceBytes = maxSourceBytes;
    }

    /// <summary>Gets the default finite image policy.</summary>
    public static Limits Default { get; } = new();

    /// <summary>Gets the maximum pixels on either axis.</summary>
    public int MaxDimension { get; }

    /// <summary>Gets the maximum total pixels.</summary>
    public int MaxPixels { get; }

    /// <summary>Gets the maximum owned source byte count.</summary>
    public int MaxSourceBytes { get; }

    /// <summary>Validates image dimensions and source size against this policy.</summary>
    /// <param name="size">The proposed positive pixel dimensions.</param>
    /// <param name="sourceBytes">The proposed non-negative source byte count.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension, area, or byte count exceeds policy.</exception>
    internal void Validate(Size size, int sourceBytes)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Image dimensions must be positive.");
        }

        if (size.Width > MaxDimension || size.Height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "An image dimension exceeds policy.");
        }

        if ((long) size.Width * size.Height > MaxPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The image pixel count exceeds policy.");
        }

        if (sourceBytes < 0 || sourceBytes > MaxSourceBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceBytes),
                sourceBytes,
                "The image source byte count exceeds policy.");
        }
    }
}
