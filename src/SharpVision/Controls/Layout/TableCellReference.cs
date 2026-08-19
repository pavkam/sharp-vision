// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Identifies one cell by its stable row identity and zero-based column index.</summary>
public readonly record struct TableCellReference
{
    /// <summary>Initializes a validated table-cell reference.</summary>
    /// <param name="row">The non-null row owned by a table.</param>
    /// <param name="columnIndex">The non-negative column index.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnIndex"/> is negative.</exception>
    public TableCellReference(TableRow row, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        Row = row;
        ColumnIndex = columnIndex;
    }

    /// <summary>Gets the referenced row.</summary>
    public TableRow Row { get; }

    /// <summary>Gets the zero-based referenced column index.</summary>
    [NonNegativeValue]
    public int ColumnIndex { get; }

    /// <summary>Gets the retained cell control at this reference.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The row no longer contains the column index.</exception>
    public ControlBase Cell => Row.Cells[ColumnIndex];
}
