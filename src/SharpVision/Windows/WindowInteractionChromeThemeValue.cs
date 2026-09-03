// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

/// <summary>Captures immutable Window interaction chrome resolved from one Theme.</summary>
internal readonly record struct WindowInteractionChromeThemeValue
{
    /// <summary>Initializes one complete resolved interaction-chrome value.</summary>
    internal WindowInteractionChromeThemeValue(
        Rune closeGlyph,
        Rune closeLeftBracket,
        Rune closeRightBracket,
        Color closeForeground,
        Color closeActiveForeground,
        Color closePressedForeground,
        Color closeDisabledForeground,
        Rune resizeGripGlyph,
        Color resizeGripForeground,
        Color resizeGripActiveForeground,
        Color resizeGripPressedForeground,
        Color resizeGripDisabledForeground)
    {
        CloseGlyph = closeGlyph;
        CloseLeftBracket = closeLeftBracket;
        CloseRightBracket = closeRightBracket;
        CloseForeground = closeForeground;
        CloseActiveForeground = closeActiveForeground;
        ClosePressedForeground = closePressedForeground;
        CloseDisabledForeground = closeDisabledForeground;
        ResizeGripGlyph = resizeGripGlyph;
        ResizeGripForeground = resizeGripForeground;
        ResizeGripActiveForeground = resizeGripActiveForeground;
        ResizeGripPressedForeground = resizeGripPressedForeground;
        ResizeGripDisabledForeground = resizeGripDisabledForeground;
    }

    /// <summary>Gets the close mark.</summary>
    internal Rune CloseGlyph { get; }

    /// <summary>Gets the glyph immediately left of the close mark.</summary>
    internal Rune CloseLeftBracket { get; }

    /// <summary>Gets the glyph immediately right of the close mark.</summary>
    internal Rune CloseRightBracket { get; }

    /// <summary>Gets the resting close-mark foreground.</summary>
    internal Color CloseForeground { get; }

    /// <summary>Gets the pointer-over close-mark foreground.</summary>
    internal Color CloseActiveForeground { get; }

    /// <summary>Gets the pressed close-mark foreground.</summary>
    internal Color ClosePressedForeground { get; }

    /// <summary>Gets the disabled close-mark foreground.</summary>
    internal Color CloseDisabledForeground { get; }

    /// <summary>Gets the resize-grip glyph.</summary>
    internal Rune ResizeGripGlyph { get; }

    /// <summary>Gets the resting resize-grip foreground.</summary>
    internal Color ResizeGripForeground { get; }

    /// <summary>Gets the pointer-over resize-grip foreground.</summary>
    internal Color ResizeGripActiveForeground { get; }

    /// <summary>Gets the active-gesture resize-grip foreground.</summary>
    internal Color ResizeGripPressedForeground { get; }

    /// <summary>Gets the disabled resize-grip foreground.</summary>
    internal Color ResizeGripDisabledForeground { get; }
}
