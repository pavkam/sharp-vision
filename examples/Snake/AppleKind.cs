// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Determines the effect and appearance of a food item.</summary>
public enum AppleKind
{
    /// <summary>Grows the snake by one segment.</summary>
    Normal,

    /// <summary>Grows the snake by three segments and awards bonus points.</summary>
    Golden,

    /// <summary>Shrinks the snake by two segments. Kills if too short.</summary>
    Poison,

    /// <summary>Temporarily increases movement speed.</summary>
    Speed,

    /// <summary>Grants one extra life.</summary>
    Life
}
