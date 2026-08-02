// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Draws a non-interactive horizontal or vertical divider line.</summary>
[PublicAPI]
public sealed class Separator: Control
{
    private Rune? _horizontalGlyph;
    private Rune? _verticalGlyph;

    /// <summary>Initializes a non-focusable horizontal separator.</summary>
    public Separator()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the separator orientation.</summary>
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

    /// <summary>Gets or sets the local horizontal separator glyph.</summary>
    public Rune HorizontalGlyph
    {
        get => _horizontalGlyph ?? ControlGlyphs.Separators.Horizontal.Value;
        set => SetOptionalGlyph(ref _horizontalGlyph, value, nameof(HorizontalGlyph));
    }

    /// <summary>Gets or sets the local vertical separator glyph.</summary>
    public Rune VerticalGlyph
    {
        get => _verticalGlyph ?? ControlGlyphs.Separators.Vertical.Value;
        set => SetOptionalGlyph(ref _verticalGlyph, value, nameof(VerticalGlyph));
    }

    /// <summary>Clears both local separator glyphs to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _horizontalGlyph, nameof(HorizontalGlyph));
        _ = ResetOptionalGlyph(ref _verticalGlyph, nameof(VerticalGlyph));
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
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var s = ResolvedStyle;
        var themed = Orientation == Orientation.Horizontal
            ? ControlGlyphs.Separators.Horizontal
            : ControlGlyphs.Separators.Vertical;
        var selected = Orientation == Orientation.Horizontal ? HorizontalGlyph : VerticalGlyph;
        var glyph = selected.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        if (Orientation == Orientation.Horizontal)
        {
            ControlChrome.DrawHorizontalLine(canvas, Bounds, glyph, s);
        }
        else
        {
            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                canvas.DrawRune(glyph, new Point(Bounds.X, y), s, BackgroundMode.Transparent);
            }
        }
    }

}
