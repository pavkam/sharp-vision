// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Outcome of one game tick.</summary>
public enum TickResult
{
    /// <summary>The snake moved without incident.</summary>
    Moved,

    /// <summary>The snake ate an apple.</summary>
    Ate,

    /// <summary>The snake collided with a wall, obstacle, or itself.</summary>
    Died
}
