// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Identifies logical pointer buttons.</summary>
[Flags]
[PublicAPI]
public enum Buttons
{
    /// <summary>No button is active.</summary>
    None = 0,

    /// <summary>The primary button.</summary>
    Primary = 1 << 0,

    /// <summary>The middle button.</summary>
    Middle = 1 << 1,

    /// <summary>The secondary button.</summary>
    Secondary = 1 << 2,

    /// <summary>The first extended button.</summary>
    Back = 1 << 3,

    /// <summary>The second extended button.</summary>
    Forward = 1 << 4
}
