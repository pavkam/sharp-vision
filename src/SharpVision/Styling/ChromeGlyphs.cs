// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines code-owned glyphs for borders, block shadows, and window chrome.</summary>
internal readonly record struct ChromeGlyphs
{
    /// <summary>Initializes one complete border family plus shadow and window-interaction glyphs.</summary>
    /// <param name="topLeft">The top-left border corner.</param>
    /// <param name="top">The top border edge.</param>
    /// <param name="topRight">The top-right border corner.</param>
    /// <param name="right">The right border edge.</param>
    /// <param name="bottomRight">The bottom-right border corner.</param>
    /// <param name="bottom">The bottom border edge.</param>
    /// <param name="bottomLeft">The bottom-left border corner.</param>
    /// <param name="left">The left border edge.</param>
    /// <param name="shadow">The block-shadow glyph.</param>
    /// <param name="fractionalUpper">The upper-half fractional shadow edge.</param>
    /// <param name="fractionalLower">The lower-half fractional shadow edge.</param>
    /// <param name="fractionalFull">The full fractional shadow body and terminal corner.</param>
    /// <param name="windowCloseLeft">The opening window-close bracket.</param>
    /// <param name="windowClose">The window-close glyph.</param>
    /// <param name="windowCloseRight">The closing window-close bracket.</param>
    /// <param name="windowResize">The bottom-right window-resize grip.</param>
    public ChromeGlyphs(
        ControlGlyph topLeft,
        ControlGlyph top,
        ControlGlyph topRight,
        ControlGlyph right,
        ControlGlyph bottomRight,
        ControlGlyph bottom,
        ControlGlyph bottomLeft,
        ControlGlyph left,
        ControlGlyph shadow,
        ControlGlyph fractionalUpper,
        ControlGlyph fractionalLower,
        ControlGlyph fractionalFull,
        ControlGlyph windowCloseLeft,
        ControlGlyph windowClose,
        ControlGlyph windowCloseRight,
        ControlGlyph windowResize)
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
        FractionalUpper = fractionalUpper;
        FractionalLower = fractionalLower;
        FractionalFull = fractionalFull;
        WindowCloseLeft = windowCloseLeft;
        WindowClose = windowClose;
        WindowCloseRight = windowCloseRight;
        WindowResize = windowResize;
    }

    /// <summary>Gets the top-left border corner.</summary>
    public ControlGlyph TopLeft { get; }

    /// <summary>Gets the top border edge.</summary>
    public ControlGlyph Top { get; }

    /// <summary>Gets the top-right border corner.</summary>
    public ControlGlyph TopRight { get; }

    /// <summary>Gets the right border edge.</summary>
    public ControlGlyph Right { get; }

    /// <summary>Gets the bottom-right border corner.</summary>
    public ControlGlyph BottomRight { get; }

    /// <summary>Gets the bottom border edge.</summary>
    public ControlGlyph Bottom { get; }

    /// <summary>Gets the bottom-left border corner.</summary>
    public ControlGlyph BottomLeft { get; }

    /// <summary>Gets the left border edge.</summary>
    public ControlGlyph Left { get; }

    /// <summary>Gets the block-shadow glyph.</summary>
    public ControlGlyph Shadow { get; }

    /// <summary>Gets the upper-half fractional shadow edge.</summary>
    public ControlGlyph FractionalUpper { get; }

    /// <summary>Gets the lower-half fractional shadow edge.</summary>
    public ControlGlyph FractionalLower { get; }

    /// <summary>Gets the full fractional shadow body and terminal corner.</summary>
    public ControlGlyph FractionalFull { get; }

    /// <summary>Gets the opening window-close bracket.</summary>
    public ControlGlyph WindowCloseLeft { get; }

    /// <summary>Gets the window-close glyph.</summary>
    public ControlGlyph WindowClose { get; }

    /// <summary>Gets the closing window-close bracket.</summary>
    public ControlGlyph WindowCloseRight { get; }

    /// <summary>Gets the bottom-right window-resize grip.</summary>
    public ControlGlyph WindowResize { get; }
}
