// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.SyntaxHighlighting;

/// <summary>Provides the private measured and clipped render surface for one CodeView.</summary>
internal sealed class CodeViewContent: ControlBase
{
    private readonly CodeView _owner;

    /// <summary>Initializes a non-focusable render surface for the owning view.</summary>
    /// <param name="owner">The non-null owning view.</param>
    internal CodeViewContent(CodeView owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <summary>Requests the invalidation phase an owning view's own state change requires.</summary>
    /// <param name="impact">The earliest phase to re-run.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">This render surface is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">This render surface is disposed.</exception>
    internal void RequestInvalidate(InvalidationImpact impact) => Invalidate(impact);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => _owner.MeasureAndWrap(constraint.Width);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) => _owner.RenderProjectedContent(canvas, ContentBounds);
}
