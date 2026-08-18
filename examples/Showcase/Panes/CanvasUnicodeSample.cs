// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates grapheme-safe semantic drawing and clip-edge repair.</summary>
internal sealed class CanvasUnicodeSample: CanvasSampleBase
{
    /// <summary>Initializes the Unicode drawing sample.</summary>
    internal CanvasUnicodeSample()
        : base(width: 32, height: 5, minWidth: 20, minHeight: 5, borderStyle: LineStyle.Rounded)
    {
    }

    /// <inheritdoc/>
    protected override void DrawContent(TerminalCanvas canvas, CellStyle style)
    {
        _ = canvas.Draw("é 你好 👩‍💻 🇺🇸".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 1), style);
        _ = canvas.Draw("complete grapheme owners".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 2), style);
        _ = canvas.Draw(
            "👩‍💻".AsSpan(),
            new Point(Bounds.Right - 1, Bounds.Y + 3),
            style);
        _ = canvas.Draw("wide clip edge →".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 3), style);
    }
}
