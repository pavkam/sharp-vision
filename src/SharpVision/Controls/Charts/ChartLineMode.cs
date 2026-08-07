// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Selects how line-series segments rasterize into cells.</summary>
[PublicAPI]
public enum ChartLineMode
{
    /// <summary>Segments rasterize through the canvas half-cell quadrant primitive, doubling the
    /// effective resolution and merging crossing lines into connected glyphs.</summary>
    Quadrant,

    /// <summary>Segments rasterize as whole cells drawn with the style's own line glyph, exactly
    /// as authored - the mode a theme selects when it replaces the line glyph.</summary>
    Glyph
}
