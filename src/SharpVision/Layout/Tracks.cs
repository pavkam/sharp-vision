// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;


/// <summary>Allocates integer cells across fixed, intrinsic, percentage, and star tracks.</summary>
public static class Tracks
{
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
        int[] result = new int[lengths.Length];
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

        long current = 0;

        for (int index = 0; index < tracks.Length; index++)
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
        int deficit = required - (int) current;
        int previous = 0;

        for (int offset = 0; offset < count; offset++)
        {
            int edge = RoundRatio(deficit, offset + 1, count);
            tracks[start + offset] += edge - previous;
            previous = edge;
        }
    }

    private static void AllocateStars(
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> maximum,
        Span<int> destination,
        ref int remaining)
    {
        while (remaining > 0)
        {
            double totalWeight = 0d;

            for (int index = 0; index < lengths.Length; index++)
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
            int pass = remaining;
            double cumulativeWeight = 0d;
            int previousEdge = 0;
            int distributed = 0;

            for (int index = 0; index < lengths.Length; index++)
            {
                if (lengths[index].Kind != Kind.Star ||
                    destination[index] >= MaximumAt(maximum, index))
                {
                    continue;
                }

                cumulativeWeight += lengths[index].Value;
                int edge = RoundWeighted(pass, cumulativeWeight, totalWeight);
                int share = edge - previousEdge;
                int capacity = MaximumAt(maximum, index) - destination[index];
                int added = Math.Min(share, capacity);
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

    private static int Clamp(int value, ReadOnlySpan<int> minimum, ReadOnlySpan<int> maximum, int index) =>
        Math.Clamp(value, MinimumAt(minimum, index), MaximumAt(maximum, index));

    private static int MaximumAt(ReadOnlySpan<int> maximum, int index) =>
        maximum.IsEmpty ? int.MaxValue : maximum[index];

    private static int MinimumAt(ReadOnlySpan<int> minimum, int index) =>
        minimum.IsEmpty ? 0 : minimum[index];

    private static int RoundPercent(int available, double percent)
    {
        double value = Math.Round(available * percent / 100, MidpointRounding.AwayFromZero);
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private static int RoundRatio(int value, int numerator, int denominator) =>
        (int) ((((long) value * numerator) + (denominator / 2L)) / denominator);

    private static int RoundWeighted(int value, double cumulative, double total)
    {
        Debug.Assert(total > 0, "Weighted allocation requires a positive denominator.");
        double result = Math.Round(value * cumulative / total, MidpointRounding.AwayFromZero);
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
        if (!available.HasValue)
        {
            for (int index = 0; index < lengths.Length; index++)
            {
                int requested = lengths[index].Kind == Kind.Cells
                    ? (int) lengths[index].Value
                    : automatic[index];
                destination[index] = Clamp(requested, minimum, maximum, index);
            }

            return;
        }

        long total = 0;
        double cumulativePercent = 0d;
        int previousPercentEdge = 0;

        // Fixed and intrinsic reservations are independent. Percentage edges
        // share one cumulative coordinate system based on the final axis.
        for (int index = 0; index < lengths.Length; index++)
        {
            int requested = lengths[index].Kind switch
            {
                Kind.Auto => automatic[index],
                Kind.Cells => (int) lengths[index].Value,
                Kind.Percent => ResolvePercentRequest(
                    available.Value,
                    lengths[index].Value,
                    ref cumulativePercent,
                    ref previousPercentEdge),
                Kind.Star => MinimumAt(minimum, index),
                _ => throw new UnreachableException(),
            };

            destination[index] = Clamp(requested, minimum, maximum, index);
            total += destination[index];
        }

        if (total > available.Value)
        {
            long deficit = total - available.Value;
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

        int remaining = available.Value - (int) total;
        AllocateStars(lengths, maximum, destination, ref remaining);
    }

    private static int ResolvePercentRequest(
        int available,
        double percent,
        ref double cumulative,
        ref int previousEdge)
    {
        cumulative += percent;
        int edge = RoundPercent(available, cumulative);
        int requested = Math.Max(0, edge - previousEdge);
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
        for (int index = lengths.Length - 1; index >= 0 && deficit > 0; index--)
        {
            if (lengths[index].Kind != kind)
            {
                continue;
            }

            int removable = destination[index] - MinimumAt(minimum, index);
            int removed = (int) Math.Min(deficit, removable);
            destination[index] -= removed;
            deficit -= removed;
        }
    }

    private static void ShrinkBelowMinimum(
        ReadOnlySpan<Length> lengths,
        Span<int> destination,
        ref long deficit)
    {
        ReadOnlySpan<Kind> priority = [Kind.Percent, Kind.Auto, Kind.Cells, Kind.Star];

        foreach (Kind kind in priority)
        {
            for (int index = lengths.Length - 1; index >= 0 && deficit > 0; index--)
            {
                if (lengths[index].Kind != kind)
                {
                    continue;
                }

                int removed = (int) Math.Min(deficit, destination[index]);
                destination[index] -= removed;
                deficit -= removed;
            }
        }
    }

    private static void Validate(
        int? available,
        ReadOnlySpan<Length> lengths,
        ReadOnlySpan<int> automatic,
        ReadOnlySpan<int> minimum,
        ReadOnlySpan<int> maximum,
        int destinationLength)
    {
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

        for (int index = 0; index < lengths.Length; index++)
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
}
