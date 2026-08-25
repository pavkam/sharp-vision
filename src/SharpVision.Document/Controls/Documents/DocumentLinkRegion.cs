// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Records one contiguous stretch of cells that activates a link.</summary>
/// <remarks>
/// A link that wraps produces one region per line it occupies, all sharing
/// <see cref="LinkIndex"/>. Every region is clickable, and focus reveal uses the first, so a wrapped
/// link behaves as one link rather than several.
/// </remarks>
internal readonly struct DocumentLinkRegion
{
    /// <summary>Initializes a clickable region.</summary>
    /// <param name="linkIndex">The non-negative index of the owning link in document order.</param>
    /// <param name="line">The non-negative line the region occupies.</param>
    /// <param name="column">The non-negative first column of the region.</param>
    /// <param name="cells">The positive cell width of the region.</param>
    public DocumentLinkRegion(int linkIndex, int line, int column, int cells)
    {
        Debug.Assert(linkIndex >= 0, "A link region belongs to a real link.");
        Debug.Assert(line >= 0, "A link region sits on a real line.");
        Debug.Assert(column >= 0, "A link region starts at a non-negative column.");
        Debug.Assert(cells > 0, "A link region covers at least one cell.");

        LinkIndex = linkIndex;
        Line = line;
        Column = column;
        Cells = cells;
    }

    /// <summary>Gets the index of the owning link in document order.</summary>
    public int LinkIndex { get; }

    /// <summary>Gets the line the region occupies.</summary>
    public int Line { get; }

    /// <summary>Gets the first column of the region.</summary>
    public int Column { get; }

    /// <summary>Gets the cell width of the region.</summary>
    public int Cells { get; }

    /// <summary>Gets whether a column falls inside this region.</summary>
    /// <param name="column">The candidate column.</param>
    /// <returns>True when the column is covered.</returns>
    [Pure]
    public bool Contains(int column) => column >= Column && column < (long) Column + Cells;
}
