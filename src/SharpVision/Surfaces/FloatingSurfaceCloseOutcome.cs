// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

/// <summary>Describes the committed result of one shared floating-surface close request.</summary>
/// <remarks>
/// Returned by the outcome-reporting overloads of the protected close seams on
/// <see cref="FloatingSurfaceBase"/> (<see cref="FloatingSurfaceBase.CloseSurfaceWithOutcome(Action,Action,Action)"/>
/// and its siblings), so an externally defined surface family can distinguish a veto or a deferred
/// fade-out from immediate completion instead of only learning whether the surface is closed.
/// </remarks>
[PublicAPI]
public enum FloatingSurfaceCloseOutcome
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
