// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Identifies the renderer path selected for terminal image placements.</summary>
[PublicAPI]
public enum TerminalGraphicsBackend
{
    /// <summary>No terminal image protocol is authorized; ordinary cell fallback is used.</summary>
    CellFallback,

    /// <summary>The retained Kitty graphics backend is selected.</summary>
    Kitty,

    /// <summary>The non-retained sixel and iTerm2 selection backend is selected.</summary>
    NonRetained
}
