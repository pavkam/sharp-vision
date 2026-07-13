// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies the xterm-compatible SGR underline presentation.</summary>
public enum Underline
{
    /// <summary>Disables underlining.</summary>
    None,

    /// <summary>Requests one straight underline.</summary>
    Straight,

    /// <summary>Requests two straight underlines.</summary>
    Paired,

    /// <summary>Requests a curly underline.</summary>
    Curly,

    /// <summary>Requests a dotted underline.</summary>
    Dotted,

    /// <summary>Requests a dashed underline.</summary>
    Dashed,
}
