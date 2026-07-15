// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ProbeContainer(int capacity = int.MaxValue) : base(capacity)
    {
    }

    /// <inheritdoc/>
    protected override bool OwnsPointerState => CanFocus;

    /// <summary>Gets or sets whether rendering clips owned descendants.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipChildren;

    /// <summary>Measures one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="constraint">The child constraint.</param>
    /// <returns>The committed child desired size.</returns>
    internal Size MeasureOwned(Control child, Constraint constraint) =>
        MeasureChild(child, constraint);

    /// <summary>Arranges one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="slot">The assigned outer slot.</param>
    /// <param name="resolvedAxes">Axes already resolved by this parent.</param>
    internal void ArrangeOwned(Control child, Rect slot, ResolvedAxes resolvedAxes) =>
        ArrangeChild(child, slot, resolvedAxes);
}
