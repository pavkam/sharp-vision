// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides the shared private measured and clipped render surface for one projection-style
/// <see cref="ScrollableCompositeControlBase"/>: a composite that lays its own content out and paints
/// it through one child surface inside a private scrolling host.</summary>
/// <remarks>
/// The surface owns no projection state and makes no layout or painting decisions of its own:
/// measurement and painting both delegate straight back to the owner through the callbacks supplied
/// at construction, and the surface owns no style slot of its own - the owner's
/// <see cref="ScrollableCompositeControlBase.ResolveProjectionThemeChangeImpact"/> composes the
/// owner's own theme impact into this surface's so a theme swap that changes only the owner's style
/// still invalidates the surface it never directly targets.
/// </remarks>
[PublicAPI]
public sealed class ProjectionSurface: ControlBase
{
    private readonly ScrollableCompositeControlBase _owner;
    private readonly Func<int?, Size> _measure;
    private readonly Action<TerminalCanvas, Rect> _render;

    /// <summary>Initializes a non-focusable render surface delegating measurement and painting to an
    /// owning projection-style composite.</summary>
    /// <param name="owner">The non-null owning composite.</param>
    /// <param name="measure">
    /// The non-null callback that measures the owner's current projection against a measure-time
    /// width constraint, or null when unconstrained.
    /// </param>
    /// <param name="render">
    /// The non-null callback that draws the owner's current projection intersecting the surface's
    /// clipped content bounds.
    /// </param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public ProjectionSurface(
        ScrollableCompositeControlBase owner,
        Func<int?, Size> measure,
        Action<TerminalCanvas, Rect> render)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(render);

        _owner = owner;
        _measure = measure;
        _render = render;
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => _measure(constraint.Width);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) => _render(canvas, ContentBounds);

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        MaximumImpact(
            base.GetThemeChangeImpact(
                previous,
                current,
                previousParentAmbientFace,
                currentParentAmbientFace),
            _owner.ResolveProjectionThemeChangeImpact(
                previous,
                current,
                previousParentAmbientFace,
                currentParentAmbientFace));
}
