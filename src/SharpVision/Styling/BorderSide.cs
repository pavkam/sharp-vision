// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Selects which edges of a control's one-cell border are drawn.</summary>
[Flags]
[PublicAPI]
public enum BorderSide
{
    /// <summary>No border edges are drawn.</summary>
    None = 0,

    /// <summary>The left edge is drawn.</summary>
    Left = 1,

    /// <summary>The top edge is drawn.</summary>
    Top = 2,

    /// <summary>The right edge is drawn.</summary>
    Right = 4,

    /// <summary>The bottom edge is drawn.</summary>
    Bottom = 8,

    /// <summary>All four edges are drawn.</summary>
    All = Left | Top | Right | Bottom
}
