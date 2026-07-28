// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Describes the committed ordering of a sortable Table column.</summary>
public enum TableSortDirection
{
    /// <summary>No column sort is active.</summary>
    None,

    /// <summary>Rows are ordered from the smallest key to the largest key.</summary>
    Ascending,

    /// <summary>Rows are ordered from the largest key to the smallest key.</summary>
    Descending
}
