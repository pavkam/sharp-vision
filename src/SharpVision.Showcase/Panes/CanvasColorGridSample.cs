// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Demonstrates the indexed terminal color palette in a compact grid.</summary>
internal sealed class CanvasColorGridSample: Control
{
    /// <summary>Initializes the color grid specimen.</summary>
    internal CanvasColorGridSample()
    {
        Width = Length.Cells(40);
        Height = Length.Cells(14);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(40, 14);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 38 || Bounds.Height < 13)
        {
            return;
        }

        _ = canvas.Draw("Standard 16 colors".AsSpan(), new Point(Bounds.X + 2, Bounds.Y + 1), style);

        for (var index = 0; index < 16; index++)
        {
            var x = Bounds.X + 2 + (index * 2);
            var color = Color.Indexed(index);
            canvas.Fill(new Rect(x, Bounds.Y + 2, 2, 1), new Rune('█'), new CellStyle(color, Color.Default));
        }

        _ = canvas.Draw("216-color cube (6×6 sample)".AsSpan(), new Point(Bounds.X + 2, Bounds.Y + 4), style);

        for (var row = 0; row < 6; row++)
        {
            for (var col = 0; col < 6; col++)
            {
                var index = 16 + (row * 36) + (col * 6);
                var x = Bounds.X + 2 + (col * 2);
                var y = Bounds.Y + 5 + row;
                var color = Color.Indexed(index);
                canvas.Fill(new Rect(x, y, 2, 1), new Rune('█'), new CellStyle(color, Color.Default));
            }
        }

        _ = canvas.Draw("Grayscale ramp".AsSpan(), new Point(Bounds.X + 16, Bounds.Y + 4), style);

        for (var index = 0; index < 24; index++)
        {
            var x = Bounds.X + 16 + index;
            var color = Color.Indexed(232 + index);
            canvas.DrawRune(new Rune('█'), new Point(x, Bounds.Y + 5), new CellStyle(color, Color.Default), BackgroundMode.Transparent);
        }

        _ = canvas.Draw("Color.Indexed(n)".AsSpan(), new Point(Bounds.X + 16, Bounds.Y + 7), style);
    }
}
