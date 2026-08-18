// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Names the finite XTGETTCAP values SharpVision may request.</summary>
[PublicAPI]
public enum CapabilityName
{
    /// <summary>Number of colors (Co/colors).</summary>
    Colors = 0,

    /// <summary>Terminal description name (TN/name).</summary>
    TerminalName = 1,

    /// <summary>ncurses direct-color precision (RGB).</summary>
    DirectColor = 2,

    /// <summary>Backspace key string (kbs).</summary>
    Backspace = 3,

    /// <summary>Enter key string (kent).</summary>
    Enter = 4,

    /// <summary>Cursor-up key string (kcuu1).</summary>
    Up = 5,

    /// <summary>Cursor-down key string (kcud1).</summary>
    Down = 6,

    /// <summary>Cursor-left key string (kcub1).</summary>
    Left = 7,

    /// <summary>Cursor-right key string (kcuf1).</summary>
    Right = 8,

    /// <summary>Home key string (khome).</summary>
    Home = 9,

    /// <summary>End key string (kend).</summary>
    End = 10
}
