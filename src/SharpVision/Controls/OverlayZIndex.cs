// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Boxes one weakly attached overlay z-order value.</summary>
/// <remarks>
/// This is intentionally a mutable reference type rather than a readonly value
/// type. <see cref="ConditionalWeakTable{TKey, TValue}"/> requires reference-type
/// values, and <see cref="Overlay.SetZIndex(Control, int)"/> must update the
/// associated value in place while retaining the weak association with its
/// control. The type therefore remains beside the other attached-value storage
/// types in the <c>SharpVision.Controls.Layout</c> namespace.
/// </remarks>
internal sealed class OverlayZIndex
{
    /// <summary>Gets or sets the signed layer order.</summary>
    public int Value { get; set; }
}
