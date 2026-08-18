// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Names the finite DECRQSS status strings accepted by SharpVision.</summary>
[PublicAPI]
public enum StatusName
{
    /// <summary>No recognized status name.</summary>
    Unknown = 0,

    /// <summary>Select Graphic Rendition (SGR).</summary>
    Rendition = 1,

    /// <summary>Cursor style (DECSCUSR).</summary>
    CursorStyle = 2,

    /// <summary>Top and bottom scrolling margins (DECSTBM).</summary>
    VerticalMargins = 3,

    /// <summary>Left and right scrolling margins (DECSLRM).</summary>
    HorizontalMargins = 4,

    /// <summary>xterm modifyOtherKeys state (XTQMODKEYS).</summary>
    ModifyOtherKeys = 5,

    /// <summary>xterm formatOtherKeys state (XTQFMTKEYS).</summary>
    FormatOtherKeys = 6
}
