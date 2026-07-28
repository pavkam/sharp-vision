// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines how an image source fits its arranged terminal-cell destination.</summary>
[PublicAPI]
public enum ImageStretch
{
    /// <summary>Preserves aspect ratio while fitting the complete source inside the destination.</summary>
    Contain,

    /// <summary>Preserves aspect ratio while covering the destination and cropping excess source pixels.</summary>
    Cover,

    /// <summary>Scales both axes independently to fill the complete destination.</summary>
    Stretch
}
