// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes the rows and cells added to or removed from a Table selection.</summary>
public sealed class TableSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes a selection change snapshot.</summary>
    /// <param name="addedRows">The rows newly selected.</param>
    /// <param name="removedRows">The rows no longer selected.</param>
    /// <param name="addedCells">The cells newly selected.</param>
    /// <param name="removedCells">The cells no longer selected.</param>
    /// <exception cref="ArgumentNullException">A collection argument is null.</exception>
    public TableSelectionChangedEventArgs(
        IReadOnlyList<TableRow> addedRows,
        IReadOnlyList<TableRow> removedRows,
        IReadOnlyList<TableCellReference> addedCells,
        IReadOnlyList<TableCellReference> removedCells)
    {
        ArgumentNullException.ThrowIfNull(addedRows);
        ArgumentNullException.ThrowIfNull(removedRows);
        ArgumentNullException.ThrowIfNull(addedCells);
        ArgumentNullException.ThrowIfNull(removedCells);
        AddedRows = Array.AsReadOnly(addedRows.ToArray());
        RemovedRows = Array.AsReadOnly(removedRows.ToArray());
        AddedCells = Array.AsReadOnly(addedCells.ToArray());
        RemovedCells = Array.AsReadOnly(removedCells.ToArray());
    }

    /// <summary>Gets the rows newly selected.</summary>
    public IReadOnlyList<TableRow> AddedRows { get; }

    /// <summary>Gets the rows no longer selected.</summary>
    public IReadOnlyList<TableRow> RemovedRows { get; }

    /// <summary>Gets the cells newly selected.</summary>
    public IReadOnlyList<TableCellReference> AddedCells { get; }

    /// <summary>Gets the cells no longer selected.</summary>
    public IReadOnlyList<TableCellReference> RemovedCells { get; }
}
