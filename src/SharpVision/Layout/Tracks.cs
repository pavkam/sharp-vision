// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Allocates integer cells across fixed, intrinsic, percentage, and star tracks.</summary>
[PublicAPI]
public static class Tracks
{
    #region Public allocation API

    /// <summary>Resolves tracks into a newly allocated array.</summary>
    /// <param name="available">The bounded axis, or null during intrinsic measure.</param>
    /// <param name="lengths">The validated track definitions.</param>
    /// <param name="automatic">The non-negative intrinsic request for each track.</param>
    /// <returns>One non-negative cell extent per definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="available"/> or an intrinsic request is negative.
    /// </exception>
    /// <exception cref="ArgumentException">The input lengths differ.</exception>
    public static int[] Resolve(
        int? available,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> automatic)
    {
        Validate(available, lengths, automatic, [], [], lengths.Length);
        var result = new int[lengths.Length];
        ResolveCore(available, lengths, automatic, [], [], result);
        return result;
    }

    /// <summary>Resolves tracks into caller-owned storage without managed allocation.</summary>
    /// <param name="available">The bounded axis, or null during intrinsic measure.</param>
    /// <param name="lengths">The validated track definitions.</param>
    /// <param name="automatic">The non-negative intrinsic request for each track.</param>
    /// <param name="minimum">The non-negative minimum for each track.</param>
    /// <param name="maximum">The maximum for each track, not below its minimum.</param>
    /// <param name="destination">Caller-owned output with one element per track.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="available"/>, an intrinsic request, or a limit is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Input/output lengths differ or a maximum is below its minimum.
    /// </exception>
    public static void Resolve(
        int? available,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> automatic,
        ReadOnlySpan<int> minimum,
        ReadOnlySpan<int> maximum,
        Span<int> destination)
    {
        Validate(available, lengths, automatic, minimum, maximum, destination.Length);
        ResolveCore(available, lengths, automatic, minimum, maximum, destination);
    }

    /// <summary>Expands a contiguous span of tracks to satisfy an intrinsic request.</summary>
    /// <param name="tracks">The caller-owned non-negative track extents.</param>
    /// <param name="start">The zero-based first track.</param>
    /// <param name="count">The positive number of tracks in the span.</param>
    /// <param name="required">The non-negative required combined extent.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The range, requirement, or an existing extent is invalid.
    /// </exception>
    public static void Satisfy(Span<int> tracks, int start, int count, int required)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(required);

        if (start > tracks.Length || count > tracks.Length - start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "The span must fit within the track storage.");
        }

        var current = 0L;

        for (var index = 0; index < tracks.Length; index++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tracks[index], nameof(tracks));

            if (index >= start && index < start + count)
            {
                current += tracks[index];
            }
        }

        if (current >= required)
        {
            return;
        }

        // Cumulative edges make every intermediate round deterministic while
        // the last track receives the exact deficit.
        var deficit = required - (int) current;
        var previous = 0;

        for (var offset = 0; offset < count; offset++)
        {
            var edge = RoundRatio(deficit, offset + 1, count);
            tracks[start + offset] += edge - previous;
            previous = edge;
        }
    }

    #endregion

    #region Allocation core

    private static void AllocateStars(
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> maximum,
        Span<int> destination,
        ref int remaining)
    {
        Debug.Assert(lengths.Length == destination.Length, "Star allocation requires one destination per track.");
        Debug.Assert(maximum.IsEmpty || maximum.Length == lengths.Length, "Star maxima align with track definitions.");
        Debug.Assert(remaining >= 0, "Star allocation receives a non-negative remainder.");

        while (remaining > 0)
        {
            var totalWeight = 0d;

            for (var index = 0; index < lengths.Length; index++)
            {
                if (lengths[index].Kind == Kind.Star &&
                    destination[index] < MaximumAt(maximum, index))
                {
                    totalWeight += lengths[index].Value;
                }
            }

            if (totalWeight == 0)
            {
                return;
            }

            // Re-run the distribution when a maximum clips one share. Any
            // clipped remainder is then divided only among eligible stars.
            var pass = remaining;
            var cumulativeWeight = 0d;
            var previousEdge = 0;
            var distributed = 0;

            for (var index = 0; index < lengths.Length; index++)
            {
                if (lengths[index].Kind != Kind.Star ||
                    destination[index] >= MaximumAt(maximum, index))
                {
                    continue;
                }

                cumulativeWeight += lengths[index].Value;
                var edge = RoundWeighted(pass, cumulativeWeight, totalWeight);
                var share = edge - previousEdge;
                var capacity = MaximumAt(maximum, index) - destination[index];
                var added = Math.Min(share, capacity);
                destination[index] += added;
                distributed += added;
                previousEdge = edge;
            }

            if (distributed == 0)
            {
                return;
            }

            remaining -= distributed;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(int value, ReadOnlySpan<int> minimum, ReadOnlySpan<int> maximum, int index) =>
        Math.Clamp(value, MinimumAt(minimum, index), MaximumAt(maximum, index));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MaximumAt(ReadOnlySpan<int> maximum, int index) =>
        maximum.IsEmpty ? int.MaxValue : maximum[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MinimumAt(ReadOnlySpan<int> minimum, int index) =>
        minimum.IsEmpty ? 0 : minimum[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPercent(int available, double percent)
    {
        Debug.Assert(available >= 0, "Percentage allocation uses a non-negative axis.");
        Debug.Assert(double.IsFinite(percent) && percent >= 0, "Percentage allocation uses a validated value.");

        var value = Math.Round(available * percent / 100, MidpointRounding.AwayFromZero);
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundRatio(int value, int numerator, int denominator)
    {
        Debug.Assert(value >= 0, "Ratio rounding uses a non-negative extent.");
        Debug.Assert(numerator >= 0 && numerator <= denominator, "Ratio rounding uses a bounded numerator.");
        Debug.Assert(denominator > 0, "Ratio rounding requires a positive denominator.");

        return (int) ((((long) value * numerator) + (denominator / 2L)) / denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundWeighted(int value, double cumulative, double total)
    {
        Debug.Assert(total > 0, "Weighted allocation requires a positive denominator.");
        var result = Math.Round(value * cumulative / total, MidpointRounding.AwayFromZero);
        return Math.Min(value, (int) result);
    }

    private static void ResolveCore(
        int? available,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> automatic,
        ReadOnlySpan<int> minimum,
        ReadOnlySpan<int> maximum,
        Span<int> destination)
    {
        Debug.Assert(lengths.Length == automatic.Length, "Validated resolution inputs have aligned lengths.");
        Debug.Assert(lengths.Length == destination.Length, "Validated resolution output matches track count.");
        Debug.Assert(minimum.IsEmpty || minimum.Length == lengths.Length, "Validated minima align with tracks.");
        Debug.Assert(maximum.IsEmpty || maximum.Length == lengths.Length, "Validated maxima align with tracks.");

        if (!available.HasValue)
        {
            for (var index = 0; index < lengths.Length; index++)
            {
                var requested = lengths[index].Kind == Kind.Cells
                    ? (int) lengths[index].Value
                    : automatic[index];
                destination[index] = Clamp(requested, minimum, maximum, index);
            }

            return;
        }

        long total = 0;
        var cumulativePercent = 0d;
        var previousPercentEdge = 0;

        // Fixed and intrinsic reservations are independent. Percentage edges
        // share one cumulative coordinate system based on the final axis.
        for (var index = 0; index < lengths.Length; index++)
        {
            var requested = lengths[index].Kind switch
            {
                Kind.Auto => automatic[index],
                Kind.Cells => (int) lengths[index].Value,
                Kind.Percent => ResolvePercentRequest(
                    available.Value,
                    lengths[index].Value,
                    ref cumulativePercent,
                    ref previousPercentEdge),
                Kind.Star => MinimumAt(minimum, index),
                _ => throw new UnreachableException()
            };

            destination[index] = Clamp(requested, minimum, maximum, index);
            total += destination[index];
        }

        if (total > available.Value)
        {
            var deficit = total - available.Value;
            Shrink(Kind.Percent, lengths, minimum, destination, ref deficit);
            Shrink(Kind.Auto, lengths, minimum, destination, ref deficit);
            Shrink(Kind.Cells, lengths, minimum, destination, ref deficit);
            Shrink(Kind.Star, lengths, minimum, destination, ref deficit);

            // When caller minima are infeasible, containment wins. This mirrors
            // tiny-view box layout and prevents negative or overflowing bounds.
            if (deficit > 0)
            {
                ShrinkBelowMinimum(lengths, destination, ref deficit);
            }

            total = available.Value;
        }

        var remaining = available.Value - (int) total;
        AllocateStars(lengths, maximum, destination, ref remaining);
    }

    private static int ResolvePercentRequest(
        int available,
        double percent,
        ref double cumulative,
        ref int previousEdge)
    {
        Debug.Assert(available >= 0, "Percentage requests use a non-negative final axis.");
        Debug.Assert(double.IsFinite(percent) && percent >= 0, "Percentage requests use a validated percentage.");
        Debug.Assert(cumulative >= 0 && previousEdge >= 0, "Cumulative percentage state is non-negative.");

        cumulative += percent;
        var edge = RoundPercent(available, cumulative);
        var requested = Math.Max(0, edge - previousEdge);
        previousEdge = edge;
        return requested;
    }

    private static void Shrink(
        Kind kind,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> minimum,
        Span<int> destination,
        ref long deficit)
    {
        Debug.Assert(Enum.IsDefined(kind), "Track shrinking requires a defined length kind.");
        Debug.Assert(lengths.Length == destination.Length, "Track shrinking aligns definitions and extents.");
        Debug.Assert(minimum.IsEmpty || minimum.Length == lengths.Length, "Track shrinking aligns minima.");
        Debug.Assert(deficit >= 0, "Track shrinking receives a non-negative deficit.");

        for (var index = lengths.Length - 1; index >= 0 && deficit > 0; index--)
        {
            if (lengths[index].Kind != kind)
            {
                continue;
            }

            var removable = destination[index] - MinimumAt(minimum, index);
            var removed = (int) Math.Min(deficit, removable);
            destination[index] -= removed;
            deficit -= removed;
        }
    }

    private static void ShrinkBelowMinimum(
        ReadOnlySpan<Length> lengths,
        Span<int> destination,
        ref long deficit)
    {
        Debug.Assert(lengths.Length == destination.Length, "Below-minimum shrinking aligns definitions and extents.");
        Debug.Assert(deficit >= 0, "Below-minimum shrinking receives a non-negative deficit.");

        ReadOnlySpan<Kind> priority = [Kind.Percent, Kind.Auto, Kind.Cells, Kind.Star];

        foreach (var kind in priority)
        {
            for (var index = lengths.Length - 1; index >= 0 && deficit > 0; index--)
            {
                if (lengths[index].Kind != kind)
                {
                    continue;
                }

                var removed = (int) Math.Min(deficit, destination[index]);
                destination[index] -= removed;
                deficit -= removed;
            }
        }
    }

    #endregion

    #region Validation

    private static void Validate(
        int? available,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> automatic,
        ReadOnlySpan<int> minimum,
        ReadOnlySpan<int> maximum,
        int destinationLength)
    {
        Debug.Assert(destinationLength >= 0, "Destination lengths are non-negative.");

        if (available.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(available.Value, nameof(available));
        }

        if (automatic.Length != lengths.Length || destinationLength != lengths.Length ||
            (!minimum.IsEmpty && minimum.Length != lengths.Length) ||
            (!maximum.IsEmpty && maximum.Length != lengths.Length) ||
            minimum.IsEmpty != maximum.IsEmpty)
        {
            throw new ArgumentException("Every track input and output must have the same length.");
        }

        for (var index = 0; index < lengths.Length; index++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(automatic[index], nameof(automatic));

            if (!minimum.IsEmpty)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(minimum[index], nameof(minimum));
                ArgumentOutOfRangeException.ThrowIfNegative(maximum[index], nameof(maximum));

                if (maximum[index] < minimum[index])
                {
                    throw new ArgumentException(
                        "A track maximum cannot be below its minimum.",
                        nameof(maximum));
                }
            }
        }
    }

    #endregion
}
