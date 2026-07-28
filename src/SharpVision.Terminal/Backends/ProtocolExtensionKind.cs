// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Identifies one protocol-extension family understood by a terminal backend.</summary>
internal enum ProtocolExtensionKind
{
    /// <summary>Represents the standard VT protocol foundation.</summary>
    Vt,

    /// <summary>Represents xterm protocol extensions.</summary>
    Xterm,

    /// <summary>Represents Kitty protocol extensions.</summary>
    Kitty,

    /// <summary>Represents iTerm2 protocol extensions.</summary>
    Iterm2
}
