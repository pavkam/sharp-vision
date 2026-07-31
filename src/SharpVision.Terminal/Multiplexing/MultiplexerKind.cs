// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

/// <summary>Identifies one terminal multiplexer passthrough layer.</summary>
[PublicAPI]
public enum MultiplexerKind
{
    /// <summary>No multiplexer is present.</summary>
    None,

    /// <summary>The nearest layer uses tmux DCS passthrough.</summary>
    Tmux,

    /// <summary>The nearest layer uses GNU screen DCS passthrough.</summary>
    Screen
}
