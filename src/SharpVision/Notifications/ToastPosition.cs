// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

/// <summary>Selects the screen edge and horizontal alignment used to stack a Toast.</summary>
[PublicAPI]
public enum ToastPosition
{
    /// <summary>Stacks downward from the top-left edge.</summary>
    TopLeft,
    /// <summary>Stacks downward from the top edge with each Toast centered.</summary>
    TopCenter,
    /// <summary>Stacks downward from the top-right edge.</summary>
    TopRight,
    /// <summary>Stacks upward from the bottom-left edge.</summary>
    BottomLeft,
    /// <summary>Stacks upward from the bottom edge with each Toast centered.</summary>
    BottomCenter,
    /// <summary>Stacks upward from the bottom-right edge.</summary>
    BottomRight
}
