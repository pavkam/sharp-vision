// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Provides the private measured and clipped render surface for one JsonView.</summary>
internal sealed class JsonViewContent: ControlBase
{
    private readonly JsonView _owner;

    /// <summary>Initializes a non-focusable render surface for the owning view.</summary>
    /// <param name="owner">The non-null owning view.</param>
    internal JsonViewContent(JsonView owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Focusable = false;
        TabStop = false;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return _owner.MeasureProjectedContent();
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        _owner.RenderProjectedContent(canvas, ContentBounds);
}
