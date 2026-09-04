// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines the ColorPlane currently-selected-coordinate marker glyph.</summary>
[PublicAPI]
public readonly record struct ColorPlaneGlyphs
{
    /// <summary>Initializes the ColorPlane glyph family.</summary>
    /// <param name="selectedMarker">The currently-selected-coordinate marker.</param>
    public ColorPlaneGlyphs(ControlGlyph selectedMarker) => SelectedMarker = selectedMarker;

    /// <summary>Gets the currently-selected-coordinate marker.</summary>
    public ControlGlyph SelectedMarker { get; }
}
