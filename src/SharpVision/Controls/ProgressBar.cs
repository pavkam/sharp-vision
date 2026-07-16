// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using UnicodeWidth = Width;

/// <summary>Displays determinate or indeterminate progress as semantic terminal cells.</summary>
public sealed class ProgressBar: Control
{
    private double _value;

    #region Construction and properties

    /// <summary>Initializes a horizontal zero-to-one bar excluded from focus and hit testing.</summary>
    public ProgressBar()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        CanFocus = false;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the finite inclusive lower endpoint, strictly below Maximum.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="ArgumentException">The value is greater than or equal to Maximum.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public double Minimum
    {
        get;
        set
        {
            ValidateFinite(value, nameof(value));

            if (value >= Maximum)
            {
                throw new ArgumentException("Minimum must be less than Maximum.", nameof(value));
            }

            VerifyMutable();

            if (field.Equals(value))
            {
                return;
            }

            field = value;
            var clamped = _value < value;

            if (clamped)
            {
                _value = value;
            }

            NotifyPropertyChanged(nameof(Minimum), ChangeImpact.Render);

            if (clamped)
            {
                NotifyPropertyChanged(nameof(Value), ChangeImpact.Render);
            }
        }
    }

    /// <summary>Gets or sets the finite inclusive upper endpoint, strictly above Minimum.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="ArgumentException">The value is less than or equal to Minimum.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public double Maximum
    {
        get;
        set
        {
            ValidateFinite(value, nameof(value));

            if (value <= Minimum)
            {
                throw new ArgumentException("Maximum must be greater than Minimum.", nameof(value));
            }

            VerifyMutable();

            if (field.Equals(value))
            {
                return;
            }

            field = value;
            var clamped = _value > value;

            if (clamped)
            {
                _value = value;
            }

            NotifyPropertyChanged(nameof(Maximum), ChangeImpact.Render);

            if (clamped)
            {
                NotifyPropertyChanged(nameof(Value), ChangeImpact.Render);
            }
        }
    } = 1;

    /// <summary>Gets or sets a finite value clamped into the inclusive range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public double Value
    {
        get => _value;
        set
        {
            ValidateFinite(value, nameof(value));
            _ = SetProperty(ref _value, Math.Clamp(value, Minimum, Maximum), ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets whether the range is unknown and uses deterministic indeterminate cells.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsIndeterminate
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets whether fill advances left-to-right or bottom-to-top.</summary>
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
                throw new ArgumentOutOfRangeException(nameof(value), value, "The progress orientation is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets the printable one-cell glyph used for filled determinate cells.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell under the narrow policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune FillGlyph
    {
        get;
        set => _ = SetProperty(ref field, ValidateGlyph(value, nameof(value)), ChangeImpact.Render);
    } = new('█');

    /// <summary>Gets or sets the printable one-cell glyph used for unfilled determinate cells.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell under the narrow policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune TrackGlyph
    {
        get;
        set => _ = SetProperty(ref field, ValidateGlyph(value, nameof(value)), ChangeImpact.Render);
    } = new('░');

    /// <summary>Gets or sets the printable one-cell glyph used while progress is indeterminate.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell under the narrow policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune IndeterminateGlyph
    {
        get;
        set => _ = SetProperty(ref field, ValidateGlyph(value, nameof(value)), ChangeImpact.Render);
    } = new('▒');

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

        var style = ResolvedStyle;

        if (IsIndeterminate)
        {
            canvas.Fill(bounds, RenderGlyph(IndeterminateGlyph, new Rune('?')), style);
            return;
        }

        var cells = Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;
        var filled = FilledCells(cells);
        var fill = RenderGlyph(FillGlyph, new Rune('#'));
        var track = RenderGlyph(TrackGlyph, new Rune('.'));

        if (Orientation == Orientation.Horizontal)
        {
            DrawHorizontal(canvas, bounds, filled, fill, track, style);
        }
        else
        {
            DrawVertical(canvas, bounds, filled, fill, track, style);
        }
    }

    #endregion

    private static void DrawHorizontal(
        TerminalCanvas canvas,
        Rect bounds,
        int filled,
        Rune fill,
        Rune track,
        TerminalStyle style)
    {
        if (filled > 0)
        {
            canvas.Fill(new Rect(bounds.X, bounds.Y, filled, bounds.Height), fill, style);
        }

        if (filled < bounds.Width)
        {
            canvas.Fill(
                new Rect(bounds.X + filled, bounds.Y, bounds.Width - filled, bounds.Height),
                track,
                style);
        }
    }

    private static void DrawVertical(
        TerminalCanvas canvas,
        Rect bounds,
        int filled,
        Rune fill,
        Rune track,
        TerminalStyle style)
    {
        var trackHeight = bounds.Height - filled;

        if (trackHeight > 0)
        {
            canvas.Fill(new Rect(bounds.X, bounds.Y, bounds.Width, trackHeight), track, style);
        }

        if (filled > 0)
        {
            canvas.Fill(
                new Rect(bounds.X, bounds.Y + trackHeight, bounds.Width, filled),
                fill,
                style);
        }
    }

    private int FilledCells(int cells)
    {
        if (_value <= Minimum)
        {
            return 0;
        }

        if (_value >= Maximum)
        {
            return cells;
        }

        var normalized = (_value - Minimum) / (Maximum - Minimum);
        return (int) Math.Floor(normalized * cells);
    }

    private Rune RenderGlyph(Rune value, Rune fallback)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        return UnicodeWidth.Measure(buffer[..length], CellPolicy.AmbiguousWidth).Cells == 1
            ? value
            : fallback;
    }

    private static Rune ValidateGlyph(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = UnicodeWidth.Measure(buffer[..length], Ambiguous.Narrow);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException(
                "A progress glyph must be printable and one cell wide under the narrow policy.",
                name);
    }

    private static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "A progress range value must be finite.");
        }
    }
}
