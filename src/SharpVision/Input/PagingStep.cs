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
    /// <c>[0, count - 1]</c>, which requires <paramref name="count"/> to be at least one; when
    /// false, it is returned as-is for the caller to resolve.</param>
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

    /// <summary>Computes the accumulated-extent target for a page step across a viewport, leaving
    /// <paramref name="pageOverlap"/> cells of the current viewport in view.</summary>
    /// <param name="viewportExtent">The extent of the viewport being paged, in cells.</param>
    /// <param name="pageOverlap">The number of cells to retain from the current viewport; clamped to
    /// <paramref name="viewportExtent"/> so it can never overshoot it.</param>
    /// <returns>The target extent to pass to <see cref="Accumulate"/>, floored at one so a page step
    /// always advances by at least one item even when the viewport or overlap leaves no room.</returns>
    public static int TargetExtent(int viewportExtent, int pageOverlap) =>
        Math.Max(1, viewportExtent - Math.Min(pageOverlap, viewportExtent));

    /// <summary>Computes the minimal vertical offset that brings a fixed-height item's row slot fully
    /// inside the viewport, using saturating arithmetic so an extreme index or item extent clamps
    /// instead of silently wrapping.</summary>
    /// <param name="index">The zero-based item position whose row slot is being brought into view.</param>
    /// <param name="itemExtent">The fixed per-item extent, in cells.</param>
    /// <param name="currentOffset">The offset already in effect; returned unchanged when the row slot is
    /// already fully visible.</param>
    /// <param name="viewportExtent">The extent of the viewport being scrolled, in cells.</param>
    /// <param name="contentExtent">The total scrollable content extent, in cells.</param>
    /// <returns>The offset to scroll to, clamped to <c>[0, max(0, contentExtent - viewportExtent)]</c>.</returns>
    public static int IndexIntoViewOffset(int index, int itemExtent, int currentOffset, int viewportExtent, int contentExtent)
    {
        var start = index.Multiply(itemExtent);

        var target = start < currentOffset
            ? start
            : start.Add(itemExtent) > currentOffset.Add(viewportExtent)
                ? Math.Max(0, start.Add(itemExtent) - viewportExtent)
                : currentOffset;

        var maximum = Math.Max(0, contentExtent - viewportExtent);
        return Math.Clamp(target, 0, maximum);
    }
}
