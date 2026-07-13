// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;

/// <summary>Describes one contained integer scrollbar thumb.</summary>
public readonly record struct Thumb
{
    /// <summary>Initializes non-negative thumb geometry.</summary>
    /// <param name="start">The non-negative track offset.</param>
    /// <param name="length">The non-negative thumb length.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative.</exception>
    public Thumb(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Start = start;
        Length = length;
    }

    /// <summary>Gets the thumb offset from the track start.</summary>
    public int Start { get; }

    /// <summary>Gets the thumb length.</summary>
    public int Length { get; }

    /// <summary>Resolves stable contained thumb geometry for one track.</summary>
    /// <param name="range">The validated scroll range.</param>
    /// <param name="trackLength">The non-negative available track cells.</param>
    /// <returns>The contained thumb.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trackLength"/> is negative.</exception>
    public static Thumb Resolve(Range range, int trackLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackLength);

        if (trackLength == 0)
        {
            return default;
        }

        if (range.Span == 0)
        {
            return new Thumb(0, trackLength);
        }

        var total = (long) range.Span + range.Viewport;
        var proportional = total == 0
            ? 0
            : Round((long) trackLength * range.Viewport, total);
        var length = Math.Clamp(proportional, 1, trackLength);
        var travel = trackLength - length;
        var offset = range.Value - range.Minimum;
        var start = travel == 0 ? 0 : Round((long) travel * offset, range.Span);
        return new Thumb(start, length);
    }

    /// <summary>Maps a dragged thumb offset back to the nearest range value.</summary>
    /// <param name="range">The validated scroll range.</param>
    /// <param name="trackLength">The non-negative available track cells.</param>
    /// <param name="thumbStart">The requested thumb start, clamped to travel.</param>
    /// <returns>The inclusive mapped value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trackLength"/> is negative.</exception>
    public static int ValueAt(Range range, int trackLength, int thumbStart)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackLength);
        Thumb thumb = Resolve(range, trackLength);
        var travel = trackLength - thumb.Length;

        if (travel == 0 || range.Span == 0)
        {
            return range.Minimum;
        }

        var start = Math.Clamp(thumbStart, 0, travel);
        var offset = Round((long) range.Span * start, travel);
        return range.Minimum + offset;
    }

    private static int Round(long numerator, long denominator) =>
        (int) ((numerator + (denominator / 2)) / denominator);
}
