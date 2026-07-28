// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies a bounded terminal query response family.</summary>
[PublicAPI]
public enum QueryKind
{
    /// <summary>Primary device attributes.</summary>
    PrimaryAttributes = 0,

    /// <summary>Secondary device attributes.</summary>
    SecondaryAttributes = 1,

    /// <summary>A cursor position report.</summary>
    CursorPosition = 2,

    /// <summary>A DEC private mode report.</summary>
    PrivateMode = 3,

    /// <summary>A default foreground color reply.</summary>
    ForegroundColor = 4,

    /// <summary>A default background color reply.</summary>
    BackgroundColor = 5,

    /// <summary>A correlated Kitty clipboard response.</summary>
    KittyClipboard = 6,

    /// <summary>Current Kitty progressive keyboard flags.</summary>
    Keyboard = 7,

    /// <summary>One indexed palette color reply.</summary>
    PaletteColor = 8,

    /// <summary>The text-area size in pixels.</summary>
    WindowPixels = 9,

    /// <summary>The character-cell size in pixels.</summary>
    CellPixels = 10,

    /// <summary>The text-area size in cells.</summary>
    WindowCells = 11,

    /// <summary>One DECRQSS response correlated by exact status selector.</summary>
    StatusString = 12,

    /// <summary>One XTGETTCAP response correlated by exact capability name.</summary>
    CapabilityString = 13,

    /// <summary>Current xterm modifyOtherKeys state.</summary>
    ModifyOtherKeys = 14,

    /// <summary>A numeric Kitty graphics APC response.</summary>
    KittyGraphics = 15
}
