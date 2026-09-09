// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

/// <summary>Identifies which pointer-driven gesture a <see cref="Window"/>'s single shared drag
/// capability is currently resolving.</summary>
internal enum WindowGesture
{
    /// <summary>No title-bar move or corner resize is in progress.</summary>
    None,

    /// <summary>A title-bar press is moving the window.</summary>
    Move,

    /// <summary>A bottom-right corner press is resizing the window.</summary>
    Resize
}
