// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Displays a visual progress indicator using block characters with optional sub-cell resolution.</summary>
[PublicAPI]
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

    /// <summary>Raised after a changed value commits.</summary>
    public event EventHandler<ProgressValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the lower bound of the progress range. Value is clamped to [Minimum, Maximum]. Default is 0.</summary>
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

            if (Math.Abs(field - value) < float.Epsilon && Math.Abs(_value - clamped) < float.Epsilon)
            {
                return;
            }

            field = value;
            var previousValue = _value;
            _value = clamped;

            NotifyPropertyChanged(nameof(Minimum), InvalidationImpact.Render);
            NotifyValueChanged(previousValue, clamped);
        }
    }

    /// <summary>Gets or sets the upper bound of the progress range. Value is clamped to [Minimum, Maximum]. Default is 100.</summary>
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

            if (Math.Abs(field - value) < float.Epsilon && Math.Abs(_value - clamped) < float.Epsilon)
            {
                return;
            }

            field = value;
            var previousValue = _value;
            _value = clamped;

            NotifyPropertyChanged(nameof(Maximum), InvalidationImpact.Render);
            NotifyValueChanged(previousValue, clamped);
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
            VerifyMutable();
            var clamped = Math.Clamp(value, Minimum, Maximum);
            var previousValue = _value;
            _value = clamped;

            NotifyValueChanged(previousValue, clamped);
        }
    }

    /// <summary>Gets or sets whether the bar shows an indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public bool IsIndeterminate
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets the optional foreground override of completed progress.</summary>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public Color? FillColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the foreground of incomplete track cells.</summary>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public Color? TrackColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the foreground of the indeterminate state.</summary>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public Color? IndeterminateColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the local fully filled glyph.</summary>
    public Rune FillGlyph
    {
        get => _fillGlyph ?? ControlGlyphs.Progress.Full.Value;
        set => SetOptionalGlyph(ref _fillGlyph, value, nameof(FillGlyph));
    }

    /// <summary>Gets or sets the local empty-track glyph.</summary>
    public Rune TrackGlyph
    {
        get => _trackGlyph ?? ControlGlyphs.Progress.Empty.Value;
        set => SetOptionalGlyph(ref _trackGlyph, value, nameof(TrackGlyph));
    }

    /// <summary>Gets or sets the local indeterminate glyph.</summary>
    public Rune IndeterminateGlyph
    {
        get => _indeterminateGlyph ?? ControlGlyphs.Progress.Indeterminate.Value;
        set => SetOptionalGlyph(ref _indeterminateGlyph, value, nameof(IndeterminateGlyph));
    }

    /// <summary>Clears local progress glyph overrides to the code-owned defaults.</summary>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _fillGlyph, nameof(FillGlyph));
        _ = ResetOptionalGlyph(ref _trackGlyph, nameof(TrackGlyph));
        _ = ResetOptionalGlyph(ref _indeterminateGlyph, nameof(IndeterminateGlyph));
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
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

        var inherited = ResolvedStyle;

        if (IsIndeterminate)
        {
            var style = inherited.WithForeground(IndeterminateColor ?? Theme?.ResolveColor(ThemeColor.Accent) ?? Color.Default);
            var glyph = ResolveConfiguredGlyph(
                IndeterminateGlyph,
                ControlGlyphs.Progress.Indeterminate);

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
            RenderHorizontal(
                canvas,
                inherited.WithForeground(FillColor ?? Theme?.ResolveColor(ThemeColor.Accent) ?? Color.Default),
                inherited.WithForeground(TrackColor ?? Theme?.Muted ?? Color.Default),
                ratio);
        }
        else
        {
            RenderVertical(
                canvas,
                inherited.WithForeground(FillColor ?? Theme?.ResolveColor(ThemeColor.Accent) ?? Color.Default),
                inherited.WithForeground(TrackColor ?? Theme?.Muted ?? Color.Default),
                ratio);
        }
    }

    private void RenderHorizontal(
        TerminalCanvas canvas,
        TerminalStyle fillStyle,
        TerminalStyle trackStyle,
        double ratio)
    {
        var progress = ControlGlyphs.Progress;

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
                        ? ResolveControlGlyph(progress.HorizontalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                var style = cellIndex < fullCells || (cellIndex == fullCells && remainder > 0)
                    ? fillStyle
                    : trackStyle;
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
                var style = x - Bounds.X < filled ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(x, Bounds.Y), style, BackgroundMode.Transparent);
            }
        }
    }

    private void RenderVertical(
        TerminalCanvas canvas,
        TerminalStyle fillStyle,
        TerminalStyle trackStyle,
        double ratio)
    {
        var progress = ControlGlyphs.Progress;

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
                        ? ResolveControlGlyph(progress.VerticalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(TrackGlyph, progress.Empty);
                var style = cellFromBottom < fullCells || (cellFromBottom == fullCells && remainder > 0)
                    ? fillStyle
                    : trackStyle;
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
                var style = y >= emptyEnd ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(Bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
    }

    private Rune ResolveConfiguredGlyph(Rune value, ControlGlyph themed) =>
        CellGlyphResolver.Resolve(value, themed.Fallback, CellPolicy.AmbiguousWidth);

    // Raised for every committed Value transition regardless of which public
    // setter caused it — Value directly, or Minimum/Maximum clamping it — so
    // PropertyChanged(Value) and ValueChanged subscribers observe the same
    // history. Callers commit _value before calling this, so every endpoint
    // and Value are already coherent for both notifications raised here. A
    // clamp that leaves Value unchanged stays silent.
    private void NotifyValueChanged(double previousValue, double currentValue)
    {
        if (previousValue.Equals(currentValue))
        {
            return;
        }

        NotifyPropertyChanged(nameof(Value), InvalidationImpact.Render);
        ValueChanged?.Invoke(this, new ProgressValueChangedEventArgs(previousValue, currentValue));
    }

    private static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Progress values must be finite.");
        }
    }
}
