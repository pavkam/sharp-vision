// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using GraphicsImage = Terminal.Graphics.ImageSource;
using PlacementMode = Terminal.Graphics.PlacementMode;
/// <summary>Draws a deterministic cell underlay followed by one semantic image placement.</summary>
internal sealed class GraphicsProbeControl: ControlBase
{
    private readonly GraphicsImage _source;

    /// <summary>Initializes a probe borrowing one immutable image.</summary>
    /// <param name="source">The non-null immutable image.</param>
    internal GraphicsProbeControl(GraphicsImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (ContentBounds.Width == 0 || ContentBounds.Height == 0)
        {
            return;
        }

        canvas.Fill(ContentBounds, new Rune('.'), ResolvedStyle);
        canvas.DrawImage(_source, ContentBounds, PlacementMode.Stretch);
    }
}
