// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

#pragma warning disable IDE0001 // Keep the terminal drawing alias explicit after retiring layout Canvas.
#pragma warning restore IDE0001

/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ProbeContainer(int capacity = int.MaxValue) : base(capacity)
    {
    }

    /// <summary>Gets or sets test-authored intrinsic border chrome.</summary>
    public new Border Border
    {
        get => base.Border;
        set => base.Border = value;
    }

    /// <summary>Gets or sets test-authored intrinsic shadow chrome.</summary>
    public new Shadow Shadow
    {
        get => base.Shadow;
        set => base.Shadow = value;
    }

    /// <summary>Gets or sets whether rendering clips owned descendants.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <summary>Gets how often rendering requested this container's own visual bounds.</summary>
    internal int VisualBoundsReads { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next OnRenderAdornment pass.</summary>
    internal Action<ProbeContainer>? RenderingAdornment { get; set; }

    /// <inheritdoc/>
    protected override void OnRenderAdornment(TerminalCanvas canvas)
    {
        _ = canvas;
        RenderingAdornment?.Invoke(this);
    }

    /// <summary>Gets the number of OnChildrenChanged invocations.</summary>
    internal int ChildrenChangedCalls { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next OnChildrenChanged pass.</summary>
    internal Action<ProbeContainer>? ChildrenChanging { get; set; }

    /// <inheritdoc/>
    protected override void OnChildrenChanged()
    {
        ChildrenChangedCalls++;
        ChildrenChanging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipChildren;

    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            VisualBoundsReads++;
            return base.VisualBounds;
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => _ = bounds;

    /// <summary>Measures one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="constraint">The child constraint.</param>
    /// <returns>The committed child desired size.</returns>
    internal Size MeasureOwned(ControlBase child, Constraint constraint) =>
        MeasureChild(child, constraint);

    /// <summary>Arranges one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="slot">The assigned outer slot.</param>
    /// <param name="resolvedAxes">Axes already resolved by this parent.</param>
    internal void ArrangeOwned(ControlBase child, Rect slot, ResolvedAxes resolvedAxes) =>
        ArrangeChild(child, slot, resolvedAxes);
}
