// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Identifies one physical edge of a containing rectangle.</summary>
[PublicAPI]
public enum DockSide
{
    /// <summary>Consumes the left edge.</summary>
    Left,

    /// <summary>Consumes the top edge.</summary>
    Top,

    /// <summary>Consumes the right edge.</summary>
    Right,

    /// <summary>Consumes the bottom edge.</summary>
    Bottom
}
