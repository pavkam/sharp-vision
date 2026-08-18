// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Identifies one terminal-emulator backend family.</summary>
internal enum TerminalBackendKind
{
    /// <summary>Represents the conservative VT baseline.</summary>
    Vt,

    /// <summary>Represents the xterm-compatible family.</summary>
    Xterm,

    /// <summary>Represents the Kitty terminal family.</summary>
    Kitty,

    /// <summary>Represents the iTerm2 terminal family.</summary>
    Iterm2,
}
