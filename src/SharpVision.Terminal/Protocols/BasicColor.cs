// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies one classic ANSI or aixterm 16-color palette entry.</summary>
public enum BasicColor
{
    /// <summary>Uses normal black, palette index 0.</summary>
    Black,

    /// <summary>Uses normal red, palette index 1.</summary>
    Red,

    /// <summary>Uses normal green, palette index 2.</summary>
    Green,

    /// <summary>Uses normal yellow, palette index 3.</summary>
    Yellow,

    /// <summary>Uses normal blue, palette index 4.</summary>
    Blue,

    /// <summary>Uses normal magenta, palette index 5.</summary>
    Magenta,

    /// <summary>Uses normal cyan, palette index 6.</summary>
    Cyan,

    /// <summary>Uses normal white, palette index 7.</summary>
    White,

    /// <summary>Uses bright black, palette index 8.</summary>
    BrightBlack,

    /// <summary>Uses bright red, palette index 9.</summary>
    BrightRed,

    /// <summary>Uses bright green, palette index 10.</summary>
    BrightGreen,

    /// <summary>Uses bright yellow, palette index 11.</summary>
    BrightYellow,

    /// <summary>Uses bright blue, palette index 12.</summary>
    BrightBlue,

    /// <summary>Uses bright magenta, palette index 13.</summary>
    BrightMagenta,

    /// <summary>Uses bright cyan, palette index 14.</summary>
    BrightCyan,

    /// <summary>Uses bright white, palette index 15.</summary>
    BrightWhite,
}
