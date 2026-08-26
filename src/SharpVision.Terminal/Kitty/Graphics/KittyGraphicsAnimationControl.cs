// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies the Kitty animation playback control sub-actions carried by the <c>s</c> key.</summary>
[PublicAPI]
public enum KittyGraphicsAnimationControl
{
    /// <summary>Stops the animation.</summary>
    Stop = 1,

    /// <summary>Runs the animation but waits for new frames once the retained frames are exhausted.</summary>
    WaitForNewFrames = 2,

    /// <summary>Runs the animation, looping over the retained frames.</summary>
    Run = 3
}
