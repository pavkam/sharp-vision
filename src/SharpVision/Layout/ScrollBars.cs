// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Selects the scrollable axes owned by one overflow host.</summary>
[Flags]
public enum ScrollBars
{
    /// <summary>Disables scrolling on both axes.</summary>
    None = 0,

    /// <summary>Enables left-to-right scrolling.</summary>
    Horizontal = 1,

    /// <summary>Enables top-to-bottom scrolling.</summary>
    Vertical = 2,

    /// <summary>Enables both axes.</summary>
    Both = Horizontal | Vertical,
}
