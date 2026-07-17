// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays a visual progress indicator using block characters with optional sub-cell resolution.</summary>
public sealed class ProgressBar: Control
{
    private Rune? _fillGlyph;
    private Rune? _trackGlyph;
    private Rune? _indeterminateGlyph;
    private double _value;

    /// <summary>Initializes a non-focusable horizontal progress bar at zero progress.</summary>
    public ProgressBar()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the minimum value.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Minimum
    {
        get;
        set
        {
            ValidateFinite(value, nameof(value));

            if (value >= Maximum)
            {
                throw new ArgumentException("Minimum must be below Maximum.", nameof(value));
            }

            VerifyMutable();
            var clamped = Math.Max(_value, value);

            if (field == value && _value == clamped)
            {
                return;
            }

            field = value;
            var valueChanged = _value != clamped;
            _value = clamped;
            NotifyPropertyChanged(nameof(Minimum), ChangeImpact.Render);

            if (valueChanged)
            {
                NotifyPropertyChanged(nameof(Value), ChangeImpact.Render);
            }
        }
    }

    /// <summary>Gets or sets the maximum value.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Maximum
    {
        get;
        set
        {
            ValidateFinite(value, nameof(value));

            if (value <= Minimum)
            {
                throw new ArgumentException("Maximum must be above Minimum.", nameof(value));
            }

            VerifyMutable();
            var clamped = Math.Min(_value, value);

            if (field == value && _value == clamped)
            {
                return;
            }

            field = value;
            var valueChanged = _value != clamped;
            _value = clamped;
            NotifyPropertyChanged(nameof(Maximum), ChangeImpact.Render);

            if (valueChanged)
            {
                NotifyPropertyChanged(nameof(Value), ChangeImpact.Render);
            }
        }
    } = 1;

    /// <summary>Gets or sets the current value, clamped between Minimum and Maximum.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Value
    {
        get => _value;
        set
        {
            ValidateFinite(value, nameof(value));
            var clamped = Math.Clamp(value, Minimum, Maximum);
            _ = SetProperty(ref _value, clamped, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets whether the bar shows an indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public bool IsIndeterminate
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets horizontal or vertical bar layout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The progress bar orientation is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets whether to use fractional block characters for sub-cell resolution.</summary>
    /// <remarks>
    /// When true, bars use the theme's eight intermediate horizontal or vertical fraction levels,
    /// providing eight times the effective resolution. When false, each cell uses either the
    /// theme's full or empty progress glyph.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public bool UseSubCellResolution
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets the local fully filled glyph.</summary>
    public Rune FillGlyph
    {
        get => _fillGlyph ?? ResolveThemeGlyphs().Progress.Full.Value;
        set => SetGlyph(ref _fillGlyph, value, nameof(FillGlyph));
    }

    /// <summary>Gets or sets the local empty-track glyph.</summary>
    public Rune TrackGlyph
    {
        get => _trackGlyph ?? ResolveThemeGlyphs().Progress.Empty.Value;
        set => SetGlyph(ref _trackGlyph, value, nameof(TrackGlyph));
    }

    /// <summary>Gets or sets the local indeterminate glyph.</summary>
    public Rune IndeterminateGlyph
    {
        get => _indeterminateGlyph ?? ResolveThemeGlyphs().Progress.Indeterminate.Value;
        set => SetGlyph(ref _indeterminateGlyph, value, nameof(IndeterminateGlyph));
    }

    /// <summary>Clears local progress glyph overrides so the active theme supplies them.</summary>
    public void ResetGlyphs()
    {
        VerifyMutable();

        if (!_fillGlyph.HasValue && !_trackGlyph.HasValue && !_indeterminateGlyph.HasValue)
        {
            return;
        }

        _fillGlyph = null;
        _trackGlyph = null;
        _indeterminateGlyph = null;
        NotifyPropertyChanged(nameof(FillGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(TrackGlyph), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(IndeterminateGlyph), ChangeImpact.Render);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return Orientation == Orientation.Horizontal ? new Size(10, 1) : new Size(1, 10);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (IsIndeterminate)
        {
            var glyph = ResolveConfiguredGlyph(
                IndeterminateGlyph,
                ResolveThemeGlyphs().Progress.Indeterminate);

            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                for (var x = Bounds.X; x < Bounds.Right; x++)
                {
                    canvas.DrawRune(glyph, new Point(x, y), style, BackgroundMode.Transparent);
                }
            }

            return;
        }

        var range = Maximum - Minimum;
        var ratio = range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;

        if (Orientation == Orientation.Horizontal)
        {
            RenderHorizontal(canvas, style, ratio);
        }
        else
        {
            RenderVertical(canvas, style, ratio);
        }
    }

    private void RenderHorizontal(TerminalCanvas canvas, TerminalStyle style, double ratio)
    {
        var progress = ResolveThemeGlyphs().Progress;

        if (UseSubCellResolution)
        {
            var totalEighths = (int) (ratio * Bounds.Width * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var cellIndex = x - Bounds.X;
                var glyph = cellIndex < fullCells
                    ? ResolveConfiguredGlyph(FillGlyph, progress.Full)
                    : cellIndex == fullCells && remainder > 0
                        ? ResolveThemeGlyph(progress.HorizontalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                canvas.DrawRune(glyph, new Point(x, Bounds.Y), style, BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (Bounds.Width * ratio);

            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var glyph = x - Bounds.X < filled
                    ? ResolveConfiguredGlyph(FillGlyph, progress.Full)
                    : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                canvas.DrawRune(glyph, new Point(x, Bounds.Y), style, BackgroundMode.Transparent);
            }
        }
    }

    private void RenderVertical(TerminalCanvas canvas, TerminalStyle style, double ratio)
    {
        var progress = ResolveThemeGlyphs().Progress;

        if (UseSubCellResolution)
        {
            var totalEighths = (int) (ratio * Bounds.Height * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                var cellFromBottom = Bounds.Bottom - 1 - y;
                var glyph = cellFromBottom < fullCells
                    ? ResolveConfiguredGlyph(FillGlyph, progress.Full)
                    : cellFromBottom == fullCells && remainder > 0
                        ? ResolveThemeGlyph(progress.VerticalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                canvas.DrawRune(glyph, new Point(Bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (Bounds.Height * ratio);
            var emptyEnd = Bounds.Bottom - filled;

            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                var glyph = y >= emptyEnd
                    ? ResolveConfiguredGlyph(FillGlyph, progress.Full)
                    : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                canvas.DrawRune(glyph, new Point(Bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
    }

    private Rune ResolveConfiguredGlyph(Rune value, ThemedGlyph themed) =>
        CellGlyph.Resolve(value, themed.Fallback, CellPolicy.AmbiguousWidth);

    private void SetGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ThemedGlyph(value, value);
        VerifyMutable();

        if (storage == value)
        {
            return;
        }

        storage = value;
        NotifyPropertyChanged(propertyName, ChangeImpact.Render);
    }

    private static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Progress values must be finite.");
        }
    }
}
