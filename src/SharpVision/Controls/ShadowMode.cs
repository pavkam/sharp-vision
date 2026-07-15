// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines how intrinsic shadow chrome changes cells in its visual overflow footprint.</summary>
public enum ShadowMode
{
    /// <summary>Preserves underlying graphemes and replaces their semantic style.</summary>
    Composite,

    /// <summary>Replaces footprint cells with the configured block or shade glyph.</summary>
    BlockGlyph,
}
