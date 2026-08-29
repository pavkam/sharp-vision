// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies the semantic depth treatment of a one-cell border.</summary>
public enum BorderRelief
{
    /// <summary>Paints every enabled edge with the border's uniform foreground.</summary>
    Flat,
    /// <summary>Paints highlight on top/left and shade on right/bottom.</summary>
    Raised,
    /// <summary>Paints shade on top/left and highlight on right/bottom.</summary>
    Sunken
}
