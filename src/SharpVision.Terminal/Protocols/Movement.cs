// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies a relative cursor movement direction.</summary>
[PublicAPI]
public enum Movement
{
    /// <summary>Move toward the top of the display.</summary>
    Up,

    /// <summary>Move toward the bottom of the display.</summary>
    Down,

    /// <summary>Move toward increasing columns.</summary>
    Forward,

    /// <summary>Move toward decreasing columns.</summary>
    Back
}
