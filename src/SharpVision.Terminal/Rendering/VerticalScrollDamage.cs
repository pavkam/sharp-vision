// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Describes one vertical region whose retained rows move by a fixed offset.</summary>
internal readonly struct VerticalScrollDamage
{
    /// <summary>Initializes one validated zero-based inclusive region and source-row offset.</summary>
    /// <param name="top">The inclusive top row.</param>
    /// <param name="bottom">The inclusive bottom row.</param>
    /// <param name="sourceOffset">The nonzero source row offset for each retained target row.</param>
    public VerticalScrollDamage(int top, int bottom, int sourceOffset)
    {
        Debug.Assert(top >= 0, "A scroll region starts inside the frame.");
        Debug.Assert(bottom > top, "A useful scroll region contains at least two rows.");
        Debug.Assert(sourceOffset != 0, "A scroll transform moves at least one row.");
        Top = top;
        Bottom = bottom;
        SourceOffset = sourceOffset;
    }

    /// <summary>Gets the inclusive zero-based top row.</summary>
    public int Top { get; }

    /// <summary>Gets the inclusive zero-based bottom row.</summary>
    public int Bottom { get; }

    /// <summary>Gets the source-row offset; positive scrolls content up.</summary>
    public int SourceOffset { get; }

    /// <summary>Gets the positive scroll distance.</summary>
    public int Count => Math.Abs(SourceOffset);

    /// <summary>Gets whether this value describes an active scroll.</summary>
    public bool IsActive => SourceOffset != 0;

    /// <summary>Maps a target row to its retained source row or reports newly exposed content.</summary>
    /// <param name="row">The zero-based target row.</param>
    /// <param name="sourceRow">Receives the original source row for retained content.</param>
    /// <returns>True when the row survives the scroll rather than becoming exposed.</returns>
    public bool TryMapSourceRow(int row, out int sourceRow)
    {
        sourceRow = row + SourceOffset;
        return row >= Top &&
            row <= Bottom &&
            sourceRow >= Top &&
            sourceRow <= Bottom;
    }

    /// <summary>Gets whether the row is newly exposed by this scroll.</summary>
    /// <param name="row">The zero-based target row.</param>
    /// <returns>True when the terminal creates a blank row that must be compared as exposure.</returns>
    public bool IsExposed(int row) =>
        row >= Top && row <= Bottom && !TryMapSourceRow(row, out _);
}
