// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Identifies the center-to-edge connections owned by one semantic line cell.</summary>
[Flags]
[PublicAPI]
public enum LineConnections
{
    /// <summary>No edge is connected.</summary>
    None = 0,

    /// <summary>The stroke reaches the top edge.</summary>
    Up = 1,

    /// <summary>The stroke reaches the right edge.</summary>
    Right = 2,

    /// <summary>The stroke reaches the bottom edge.</summary>
    Down = 4,

    /// <summary>The stroke reaches the left edge.</summary>
    Left = 8
}
