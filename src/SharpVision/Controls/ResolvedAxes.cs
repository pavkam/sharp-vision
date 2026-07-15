// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies child border-box axes already resolved by a layout owner.</summary>
[Flags]
public enum ResolvedAxes
{
    /// <summary>No axis was resolved by the parent.</summary>
    None = 0,

    /// <summary>The child must use the assigned slot width.</summary>
    Width = 1,

    /// <summary>The child must use the assigned slot height.</summary>
    Height = 2,

    /// <summary>The child must use both assigned slot dimensions.</summary>
    Both = Width | Height,
}
