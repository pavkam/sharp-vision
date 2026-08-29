// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Provides allocation-free semantic damage enumeration between complete frames.
/// </summary>
[PublicAPI]
public static class Damage
{
    private const int _maximumScrollDetectionRows = 512;

    /// <summary>Compares complete ordered semantic graphics placements.</summary>
    /// <param name="front">The committed frame, or null before the first commit.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete graphics reconstruction is required.</param>
    /// <returns>Whether a backend must replace its remote graphics state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="back"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    [Pure]
    public static bool PlacementsChanged(Frame? front, Frame back, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        return full || front is null || !front.EffectivePlacementsEqual(back);
    }

    /// <summary>Enumerates merged grapheme-safe changed runs.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether to force complete target damage.</param>
    /// <returns>An allocation-free enumerable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="back"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    public static DamageEnumerable Enumerate(Frame? front, Frame back, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        return new DamageEnumerable(front, back, full);
    }

    /// <summary>Enumerates damage after applying one already-emitted vertical scroll.</summary>
    internal static DamageEnumerable Enumerate(
        Frame front,
        Frame back,
        VerticalScrollDamage scroll) =>
        new(front, back, full: false, scroll);

    /// <summary>Enumerates damage between optional protocol cell projections.</summary>
    internal static DamageEnumerable Enumerate(
        Frame? front,
        Frame back,
        bool full,
        GraphicsCellOverlay? frontOverlay,
        GraphicsCellOverlay? backOverlay,
        VerticalScrollDamage scroll = default) =>
        new(front, back, full, scroll, frontOverlay, backOverlay);

    /// <summary>Finds the byte-beneficial vertical scroll that preserves the most changed cells.</summary>
    internal static bool TryFindVerticalScroll(
        Frame front,
        Frame back,
        GraphicsCellOverlay? frontOverlay,
        GraphicsCellOverlay? backOverlay,
        out VerticalScrollDamage scroll)
    {
        Debug.Assert(front.Size == back.Size, "Scroll detection requires stable geometry.");
        scroll = default;

        var graphicsCanScroll =
            (front.PlacementCount == 0 && back.PlacementCount == 0) ||
            (frontOverlay?.CoveredPlacementCount == front.PlacementCount &&
             backOverlay?.CoveredPlacementCount == back.PlacementCount);

        if (!graphicsCanScroll ||
            back.Size.Width == 0 ||
            back.Size.Height < 3 ||
            back.Size.Height > _maximumScrollDetectionRows)
        {
            return false;
        }

        var firstChanged = -1;
        var lastChanged = -1;
        var changedRows = 0;

        for (var row = 0; row < back.Size.Height; row++)
        {
            if (RowProbe(front, frontOverlay, row) != RowProbe(back, backOverlay, row))
            {
                firstChanged = firstChanged < 0 ? row : firstChanged;
                lastChanged = row;
                changedRows++;
            }
        }

        // One changed row cannot repay a region, scroll, reset, and exposed-row repaint.
        // This also keeps the unchanged and ordinary sparse-render paths linear.
        return changedRows >= 2 &&
               (TryFindVerticalScrollFromAnchor(
                   front,
                   back,
                   frontOverlay,
                   backOverlay,
                   firstChanged,
                   out scroll) ||
               (lastChanged != firstChanged &&
                TryFindVerticalScrollFromAnchor(
                    front,
                    back,
                    frontOverlay,
                    backOverlay,
                    lastChanged,
                    out scroll)));
    }

    private static bool TryFindVerticalScrollFromAnchor(
        Frame front,
        Frame back,
        GraphicsCellOverlay? frontOverlay,
        GraphicsCellOverlay? backOverlay,
        int anchor,
        out VerticalScrollDamage scroll)
    {
        var bestSavings = 0;
        var candidate = default(VerticalScrollDamage);

        for (var distance = 1; distance < back.Size.Height; distance++)
        {
            if (anchor + distance < back.Size.Height &&
                TrySourceRow(anchor + distance))
            {
                scroll = candidate;
                return true;
            }

            if (anchor - distance >= 0 && TrySourceRow(anchor - distance))
            {
                scroll = candidate;
                return true;
            }
        }

        scroll = default;
        return false;

        bool TrySourceRow(int sourceRow)
        {
            if (RowProbe(front, frontOverlay, sourceRow) != RowProbe(back, backOverlay, anchor) ||
                !RowsEqual(front, frontOverlay, sourceRow, back, backOverlay, anchor))
            {
                return false;
            }

            var sourceOffset = sourceRow - anchor;
            var first = Math.Max(0, -sourceOffset);
            var end = Math.Min(back.Size.Height, back.Size.Height - sourceOffset);
            var runStart = anchor;

            while (runStart > first &&
                   RowProbe(front, frontOverlay, runStart - 1 + sourceOffset) ==
                   RowProbe(back, backOverlay, runStart - 1) &&
                   RowsEqual(
                       front,
                       frontOverlay,
                       runStart - 1 + sourceOffset,
                       back,
                       backOverlay,
                       runStart - 1))
            {
                runStart--;
            }

            var runEnd = anchor + 1;

            while (runEnd < end &&
                   RowProbe(front, frontOverlay, runEnd + sourceOffset) ==
                   RowProbe(back, backOverlay, runEnd) &&
                   RowsEqual(
                       front,
                       frontOverlay,
                       runEnd + sourceOffset,
                       back,
                       backOverlay,
                       runEnd))
            {
                runEnd++;
            }

            ConsiderCandidate(
                front,
                back,
                runStart,
                runEnd,
                sourceOffset,
                frontOverlay,
                backOverlay,
                ref bestSavings,
                ref candidate);
            return candidate.IsActive;
        }
    }

    private static ulong RowProbe(Frame frame, GraphicsCellOverlay? overlay, int row)
    {
        if (overlay is null)
        {
            return frame.RowSemanticFingerprint(row);
        }

        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        var offset = checked(row * frame.Size.Width);
        var value = offsetBasis;

        for (var column = 0; column < frame.Size.Width; column++)
        {
            value = (value ^ CellProbe(frame, overlay, offset + column)) * prime;
        }

        return value;
    }

    private static ulong CellProbe(Frame frame, GraphicsCellOverlay? overlay, int index)
    {
        var projected = overlay?.GetCell(index) ?? default;

        if (projected.IsActive)
        {
            return (uint) projected.GetHashCode();
        }

        var cell = frame.GetCellByIndex(index);
        var value = ((ulong) cell.Hash << 32) | (uint) cell.Style.GetHashCode();
        value ^= (ulong) cell.Width << 24;
        value ^= (uint) cell.Length;
        value ^= (uint) (cell.IsContinuation ? (cell.LeadIndex % frame.Size.Width) + 2 : 1);
        return value;
    }

    private static void ConsiderCandidate(
        Frame front,
        Frame back,
        int runStart,
        int runEnd,
        int sourceOffset,
        GraphicsCellOverlay? frontOverlay,
        GraphicsCellOverlay? backOverlay,
        ref int bestSavings,
        ref VerticalScrollDamage best)
    {
        var top = Math.Min(runStart, runStart + sourceOffset);
        var bottom = Math.Max(runEnd - 1, runEnd - 1 + sourceOffset);

        if (top < 0 || bottom >= back.Size.Height || top == bottom)
        {
            return;
        }

        var changedCells = 0;
        var changedRows = 0;

        for (var row = runStart; row < runEnd; row++)
        {
            var rowChanged = false;
            var offset = checked(row * back.Size.Width);

            for (var column = 0; column < back.Size.Width; column++)
            {
                if (!GraphicsCellOverlay.CellsEqual(
                        front,
                        frontOverlay,
                        offset + column,
                        back,
                        backOverlay,
                        offset + column))
                {
                    changedCells++;
                    rowChanged = true;
                }
            }

            changedRows += rowChanged ? 1 : 0;
        }

        var exposedUnchangedCells = 0;
        var candidate = new VerticalScrollDamage(top, bottom, sourceOffset);

        for (var row = top; row <= bottom; row++)
        {
            if (!candidate.IsExposed(row))
            {
                continue;
            }

            var offset = checked(row * back.Size.Width);

            for (var column = 0; column < back.Size.Width; column++)
            {
                exposedUnchangedCells += GraphicsCellOverlay.CellsEqual(
                    front,
                    frontOverlay,
                    offset + column,
                    back,
                    backOverlay,
                    offset + column) ? 1 : 0;
            }
        }

        var commandBytes = 11 + Digits(top + 1) + Digits(bottom + 1) + Digits(candidate.Count);
        var savings = changedCells + (changedRows * 4) - exposedUnchangedCells - commandBytes;

        if (savings > bestSavings)
        {
            bestSavings = savings;
            best = candidate;
        }
    }

    private static int Digits(int value) => value switch
    {
        < 10 => 1,
        < 100 => 2,
        < 1_000 => 3,
        < 10_000 => 4,
        _ => 5
    };

    private static bool RowsEqual(
        Frame left,
        GraphicsCellOverlay? leftOverlay,
        int leftRow,
        Frame right,
        GraphicsCellOverlay? rightOverlay,
        int rightRow)
    {
        if (leftOverlay is null && rightOverlay is null)
        {
            return left.RowSemanticallyEquals(right, leftRow, rightRow);
        }

        var leftOffset = checked(leftRow * left.Size.Width);
        var rightOffset = checked(rightRow * right.Size.Width);

        for (var column = 0; column < left.Size.Width; column++)
        {
            if (!GraphicsCellOverlay.CellsEqual(
                    left,
                    leftOverlay,
                    leftOffset + column,
                    right,
                    rightOverlay,
                    rightOffset + column))
            {
                return false;
            }
        }

        return true;
    }
}
