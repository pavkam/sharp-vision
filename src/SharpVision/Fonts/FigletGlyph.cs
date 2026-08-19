// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Owns one normalized fixed-height FIGfont glyph.</summary>
internal sealed class FigletGlyph
{
    /// <summary>Initializes one internally validated glyph.</summary>
    /// <param name="rows">The owned equally wide rows.</param>
    public FigletGlyph(string[] rows)
    {
        Rows = rows;
        Width = rows.Length == 0 ? 0 : rows[0].Length;
    }

    /// <summary>Gets the owned equally wide rows.</summary>
    public string[] Rows { get; }

    /// <summary>Gets the normalized UTF-16 row width.</summary>
    [NonNegativeValue]
    public int Width { get; }
}
