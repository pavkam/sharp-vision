// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using UnicodeWidth = Width;

/// <summary>Draws one non-interactive horizontal or vertical divider line.</summary>
public sealed class Separator: Control
{
    #region Construction and properties

    /// <summary>Initializes a horizontal divider excluded from focus and hit testing.</summary>
    public Separator()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        CanFocus = false;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets whether the divider runs left-to-right or top-to-bottom.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The separator orientation is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets the printable one-cell horizontal divider glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell under the narrow policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune HorizontalGlyph
    {
        get;
        set => _ = SetProperty(ref field, Validate(value, nameof(value)), ChangeImpact.Render);
    } = new('─');

    /// <summary>Gets or sets the printable one-cell vertical divider glyph.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell under the narrow policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune VerticalGlyph
    {
        get;
        set => _ = SetProperty(ref field, Validate(value, nameof(value)), ChangeImpact.Render);
    } = new('│');

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderChrome(canvas);
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var glyph = RenderGlyph();

        if (Orientation == Orientation.Horizontal)
        {
            canvas.DrawLine(
                new Point(bounds.X, bounds.Y),
                new Point(bounds.Right - 1, bounds.Y),
                glyph,
                ResolvedStyle);
        }
        else
        {
            canvas.DrawLine(
                new Point(bounds.X, bounds.Y),
                new Point(bounds.X, bounds.Bottom - 1),
                glyph,
                ResolvedStyle);
        }
    }

    #endregion

    private Rune RenderGlyph()
    {
        var value = Orientation == Orientation.Horizontal ? HorizontalGlyph : VerticalGlyph;
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);

        return UnicodeWidth.Measure(buffer[..length], CellPolicy.AmbiguousWidth).Cells == 1
            ? value
            : Orientation == Orientation.Horizontal
                ? new Rune('-')
                : new Rune('|');
    }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = UnicodeWidth.Measure(buffer[..length], Ambiguous.Narrow);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException(
                "A separator glyph must be printable and one cell wide under the narrow policy.",
                name);
    }
}
