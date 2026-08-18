// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Stores weakly attached Grid origin and span values.</summary>
internal sealed class GridPlacement
{
    /// <summary>Gets or sets the zero-based row.</summary>
    public int Row { get; set; }

    /// <summary>Gets or sets the zero-based column.</summary>
    public int Column { get; set; }

    /// <summary>Gets or sets the positive row span.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Gets or sets the positive column span.</summary>
    public int ColumnSpan { get; set; } = 1;
}
