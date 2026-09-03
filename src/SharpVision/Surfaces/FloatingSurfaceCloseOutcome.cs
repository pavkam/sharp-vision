// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

/// <summary>Describes the committed result of one shared floating-surface close request.</summary>
internal enum FloatingSurfaceCloseOutcome
{
    /// <summary>No presented lifetime accepted the request.</summary>
    Ignored,

    /// <summary>A request observer or concrete family retained the presentation.</summary>
    Vetoed,

    /// <summary>Closure was accepted and structural cleanup waits for visual disappearance.</summary>
    Deferred,

    /// <summary>Closure and structural cleanup completed synchronously.</summary>
    Completed
}
