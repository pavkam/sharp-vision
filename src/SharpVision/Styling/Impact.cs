// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies whether one resource change affects render or measurement.</summary>
public enum Impact
{
    /// <summary>Only semantic cell appearance changed.</summary>
    Render,

    /// <summary>Content or box geometry may have changed.</summary>
    Measure,
}
