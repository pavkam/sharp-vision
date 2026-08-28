// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

/// <summary>Captures the immutable Window close chrome resolved from one Theme.</summary>
internal readonly record struct WindowCloseChromeThemeValue
{
    /// <summary>Initializes one complete resolved close-chrome value.</summary>
    internal WindowCloseChromeThemeValue(
        Rune closeGlyph,
        Rune leftBracket,
        Rune rightBracket,
        Color foreground,
        Color activeForeground,
        Color pressedForeground,
        Color disabledForeground)
    {
        CloseGlyph = closeGlyph;
        LeftBracket = leftBracket;
        RightBracket = rightBracket;
        Foreground = foreground;
        ActiveForeground = activeForeground;
        PressedForeground = pressedForeground;
        DisabledForeground = disabledForeground;
    }

    /// <summary>Gets the close mark.</summary>
    internal Rune CloseGlyph { get; }

    /// <summary>Gets the glyph immediately left of the close mark.</summary>
    internal Rune LeftBracket { get; }

    /// <summary>Gets the glyph immediately right of the close mark.</summary>
    internal Rune RightBracket { get; }

    /// <summary>Gets the resting close-mark foreground.</summary>
    internal Color Foreground { get; }

    /// <summary>Gets the pointer-over close-mark foreground.</summary>
    internal Color ActiveForeground { get; }

    /// <summary>Gets the pressed close-mark foreground.</summary>
    internal Color PressedForeground { get; }

    /// <summary>Gets the disabled close-mark foreground.</summary>
    internal Color DisabledForeground { get; }
}
