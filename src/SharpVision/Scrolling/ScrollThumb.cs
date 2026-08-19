// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;

using SharpVision.Controls;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Describes one contained integer scrollbar thumb.</summary>
[PublicAPI]
public readonly record struct ScrollThumb
{
    /// <summary>Initializes non-negative thumb geometry.</summary>
    /// <param name="start">The non-negative track offset.</param>
    /// <param name="length">The non-negative thumb length.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative.</exception>
    public ScrollThumb(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Start = start;
        Length = length;
    }

    /// <summary>Gets the thumb offset from the track start.</summary>
    [NonNegativeValue]
    public int Start { get; }

    /// <summary>Gets the thumb length.</summary>
    [NonNegativeValue]
    public int Length { get; }

    /// <summary>Resolves stable contained thumb geometry for one track.</summary>
    /// <param name="range">The validated scroll range.</param>
    /// <param name="trackLength">The non-negative available track cells.</param>
    /// <returns>The contained thumb.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trackLength"/> is negative.</exception>
    [Pure]
    public static ScrollThumb Resolve(ScrollRange range, int trackLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackLength);

        if (trackLength == 0)
        {
            return default;
        }

        if (range.Span == 0)
        {
            return new ScrollThumb(0, trackLength);
        }

        var total = (long) range.Span + range.Viewport;
        var proportional = total == 0
            ? 0
            : RangeValidation.RoundHalfUp((long) trackLength * range.Viewport, total);
        var length = Math.Clamp(proportional, 1, trackLength);
        var travel = trackLength - length;
        var offset = range.Value - range.Minimum;
        var start = travel == 0 ? 0 : RangeValidation.RoundHalfUp((long) travel * offset, range.Span);

        return new ScrollThumb(start, length);
    }

    /// <summary>Maps a dragged thumb offset back to the nearest range value.</summary>
    /// <param name="range">The validated scroll range.</param>
    /// <param name="trackLength">The non-negative available track cells.</param>
    /// <param name="thumbStart">The requested thumb start, clamped to travel.</param>
    /// <returns>The inclusive mapped value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trackLength"/> is negative.</exception>
    [Pure]
    [NonNegativeValue]
    public static int ValueAt(ScrollRange range, int trackLength, int thumbStart)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackLength);
        var thumb = Resolve(range, trackLength);
        var travel = trackLength - thumb.Length;

        if (travel == 0 || range.Span == 0)
        {
            return range.Minimum;
        }

        var start = Math.Clamp(thumbStart, 0, travel);
        var offset = RangeValidation.RoundHalfUp((long) range.Span * start, travel);
        return range.Minimum + offset;
    }
}
