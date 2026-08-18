// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Owns one ordered detached sequence of Table cell controls until a Table takes ownership.</summary>
[PublicAPI]
public sealed class TableRow
{
    private readonly ControlBase[] _cells;

    /// <summary>Initializes one row by copying validated detached cell controls.</summary>
    /// <param name="cells">The non-null non-empty ordered cell controls.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cells"/> or a cell is null.</exception>
    /// <exception cref="ArgumentException">The row is empty, repeats a control, or a cell is already owned or disposed.</exception>
    public TableRow(IEnumerable<ControlBase> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _cells = [.. cells];

        if (_cells.Length == 0)
        {
            throw new ArgumentException("A table row requires at least one cell.", nameof(cells));
        }

        HashSet<ControlBase> seen = [];

        foreach (var cell in _cells)
        {
            ArgumentNullException.ThrowIfNull(cell);

            if (!seen.Add(cell) || cell.Parent is not null || cell.IsDisposed)
            {
                throw new ArgumentException("Every table cell must be unique, detached, and available.", nameof(cells));
            }
        }

        Cells = Array.AsReadOnly(_cells);
    }

    /// <summary>Gets the ordered immutable cell sequence.</summary>
    public IReadOnlyList<ControlBase> Cells { get; }
}
