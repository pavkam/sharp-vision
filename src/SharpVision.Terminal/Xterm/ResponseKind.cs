// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Identifies a typed terminal query response.</summary>
[PublicAPI]
public enum ResponseKind
{
    /// <summary>No recognized response.</summary>
    None = 0,

    /// <summary>Primary device attributes (DA1).</summary>
    PrimaryAttributes = 1,

    /// <summary>Secondary device attributes (DA2).</summary>
    SecondaryAttributes = 2,

    /// <summary>A one-based cursor position report.</summary>
    CursorPosition = 3,

    /// <summary>A DEC private mode report (DECRPM).</summary>
    PrivateMode = 4,

    /// <summary>An OSC 10 default foreground color reply.</summary>
    ForegroundColor = 5,

    /// <summary>An OSC 11 default background color reply.</summary>
    BackgroundColor = 6,

    /// <summary>Current Kitty progressive keyboard flags.</summary>
    Keyboard = 7,

    /// <summary>One indexed OSC 4 palette color.</summary>
    PaletteColor = 8,

    /// <summary>The text-area size in pixels.</summary>
    WindowPixels = 9,

    /// <summary>The character-cell size in pixels.</summary>
    CellPixels = 10,

    /// <summary>The text-area size in cells.</summary>
    WindowCells = 11,

    /// <summary>xterm modifyOtherKeys state.</summary>
    ModifyOtherKeys = 12
}
