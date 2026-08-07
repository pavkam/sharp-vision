// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes one committed Table sort transition.</summary>
public sealed class TableSortChangedEventArgs: EventArgs
{
    /// <summary>Initializes a validated sort transition snapshot.</summary>
    /// <param name="columnIndex">The committed column index, or -1 when no sort is active.</param>
    /// <param name="direction">The committed direction.</param>
    /// <exception cref="ArgumentException">
    /// The no-column sentinel and <see cref="TableSortDirection.None"/> do not agree.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnIndex"/> is less than -1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is undefined.</exception>
    public TableSortChangedEventArgs(int columnIndex, TableSortDirection direction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, -1);
        direction.ValidateDefined();

        if (columnIndex == -1 != (direction == TableSortDirection.None))
        {
            throw new ArgumentException("The sort direction must agree with the selected column.", nameof(direction));
        }

        ColumnIndex = columnIndex;
        Direction = direction;
    }

    /// <summary>Gets the committed column index, or -1 when sorting is reset.</summary>
    public int ColumnIndex { get; }

    /// <summary>Gets the committed sort direction.</summary>
    public TableSortDirection Direction { get; }
}
