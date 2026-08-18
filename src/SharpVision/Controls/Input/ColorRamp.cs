// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Renders a pointer-transparent horizontal hue ramp.</summary>
internal sealed class ColorRamp: ControlBase
{
    /// <summary>Initializes a stretching one-cell hue ramp.</summary>
    public ColorRamp()
    {
        Height = Length.Cells(1);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint.Width;
        return new Size(32, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        for (var x = 0; x < bounds.Width; x++)
        {
            var hue = bounds.Width <= 1 ? 0 : x * 359 / (bounds.Width - 1);
            var color = Color.FromHsv(hue, 1, 1);
            canvas.DrawRune(
                new Rune(' '),
                new Point(bounds.X + x, bounds.Y),
                new TerminalStyle(Color.Default, color));
        }
    }
}
