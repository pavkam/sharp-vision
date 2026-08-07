// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies the bounded Kitty graphics actions implemented by SharpVision.</summary>
[PublicAPI]
public enum KittyGraphicsAction
{
    /// <summary>Queries support without storing image data.</summary>
    Query,

    /// <summary>Transmits image data without displaying it.</summary>
    Transmit,

    /// <summary>Places an already transmitted image.</summary>
    Place,

    /// <summary>Deletes one retained image and its placements.</summary>
    Delete
}
