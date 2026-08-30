// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Identifies the canonical terminal-emulator family selected for one session.</summary>
[PublicAPI]
public enum TerminalBackendFamily
{
    /// <summary>Represents the conservative VT-compatible backend.</summary>
    Vt,

    /// <summary>Represents the xterm-compatible backend.</summary>
    Xterm,

    /// <summary>Represents the Kitty terminal backend.</summary>
    Kitty,

    /// <summary>Represents the iTerm2 terminal backend.</summary>
    Iterm2
}
