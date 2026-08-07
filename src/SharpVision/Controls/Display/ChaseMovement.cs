// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Identifies how ChaseIndicator heads traverse their logical track.</summary>
[PublicAPI]
public enum ChaseMovement
{
    /// <summary>Moves one head to the final position and back to the first position.</summary>
    Bounce,

    /// <summary>Moves one head to the final position and restarts at the first position.</summary>
    Wrap,

    /// <summary>Moves mirrored heads from the center to both endpoints and back.</summary>
    Spread
}
