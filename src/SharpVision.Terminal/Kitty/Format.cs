// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Identifies direct Kitty image source encodings.</summary>
[PublicAPI]
public enum Format
{
    /// <summary>Three bytes per pixel in RGB order, reserved for the official capability query.</summary>
    Rgb = 24,

    /// <summary>Four bytes per pixel in RGBA order.</summary>
    Rgba = 32,

    /// <summary>A complete structurally valid PNG container.</summary>
    Png = 100
}
