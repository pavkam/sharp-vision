// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Draws a non-interactive horizontal or vertical divider line.</summary>
[PublicAPI]
public sealed class Separator: ControlBase, IStyled<SeparatorStyle>
{
    private readonly StyleSlot<SeparatorStyle> _style;

    /// <summary>Initializes a non-focusable horizontal separator.</summary>
    public Separator()
    {
        _style = InitializeStyle(SeparatorStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public SeparatorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public SeparatorStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the separator orientation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);

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
            var visible = canvas.Bounds.Intersect(Bounds);
            for (var y = visible.Y; y < visible.Bottom; y++)
            {
                canvas.DrawRune(glyph, new Point(Bounds.X, y), s, BackgroundMode.Transparent);
            }
        }
    }

}
