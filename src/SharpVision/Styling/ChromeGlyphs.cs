// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines theme glyphs for borders, block shadows, and window chrome.</summary>
public readonly record struct ChromeGlyphs
{
    /// <summary>Initializes one complete border family plus shadow and close glyphs.</summary>
    /// <param name="topLeft">The top-left border corner.</param>
    /// <param name="top">The top border edge.</param>
    /// <param name="topRight">The top-right border corner.</param>
    /// <param name="right">The right border edge.</param>
    /// <param name="bottomRight">The bottom-right border corner.</param>
    /// <param name="bottom">The bottom border edge.</param>
    /// <param name="bottomLeft">The bottom-left border corner.</param>
    /// <param name="left">The left border edge.</param>
    /// <param name="shadow">The block-shadow glyph.</param>
    /// <param name="windowClose">The window-close glyph.</param>
    public ChromeGlyphs(
        ThemedGlyph topLeft,
        ThemedGlyph top,
        ThemedGlyph topRight,
        ThemedGlyph right,
        ThemedGlyph bottomRight,
        ThemedGlyph bottom,
        ThemedGlyph bottomLeft,
        ThemedGlyph left,
        ThemedGlyph shadow,
        ThemedGlyph windowClose)
    {
        TopLeft = topLeft;
        Top = top;
        TopRight = topRight;
        Right = right;
        BottomRight = bottomRight;
        Bottom = bottom;
        BottomLeft = bottomLeft;
        Left = left;
        Shadow = shadow;
        WindowClose = windowClose;
    }

    /// <summary>Gets the top-left border corner.</summary>
    public ThemedGlyph TopLeft { get; }
    /// <summary>Gets the top border edge.</summary>
    public ThemedGlyph Top { get; }
    /// <summary>Gets the top-right border corner.</summary>
    public ThemedGlyph TopRight { get; }
    /// <summary>Gets the right border edge.</summary>
    public ThemedGlyph Right { get; }
    /// <summary>Gets the bottom-right border corner.</summary>
    public ThemedGlyph BottomRight { get; }
    /// <summary>Gets the bottom border edge.</summary>
    public ThemedGlyph Bottom { get; }
    /// <summary>Gets the bottom-left border corner.</summary>
    public ThemedGlyph BottomLeft { get; }
    /// <summary>Gets the left border edge.</summary>
    public ThemedGlyph Left { get; }
    /// <summary>Gets the block-shadow glyph.</summary>
    public ThemedGlyph Shadow { get; }
    /// <summary>Gets the window-close glyph.</summary>
    public ThemedGlyph WindowClose { get; }
}
