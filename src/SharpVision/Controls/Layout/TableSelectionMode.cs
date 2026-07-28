// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Defines the selection granularity and multiplicity used by a <see cref="Table"/>.</summary>
public enum TableSelectionMode
{
    /// <summary>Disables selection while retaining an active row and cell for navigation.</summary>
    None,

    /// <summary>Selects one complete row at a time.</summary>
    Row,

    /// <summary>Selects zero or more complete rows.</summary>
    MultipleRows,

    /// <summary>Selects one cell at a time.</summary>
    Cell,

    /// <summary>Selects zero or more cells.</summary>
    MultipleCells
}
