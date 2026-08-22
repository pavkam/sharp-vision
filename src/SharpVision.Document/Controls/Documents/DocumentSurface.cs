// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Provides the private measured and clipped render surface for one <see cref="Document"/>.</summary>
/// <remarks>
/// The surface exists only so the scrolling host has a child whose desired size is the document's full
/// content extent. It owns no state and makes no decisions: measurement and painting both delegate
/// straight back to the document, which holds the projection.
/// </remarks>
internal sealed class DocumentSurface: ControlBase
{
    private readonly Document _owner;

    /// <summary>Initializes a non-focusable render surface for the owning document.</summary>
    /// <param name="owner">The non-null owning document.</param>
    internal DocumentSurface(Document owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => _owner.MeasureContent(constraint.Width);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        _owner.RenderProjectedContent(canvas, ContentBounds);

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
            _owner.GetProjectedThemeChangeImpact(
                previous,
                current,
                previousParentAmbientFace,
                currentParentAmbientFace));
}
