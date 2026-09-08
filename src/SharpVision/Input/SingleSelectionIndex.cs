// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;

/// <summary>Locates the eligible index a single-selection cursor should land on next, over a
/// collection whose items and their eligibility can change out from under it.</summary>
/// <remarks>
/// A single-selection cursor tracks one active index into a collection where not every position
/// is a valid target - some items are disabled, hidden, or otherwise ineligible - and both that
/// eligibility and the collection's size can change between selections. These three scans answer
/// the questions such a cursor repeatedly needs: stepping toward an explicit end without wrapping,
/// repairing a selection a mutation left on an ineligible or out-of-range index by finding the
/// nearest surviving eligible one, and cycling continuously past either end back to the other.
/// </remarks>
[PublicAPI]
public static class SingleSelectionIndex
{
    /// <summary>Scans from <paramref name="start"/> toward <paramref name="direction"/>,
    /// inclusive, stopping at the first eligible index or at either end of the collection.</summary>
    /// <param name="start">The index to begin scanning from, inclusive. A value outside
    /// <c>[0, count)</c> is accepted and simply yields no eligible index in that direction, rather
    /// than throwing, so a caller may pass one-past-either-end without special-casing it.</param>
    /// <param name="direction">Plus one to scan toward the end, or minus one to scan toward the
    /// start.</param>
    /// <param name="count">The number of items in the collection.</param>
    /// <param name="isEligible">Reports whether the item at a given index can be selected.</param>
    /// <returns>The first eligible index reached, or -1 when the scan runs past either end without
    /// finding one.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> is not <c>1</c> or <c>-1</c>, or <paramref name="count"/> is
    /// negative.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="isEligible"/> is null.</exception>
    [Pure]
    public static int FindLinear(int start, int direction, int count, [InstantHandle] Func<int, bool> isEligible)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "The direction must be 1 or -1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(isEligible);

        for (var index = start; index >= 0 && index < count; index += direction)
        {
            if (isEligible(index))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Finds the eligible index nearest to <paramref name="start"/>: an inclusive forward
    /// scan from <paramref name="start"/>, falling back to the nearest eligible predecessor when
    /// nothing eligible is found ahead. Never wraps.</summary>
    /// <param name="start">The index to repair, typically one a mutation left ineligible or past
    /// the end of the collection.</param>
    /// <param name="count">The number of items in the collection.</param>
    /// <param name="isEligible">Reports whether the item at a given index can be selected.</param>
    /// <returns>The nearest eligible index, or -1 when no item in the collection is eligible.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="isEligible"/> is null.</exception>
    [Pure]
    public static int FindNearest(int start, int count, [InstantHandle] Func<int, bool> isEligible)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(isEligible);

        var successor = FindLinear(Math.Max(0, start), 1, count, isEligible);
        return successor >= 0 ? successor : FindLinear(Math.Min(start - 1, count - 1), -1, count, isEligible);
    }

    /// <summary>Finds the next eligible index circularly: starting one step past
    /// <paramref name="start"/> in <paramref name="direction"/> and wrapping past either end of
    /// the collection back to the other until every index has been visited once.</summary>
    /// <param name="start">The current index to step away from, or -1 for no current selection.</param>
    /// <param name="direction">Plus one to step toward the end, or minus one to step toward the
    /// start.</param>
    /// <param name="count">The number of items in the collection.</param>
    /// <param name="isEligible">Reports whether the item at a given index can be selected.</param>
    /// <returns>The next eligible index, or -1 when no item is eligible or the collection is
    /// empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> is not <c>1</c> or <c>-1</c>, <paramref name="count"/> is
    /// negative, or <paramref name="start"/> is less than -1.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="isEligible"/> is null.</exception>
    [Pure]
    public static int FindWrapped(int start, int direction, int count, [InstantHandle] Func<int, bool> isEligible)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "The direction must be 1 or -1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(start, -1);
        ArgumentNullException.ThrowIfNull(isEligible);

        if (count == 0)
        {
            return -1;
        }

        // A cleared selection (start < 0) has no current item to step away from in either
        // direction. Stepping forward from it must land on the first item (index 0); by the same
        // "conceptually just past either end" reasoning, stepping backward from it must land on
        // the last item, not one item short of it. Rebasing the backward search to begin as if it
        // were already positioned one past the end keeps both directions symmetric.
        var origin = start >= 0 ? start : direction < 0 ? count : -1;

        for (var offset = 1; offset <= count; offset++)
        {
            var index = (origin + (direction * offset) + count) % count;

            if (isEligible(index))
            {
                return index;
            }
        }

        return -1;
    }
}
