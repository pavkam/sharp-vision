// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Advances a collection index by accumulating realized item extents toward a target.</summary>
/// <remarks>
/// A page step distributes cell-space distance across a variable number of items rather than
/// applying it as a fixed item-index delta, so the loop advances one item at a time and sums each
/// visited item's realized extent (treating a negative extent as zero) until the running total
/// reaches the target. The loop always advances before its first accumulated check, so at least
/// one step is taken even when the very first item's extent alone would satisfy the target.
/// </remarks>
internal static class PagingStep
{
    /// <summary>Walks from <paramref name="start"/> in <paramref name="direction"/>, stopping once
    /// the accumulated extent reaches <paramref name="target"/> or the walk runs past either end of
    /// the collection.</summary>
    /// <param name="start">The index to advance from; not itself included in the accumulated extent.</param>
    /// <param name="direction">Plus or minus one.</param>
    /// <param name="count">The number of items in the collection being paged.</param>
    /// <param name="target">The accumulated extent to reach or exceed before stopping.</param>
    /// <param name="extentAt">Returns the realized extent of the item at a given index; a negative
    /// result is treated as zero.</param>
    /// <param name="clamp">When true, an index that runs past either end is clamped into
    /// <c>[0, count - 1]</c>; when false, it is returned as-is for the caller to resolve.</param>
    /// <returns>The resulting index.</returns>
    public static int Accumulate(int start, int direction, int count, int target, Func<int, int> extentAt, bool clamp)
    {
        var index = start;
        var accumulated = 0;

        while (true)
        {
            var next = index + direction;

            if (next < 0 || next >= count)
            {
                return clamp ? Math.Clamp(next, 0, count - 1) : next;
            }

            index = next;
            accumulated += Math.Max(0, extentAt(index));

            if (accumulated >= target)
            {
                return index;
            }
        }
    }
}
