// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Defines the supported Unicode box-drawing stroke weights.</summary>
[PublicAPI]
public enum LineWeight
{
    /// <summary>Uses light one-cell strokes.</summary>
    Light,

    /// <summary>Uses heavy one-cell strokes.</summary>
    Heavy,

    /// <summary>Uses double one-cell strokes.</summary>
    Paired
}
