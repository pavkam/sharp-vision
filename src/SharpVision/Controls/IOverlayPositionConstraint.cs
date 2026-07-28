// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Constrains one specialized child's resolved Overlay slot without changing its requested offsets.</summary>
internal interface IOverlayPositionConstraint
{
    /// <summary>Returns the final slot to arrange inside the Overlay content bounds.</summary>
    /// <param name="slot">The candidate outer slot resolved from Overlay offsets and size.</param>
    /// <param name="contentBounds">The Overlay content bounds in absolute cells.</param>
    /// <returns>The constrained outer slot.</returns>
    public Rect ConstrainOverlaySlot(Rect slot, Rect contentBounds);
}
