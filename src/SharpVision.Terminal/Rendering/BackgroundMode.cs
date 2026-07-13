// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Controls whether drawing replaces or preserves destination cell backgrounds.</summary>
public enum BackgroundMode
{
    /// <summary>Writes the supplied style background, including an explicit terminal default.</summary>
    Opaque,

    /// <summary>Retains each destination cell background while applying the supplied foreground and attributes.</summary>
    Transparent,
}
