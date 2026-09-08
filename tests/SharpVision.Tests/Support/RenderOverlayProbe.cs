// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a single-content owner that paints a fixed glyph through the promoted
/// <see cref="ControlBase.RenderOverlay"/> seam, for proving the seam draws after normal-layer
/// descendants render.</summary>
internal sealed class RenderOverlayProbe: ContentControl
{
    /// <summary>Gets or sets the glyph the overlay override draws over its content bounds, or
    /// null to draw nothing.</summary>
    internal char? OverlayGlyph { get; set; }

    /// <inheritdoc/>
    protected internal override void RenderOverlay(TerminalCanvas canvas)
    {
        base.RenderOverlay(canvas);

        if (OverlayGlyph is not { } glyph)
        {
            return;
        }

        Span<char> buffer = [glyph];
        _ = canvas.Draw(buffer, new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle);
    }
}
