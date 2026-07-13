// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Selects whether a control body fill preserves or replaces existing cells.</summary>
public enum FillMode
{
    /// <summary>Preserves existing canvas cells under the control body.</summary>
    Transparent,

    /// <summary>Fills every arranged body cell with the active background.</summary>
    Opaque,
}
