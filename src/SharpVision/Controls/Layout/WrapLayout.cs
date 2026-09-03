// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Packs measured margin-inclusive child extents into deterministic wrap lines.</summary>
internal static class WrapLayout
{
    /// <summary>Packs each outer child extent in source order and returns the packed extent.</summary>
    /// <param name="outerSizes">The non-negative margin-inclusive child desired sizes.</param>
    /// <param name="primaryLimit">The optional non-negative finite primary lane extent.</param>
    /// <param name="orientation">The axis along which each line advances.</param>
    /// <param name="spacing">The non-negative gap between adjacent children in one line.</param>
    /// <param name="lineSpacing">The non-negative gap between adjacent non-empty lines.</param>
    /// <param name="outerSlots">The destination span for margin-inclusive relative child slots.</param>
    /// <returns>The non-negative desired extent of all packed lines.</returns>
    /// <remarks>
    /// This arithmetic deliberately owns neither controls nor layout phases. Keeping it pure lets
    /// measure and arrange use identical line decisions without retaining a mutable control tree.
    /// </remarks>
    internal static Size Pack(
        ReadOnlySpan<Size> outerSizes,
        int? primaryLimit,
        Orientation orientation,
        int spacing,
        int lineSpacing,
        Span<Rect> outerSlots)
    {
        Debug.Assert(primaryLimit is null or >= 0, "A finite primary lane is non-negative.");
        Debug.Assert(spacing >= 0, "Wrap item spacing is non-negative.");
        Debug.Assert(lineSpacing >= 0, "Wrap line spacing is non-negative.");
        Debug.Assert(outerSlots.Length >= outerSizes.Length, "Every packed child has a destination slot.");

        var primaryOrigin = 0;
        var lineOrigin = 0;
        var linePrimary = 0;
        var lineCross = 0;
        var lineStart = 0;
        var lineCount = 0;
        var desiredPrimary = 0;

        for (var index = 0; index < outerSizes.Length; index++)
        {
            var size = outerSizes[index];
            var requestedPrimary = Primary(size, orientation);
            var itemPrimary = primaryLimit.HasValue
                ? Math.Min(primaryLimit.Value, requestedPrimary)
                : requestedPrimary;
            var itemCross = Cross(size, orientation);
            // The line total itself is saturated for desired-size reporting, but fit must use
            // the exact sum. Otherwise an item after an int.MaxValue-wide predecessor appears
            // to fit merely because the overflow has already saturated to the lane endpoint.
            var fits = !primaryLimit.HasValue ||
                lineCount == 0 ||
                (long) linePrimary + spacing + itemPrimary <= primaryLimit.Value;

            if (!fits)
            {
                FinalizeLine(outerSlots, lineStart, index, orientation, lineCross);
                desiredPrimary = Math.Max(desiredPrimary, linePrimary);
                lineOrigin = lineOrigin.Add(lineCross).Add(lineSpacing);
                primaryOrigin = 0;
                linePrimary = 0;
                lineCross = 0;
                lineStart = index;
                lineCount = 0;
            }

            if (lineCount > 0)
            {
                primaryOrigin = primaryOrigin.Add(spacing);
                linePrimary = linePrimary.Add(spacing);
            }

            outerSlots[index] = CreateSlot(primaryOrigin, lineOrigin, itemPrimary, itemCross, orientation);
            primaryOrigin = primaryOrigin.Add(itemPrimary);
            linePrimary = linePrimary.Add(itemPrimary);
            lineCross = Math.Max(lineCross, itemCross);
            lineCount++;
        }

        if (lineCount == 0)
        {
            return default;
        }

        FinalizeLine(outerSlots, lineStart, outerSizes.Length, orientation, lineCross);
        desiredPrimary = Math.Max(desiredPrimary, linePrimary);
        var desiredCross = lineOrigin.Add(lineCross);
        return orientation == Orientation.Horizontal
            ? new Size(desiredPrimary, desiredCross)
            : new Size(desiredCross, desiredPrimary);
    }

    [Pure]
    private static int Primary(Size size, Orientation orientation) =>
        orientation == Orientation.Horizontal ? size.Width : size.Height;

    [Pure]
    private static int Cross(Size size, Orientation orientation) =>
        orientation == Orientation.Horizontal ? size.Height : size.Width;

    [Pure]
    private static Rect CreateSlot(int primary, int cross, int primaryExtent, int crossExtent, Orientation orientation) =>
        orientation == Orientation.Horizontal
            ? new Rect(primary, cross, primaryExtent, crossExtent)
            : new Rect(cross, primary, crossExtent, primaryExtent);

    private static void FinalizeLine(
        Span<Rect> outerSlots,
        int start,
        int end,
        Orientation orientation,
        int crossExtent)
    {
        for (var index = start; index < end; index++)
        {
            var slot = outerSlots[index];
            outerSlots[index] = orientation == Orientation.Horizontal
                ? new Rect(slot.X, slot.Y, slot.Width, crossExtent)
                : new Rect(slot.X, slot.Y, crossExtent, slot.Height);
        }
    }
}
