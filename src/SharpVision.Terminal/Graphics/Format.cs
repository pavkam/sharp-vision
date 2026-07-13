// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Identifies an owned terminal image source representation.</summary>
public enum Format
{
    /// <summary>Four sRGB bytes per pixel in red, green, blue, alpha order.</summary>
    Rgba,

    /// <summary>Structurally validated encoded Portable Network Graphics bytes.</summary>
    Png,
}
