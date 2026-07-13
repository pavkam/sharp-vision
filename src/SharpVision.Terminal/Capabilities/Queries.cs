// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Contains nullable results from bounded capability queries.</summary>
public sealed record Queries
{
    /// <summary>Gets a synchronized-output query result.</summary>
    public bool? SynchronizedOutput { get; init; }

    /// <summary>Gets a focus-reporting query result.</summary>
    public bool? FocusReporting { get; init; }

    /// <summary>Gets a bracketed-paste query result.</summary>
    public bool? BracketedPaste { get; init; }

    /// <summary>Gets a pixel-mouse query result.</summary>
    public bool? PixelMouse { get; init; }

    /// <summary>Gets a cell-mouse query result.</summary>
    public bool? CellMouse { get; init; }

    /// <summary>Gets a Kitty keyboard query result.</summary>
    public bool? KittyKeyboard { get; init; }

    /// <summary>Gets an OSC 52 query result.</summary>
    public bool? Osc52 { get; init; }

    /// <summary>Gets a Kitty clipboard query result.</summary>
    public bool? KittyClipboard { get; init; }

    /// <summary>Gets a Kitty graphics query result.</summary>
    public bool? KittyGraphics { get; init; }

    /// <summary>Gets a sixel query result.</summary>
    public bool? Sixel { get; init; }

    /// <summary>Gets an iTerm2 image query result.</summary>
    public bool? ItermImages { get; init; }

    /// <summary>Gets a styled-underline query result.</summary>
    public bool? StyledUnderlines { get; init; }

    /// <summary>Gets an underline-color query result.</summary>
    public bool? UnderlineColor { get; init; }

    /// <summary>Gets an overline query result.</summary>
    public bool? Overline { get; init; }
}
