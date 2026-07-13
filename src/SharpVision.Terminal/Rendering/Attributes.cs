// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Identifies semantic text attributes stored in terminal cells.
/// </summary>
[Flags]
public enum Attributes
{
    /// <summary>No text attribute is active.</summary>
    None = 0,

    /// <summary>Bold or increased intensity.</summary>
    Bold = 1 << 0,

    /// <summary>Dim or decreased intensity.</summary>
    Dim = 1 << 1,

    /// <summary>Italic presentation.</summary>
    Italic = 1 << 2,

    /// <summary>Underlined presentation.</summary>
    Underline = 1 << 3,

    /// <summary>Blinking presentation.</summary>
    Blink = 1 << 4,

    /// <summary>Reversed foreground and background.</summary>
    Reverse = 1 << 5,

    /// <summary>Concealed presentation.</summary>
    Hidden = 1 << 6,

    /// <summary>Struck-through presentation.</summary>
    Strike = 1 << 7,

    /// <summary>Rapidly blinking presentation.</summary>
    RapidBlink = 1 << 8,

    /// <summary>Overlined presentation.</summary>
    Overline = 1 << 9,
}
