// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies a typed terminal query response.</summary>
public enum ResponseKind
{
    /// <summary>No recognized response.</summary>
    None,

    /// <summary>Primary device attributes (DA1).</summary>
    PrimaryAttributes,

    /// <summary>Secondary device attributes (DA2).</summary>
    SecondaryAttributes,

    /// <summary>A one-based cursor position report.</summary>
    CursorPosition,

    /// <summary>A DEC private mode report (DECRPM).</summary>
    PrivateMode,

    /// <summary>An OSC 10 default foreground color reply.</summary>
    ForegroundColor,

    /// <summary>An OSC 11 default background color reply.</summary>
    BackgroundColor,

    /// <summary>Current Kitty progressive keyboard flags.</summary>
    Keyboard,
}
