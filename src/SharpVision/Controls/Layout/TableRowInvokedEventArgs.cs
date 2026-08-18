// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes one Table row activation.</summary>
public sealed class TableRowInvokedEventArgs: EventArgs
{
    /// <summary>Initializes a row activation snapshot.</summary>
    /// <param name="row">The non-null activated row.</param>
    /// <param name="rowIndex">The non-negative current display index.</param>
    /// <param name="cause">The semantic activation source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowIndex"/> is negative or <paramref name="cause"/> is undefined.</exception>
    public TableRowInvokedEventArgs(TableRow row, int rowIndex, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The activation cause is unknown.");

        Row = row;
        RowIndex = rowIndex;
        Cause = cause;
    }

    /// <summary>Gets the activated row.</summary>
    public TableRow Row { get; }

    /// <summary>Gets the row's current zero-based display index.</summary>
    public int RowIndex { get; }

    /// <summary>Gets the semantic source of activation.</summary>
    public ActivationCause Cause { get; }
}
