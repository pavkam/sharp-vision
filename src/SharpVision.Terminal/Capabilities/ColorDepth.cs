// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies the terminal's safe color fidelity.</summary>
public enum ColorDepth
{
    /// <summary>Do not rely on color output.</summary>
    Monochrome,

    /// <summary>Use the basic 16-color palette.</summary>
    Basic16,

    /// <summary>Use the indexed 256-color palette.</summary>
    Indexed256,

    /// <summary>Use 24-bit RGB colors.</summary>
    TrueColor,
}
