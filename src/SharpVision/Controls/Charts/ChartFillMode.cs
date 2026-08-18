// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Selects how bar and area extents rasterize into cells.</summary>
[PublicAPI]
public enum ChartFillMode
{
    /// <summary>Extents rasterize through the canvas fractional-block primitives, so a bar or
    /// fill ends on an eighth-cell boundary instead of rounding to a whole cell.</summary>
    Fractional,

    /// <summary>Extents round to whole cells drawn with the style's own fill glyph, exactly as
    /// authored - the mode a theme selects when it replaces the fill glyph.</summary>
    Glyph
}
