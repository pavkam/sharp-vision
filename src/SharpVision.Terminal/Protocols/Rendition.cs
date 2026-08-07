// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies a Select Graphic Rendition attribute.</summary>
[PublicAPI]
public enum Rendition
{
    /// <summary>Reset every rendition attribute and color.</summary>
    Reset = 0,

    /// <summary>Request bold or increased intensity.</summary>
    Bold = 1,

    /// <summary>Request faint or decreased intensity.</summary>
    Dim = 2,

    /// <summary>Request italic text.</summary>
    Italic = 3,

    /// <summary>Request underlined text.</summary>
    Underline = 4,

    /// <summary>Request slow blink.</summary>
    SlowBlink = 5,

    /// <summary>Request rapid blink.</summary>
    RapidBlink = 6,

    /// <summary>Exchange foreground and background roles.</summary>
    Reverse = 7,

    /// <summary>Request concealed text.</summary>
    Hidden = 8,

    /// <summary>Request struck-through text.</summary>
    Strike = 9,

    /// <summary>Disable bold and dim intensity.</summary>
    NormalIntensity = 22,

    /// <summary>Disable italic text.</summary>
    NotItalic = 23,

    /// <summary>Disable underline.</summary>
    NotUnderline = 24,

    /// <summary>Disable blink.</summary>
    NotBlink = 25,

    /// <summary>Disable reverse video.</summary>
    NotReverse = 27,

    /// <summary>Disable concealment.</summary>
    NotHidden = 28,

    /// <summary>Disable strike-through.</summary>
    NotStrike = 29,

    /// <summary>Request overlined text.</summary>
    Overline = 53,

    /// <summary>Disable overline.</summary>
    NotOverline = 55
}
