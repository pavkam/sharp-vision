// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Draws a non-interactive horizontal or vertical divider line.</summary>
[PublicAPI]
public sealed class Separator: Control<SeparatorStyle>
{
    /// <summary>Initializes a non-focusable horizontal separator.</summary>
    public Separator() : base(SeparatorStyle.Definition)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        HitTestVisible = false;
    }

    /// <summary>Gets or sets the separator orientation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ActualStyle;
        var s = ResolvedStyle;
        if (Orientation == Orientation.Horizontal)
        {
            var glyph = style.HorizontalGlyph.Resolve(new Rune('-'), CellPolicy.AmbiguousWidth);
            canvas.DrawHorizontalLine(Bounds, glyph, s);
        }
        else
        {
            var glyph = style.VerticalGlyph.Resolve(new Rune('|'), CellPolicy.AmbiguousWidth);
            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                canvas.DrawRune(glyph, new Point(Bounds.X, y), s, BackgroundMode.Transparent);
            }
        }
    }

}
