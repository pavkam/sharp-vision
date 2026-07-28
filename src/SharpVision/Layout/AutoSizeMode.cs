// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Selects whether an auto-sizing container may shrink below its explicit size.</summary>
[PublicAPI]
public enum AutoSizeMode
{
    /// <summary>Fit the container's border box exactly to its content on each auto-sized axis.</summary>
    GrowAndShrink,

    /// <summary>Grow to content but never shrink below the explicit fixed-cell size on that axis.</summary>
    GrowOnly
}
