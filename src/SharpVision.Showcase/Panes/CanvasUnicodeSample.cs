// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates grapheme-safe semantic drawing and clip-edge repair.</summary>
internal sealed class CanvasUnicodeSample: Control
{
    /// <summary>Initializes the Unicode drawing sample.</summary>
    internal CanvasUnicodeSample()
    {
        Width = Length.Cells(32);
        Height = Length.Cells(5);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(32, 5);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 20 || Bounds.Height < 5)
        {
            return;
        }

        _ = canvas.Draw("é 你好 👩‍💻 🇺🇸".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 1), style);
        _ = canvas.Draw("complete grapheme owners".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 2), style);
        _ = canvas.Draw(
            "👩‍💻".AsSpan(),
            new Point(Bounds.Right - 1, Bounds.Y + 3),
            style,
            Edge.Clip);
        _ = canvas.Draw("wide clip edge →".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 3), style);
    }
}
