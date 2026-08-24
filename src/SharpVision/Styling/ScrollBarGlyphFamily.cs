// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Controls.Scrolling;

/// <summary>Defines one <see cref="GlyphFamily"/>'s complete ScrollBar contribution: the chrome
/// and fill treatment paired with the complete button, track, and thumb glyph set.</summary>
[PublicAPI]
public readonly record struct ScrollBarGlyphFamily
{
    /// <summary>Initializes and validates a chrome mode, fill mode, and their paired glyph set.</summary>
    /// <param name="chrome">The compact or full scrollbar chrome.</param>
    /// <param name="fill">The line or block glyph treatment.</param>
    /// <param name="glyphs">The complete button, track, and thumb glyph family.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chrome"/> or <paramref name="fill"/> is undefined.</exception>
    public ScrollBarGlyphFamily(ScrollBarChrome chrome, ScrollBarFill fill, ScrollBarGlyphs glyphs)
    {
        Chrome = chrome;
        Fill = fill;
        Glyphs = glyphs;
    }

    /// <summary>Gets the compact or full scrollbar chrome.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public ScrollBarChrome Chrome
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the line or block glyph treatment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public ScrollBarFill Fill
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete button, track, and thumb glyph family.</summary>
    public ScrollBarGlyphs Glyphs { get; init; }
}
