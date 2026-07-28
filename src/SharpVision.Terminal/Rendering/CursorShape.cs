// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Identifies the semantic terminal cursor shape requested by a frame.</summary>
[PublicAPI]
public enum CursorShape
{
    /// <summary>Uses the terminal's ordinary block cursor.</summary>
    Block,

    /// <summary>Uses an underline cursor when the active description supports cursor shape.</summary>
    Underline,

    /// <summary>Uses a vertical bar cursor when the active description supports cursor shape.</summary>
    Bar
}
