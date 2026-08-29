// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Owns one immutable-after-prepare protocol cell projection for a complete frame.</summary>
internal sealed class GraphicsCellOverlay
{
    private readonly GraphicsCellOverlayValue[] _cells;

    /// <summary>Initializes an empty overlay matching one active frame.</summary>
    /// <param name="frame">The non-null borrowed semantic frame.</param>
    public GraphicsCellOverlay(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.ThrowIfDisposed();
        Size = frame.Size;
        _cells = new GraphicsCellOverlayValue[checked(Size.Width * Size.Height)];
    }

    /// <summary>Gets the exact frame dimensions.</summary>
    public Size Size { get; }

    /// <summary>Gets whether at least one cell is protocol-owned.</summary>
    public bool HasCells { get; private set; }

    /// <summary>Gets the number of semantic placements represented by this overlay.</summary>
    public int CoveredPlacementCount { get; private set; }

    /// <summary>Paints one complete virtual placement over earlier overlay cells.</summary>
    /// <param name="frame">The same-sized semantic frame supplying background underlay.</param>
    /// <param name="destination">The in-bounds positive destination.</param>
    /// <param name="imageId">The nonzero terminal-assigned image identifier.</param>
    /// <param name="placementId">The nonzero virtual placement identifier.</param>
    /// <param name="identityColorDepth">The exact identifier color representation.</param>
    public void Paint(
        Frame frame,
        Rect destination,
        uint imageId,
        uint placementId,
        ColorDepth identityColorDepth)
    {
        Debug.Assert(frame.Size == Size, "An overlay and its semantic frame share geometry.");
        Debug.Assert(imageId != 0, "A placeholder uses an assigned image identifier.");
        Debug.Assert(placementId != 0, "A placeholder uses a virtual placement identifier.");

        for (var row = 0; row < destination.Height; row++)
        {
            for (var column = 0; column < destination.Width; column++)
            {
                var index = checked(((destination.Y + row) * Size.Width) + destination.X + column);
                _cells[index] = new GraphicsCellOverlayValue(
                    imageId,
                    placementId,
                    row,
                    column,
                    frame.GetCellByIndex(index).Style.Background,
                    identityColorDepth);
            }
        }

        HasCells = true;
        CoveredPlacementCount++;
    }

    /// <summary>Gets one value by validated absolute frame index.</summary>
    /// <param name="index">The absolute row-major cell index.</param>
    /// <returns>The active projected cell or the default inactive value.</returns>
    [Pure]
    public GraphicsCellOverlayValue GetCell(int index) => _cells[index];

    /// <summary>Compares complete cell projection state.</summary>
    /// <param name="other">The other overlay, or null for no projected cells.</param>
    /// <returns>Whether dimensions and every projected value are equal.</returns>
    [Pure]
    public bool SemanticallyEquals(GraphicsCellOverlay? other) =>
        other is not null && Size == other.Size && _cells.AsSpan().SequenceEqual(other._cells);

    /// <summary>Compares two projected cells, falling through to semantic frames when inactive.</summary>
    internal static bool CellsEqual(
        Frame leftFrame,
        GraphicsCellOverlay? leftOverlay,
        int leftIndex,
        Frame rightFrame,
        GraphicsCellOverlay? rightOverlay,
        int rightIndex)
    {
        var left = leftOverlay?.GetCell(leftIndex) ?? default;
        var right = rightOverlay?.GetCell(rightIndex) ?? default;

        return left.IsActive || right.IsActive
            ? left == right
            : leftFrame.CellSemanticallyEquals(rightFrame, leftIndex, rightIndex);
    }
}
