// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Names one optional terminal protocol or extension a profile can report.</summary>
public enum TerminalProtocol
{
    /// <summary>DEC private mode 2026 synchronized output.</summary>
    SynchronizedOutput,

    /// <summary>Focus in/out reporting.</summary>
    FocusReporting,

    /// <summary>Bracketed paste.</summary>
    BracketedPaste,

    /// <summary>SGR pixel-coordinate mouse reporting.</summary>
    PixelMouse,

    /// <summary>SGR cell-coordinate mouse reporting.</summary>
    CellMouse,

    /// <summary>The Kitty keyboard protocol.</summary>
    KittyKeyboard,

    /// <summary>OSC 52 clipboard access.</summary>
    Osc52,

    /// <summary>Kitty OSC 5522 clipboard access.</summary>
    KittyClipboard,

    /// <summary>The Kitty graphics extension.</summary>
    KittyGraphics,

    /// <summary>Sixel raster graphics.</summary>
    Sixel,

    /// <summary>iTerm2 inline images.</summary>
    ItermImages,

    /// <summary>Styled underline variants.</summary>
    StyledUnderlines,

    /// <summary>Independent underline color.</summary>
    UnderlineColor,

    /// <summary>Overline rendition.</summary>
    Overline,
}
