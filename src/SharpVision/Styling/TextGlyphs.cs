// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines glyphs used for framework-authored text presentation.</summary>
[PublicAPI]
public readonly record struct TextGlyphs
{
    /// <summary>Initializes framework text glyphs.</summary>
    /// <param name="ellipsis">The truncation ellipsis.</param>
    public TextGlyphs(ControlGlyph ellipsis) => Ellipsis = ellipsis;

    /// <summary>Gets the truncation ellipsis.</summary>
    public ControlGlyph Ellipsis { get; }
}
