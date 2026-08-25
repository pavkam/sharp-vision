// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

/// <summary>Selects the deterministic entrance animation used when a Toast is shown.</summary>
[PublicAPI]
public enum ToastAnimation
{
    /// <summary>Moves the Toast from the presentation host's top edge to its final slot.</summary>
    SlideTop,
    /// <summary>Moves the Toast downward from one Toast-height above its final slot.</summary>
    SlideDown,
    /// <summary>Moves the Toast rightward from one Toast-width left of its final slot.</summary>
    SlideLeft,
    /// <summary>Moves the Toast leftward from one Toast-width right of its final slot.</summary>
    SlideRight,
    /// <summary>Expands the clipped Toast from the center of its final slot.</summary>
    Expand,
    /// <summary>Reveals Toast cells through a stable terminal-safe dissolve.</summary>
    Fade
}
