// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines how intrinsic control shadow chrome changes cells in its visual overflow footprint.</summary>
[PublicAPI]
public enum ShadowMode
{
    /// <summary>Preserves underlying graphemes and replaces their semantic style.</summary>
    Composite,

    /// <summary>Replaces footprint cells with the configured block or shade glyph.</summary>
    BlockGlyph,

    /// <summary>Uses code-owned half-block and full-block glyphs for fractional vertical depth.</summary>
    FractionalBlock
}
