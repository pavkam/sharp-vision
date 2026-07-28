// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Defines how an image source is fitted into its terminal-cell destination.</summary>
[PublicAPI]
public enum PlacementMode
{
    /// <summary>Preserves aspect ratio and fits the complete source inside the destination.</summary>
    Contain = 0,

    /// <summary>Preserves aspect ratio and fills the destination by cropping excess source area.</summary>
    Cover,

    /// <summary>Scales each source axis independently to fill the destination.</summary>
    Stretch
}
