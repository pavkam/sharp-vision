// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies one concrete terminal color representation.</summary>
public enum ColorKind
{
    /// <summary>Use the terminal default color.</summary>
    Default,

    /// <summary>Use one of the terminal's 256 indexed colors.</summary>
    Indexed,

    /// <summary>Use an explicit 24-bit RGB color.</summary>
    Rgb,
}
