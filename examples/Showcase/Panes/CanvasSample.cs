// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates semantic line, box, fill, clear, and style primitives.</summary>
internal sealed class CanvasSample: CanvasSampleBase
{
    /// <summary>Initializes the fixed-size drawing-fundamentals sample.</summary>
    internal CanvasSample()
        : base(width: 40, height: 8, minWidth: 38, minHeight: 8, borderStyle: LineStyle.Rounded)
    {
    }

    /// <inheritdoc/>
    protected override void DrawContent(TerminalCanvas canvas, CellStyle style)
    {
        _ = canvas.Draw("Light".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 1), style);
        _ = canvas.Draw("Heavy".AsSpan(), new Point(Bounds.X + 10, Bounds.Y + 1), style);
        _ = canvas.Draw("Paired".AsSpan(), new Point(Bounds.X + 19, Bounds.Y + 1), style);
        _ = canvas.Draw("ASCII".AsSpan(), new Point(Bounds.X + 29, Bounds.Y + 1), style);
        canvas.DrawHorizontalLine(new Point(Bounds.X + 1, Bounds.Y + 2), 6, LineStyle.Light, style);
        canvas.DrawHorizontalLine(new Point(Bounds.X + 10, Bounds.Y + 2), 6, LineStyle.Heavy, style);
        canvas.DrawHorizontalLine(new Point(Bounds.X + 19, Bounds.Y + 2), 6, LineStyle.Paired, style);
        canvas.DrawHorizontalLine(new Point(Bounds.X + 29, Bounds.Y + 2), 6, LineStyle.Ascii, style);
        canvas.DrawBox(new Rect(Bounds.X + 1, Bounds.Y + 4, 12, 3), LineStyle.Light, style);
        canvas.Fill(new Rect(Bounds.X + 15, Bounds.Y + 4, 10, 3), new Rune('.'), style);
        canvas.Clear(new Rect(Bounds.X + 18, Bounds.Y + 5, 4, 1), style);
        _ = canvas.Draw("Fill + clear".AsSpan(), new Point(Bounds.X + 27, Bounds.Y + 5), style);
    }
}
