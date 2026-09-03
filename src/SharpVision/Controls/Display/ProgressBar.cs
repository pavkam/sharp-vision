// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Displays a visual progress indicator using block characters with optional sub-cell resolution.</summary>
[PublicAPI]
public sealed class ProgressBar: ControlBase, IStyled<ProgressBarStyle>
{
    private double _value;
    private readonly CallbackTransitionStream _valueTransitions = new();
    private readonly StyleSlot<ProgressBarStyle> _style;

    /// <summary>Initializes a non-focusable horizontal progress bar at zero progress.</summary>
    public ProgressBar()
    {
        _style = InitializeStyle(ProgressBarStyle.Definition);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public ProgressBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ProgressBarStyle ActualStyle => _style.Actual;

    /// <summary>Raised after a changed value commits.</summary>
    public event EventHandler<ProgressValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the lower bound of the progress range. Assigning it clamps
    /// <see cref="Value"/> into the new range. Default is 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="ArgumentException">The value is not below <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Minimum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "Progress values must be finite.");
            ArgumentException.ThrowIfAtOrAboveMaximum(value, Maximum, nameof(value), "Minimum must be below Maximum.");

            VerifyMutable();
            var clamped = Math.Max(_value, value);

            // Exact equality, not a tolerance: Minimum and Value are the same double both before
            // and after this comparison runs, so there is no accumulated floating-point error to
            // absorb. A tolerance-based skip here previously misread any two distinct doubles
            // whose difference underflows a fixed absolute epsilon - true for every representable
            // pair near zero, including the ordinary values this range control targets - as "no
            // change," silently discarding a real assignment instead of committing it.
            if (field == value && _value == clamped)
            {
                return;
            }

            field = value;
            var previousValue = _value;
            _value = clamped;

            if (previousValue.Equals(clamped))
            {
                NotifyPropertyChanged(nameof(Minimum), InvalidationImpact.Render);
                return;
            }

            var transition = BeginPropertyTransition(
                _valueTransitions,
                InvalidationImpact.Render,
                nameof(Minimum));
            NotifyValueChanged(previousValue, clamped, ref transition);
        }
    }

    /// <summary>Gets or sets the upper bound of the progress range. Assigning it clamps
    /// <see cref="Value"/> into the new range. Default is 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="ArgumentException">The value is not above <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Maximum
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "Progress values must be finite.");
            ArgumentException.ThrowIfAtOrBelowMinimum(value, Minimum, nameof(value), "Maximum must be above Minimum.");

            VerifyMutable();
            var clamped = Math.Min(_value, value);

            // See the identical comment on Minimum's setter: exact equality is correct here for
            // the same reason - no floating-point error accumulates between these two reads of
            // the same double, so a tolerance only risks discarding a real, distinct assignment.
            if (field == value && _value == clamped)
            {
                return;
            }

            field = value;
            var previousValue = _value;
            _value = clamped;

            if (previousValue.Equals(clamped))
            {
                NotifyPropertyChanged(nameof(Maximum), InvalidationImpact.Render);
                return;
            }

            var transition = BeginPropertyTransition(
                _valueTransitions,
                InvalidationImpact.Render,
                nameof(Maximum));
            NotifyValueChanged(previousValue, clamped, ref transition);
        }
    } = 1;

    /// <summary>Gets or sets the current value, clamped between <see cref="Minimum"/> and
    /// <see cref="Maximum"/>. Only a non-finite value is rejected; an in-range assignment outside
    /// the endpoints clamps rather than throwing.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Value
    {
        get => _value;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "Progress values must be finite.");
            VerifyMutable();
            var clamped = Math.Clamp(value, Minimum, Maximum);
            var previousValue = _value;

            if (previousValue.Equals(clamped))
            {
                return;
            }

            _value = clamped;
            var transition = BeginPropertyTransition(
                _valueTransitions,
                InvalidationImpact.Render,
                nameof(Value));
            PublishValueChanged(previousValue, clamped, ref transition);
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The progress bar orientation is unknown.");

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
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var inherited = ResolvedStyle;
        var actualStyle = ActualStyle;

        if (IsIndeterminate)
        {
            var style = inherited.WithForeground(ResolveColor(actualStyle.IndeterminateColor));
            var glyph = ResolveConfiguredGlyph(actualStyle.Glyphs.IndeterminateGlyph);
            var visible = canvas.Bounds.Intersect(bounds);

            for (var y = visible.Y; y < visible.Bottom; y++)
            {
                for (var x = visible.X; x < visible.Right; x++)
                {
                    canvas.DrawRune(glyph, new Point(x, y), style, BackgroundMode.Transparent);
                }
            }

            return;
        }

        var ratio = ResolveRatio(Minimum, Maximum, Value);

        if (Orientation == Orientation.Horizontal)
        {
            RenderHorizontal(
                canvas,
                bounds,
                actualStyle.Glyphs,
                inherited.WithForeground(ResolveColor(actualStyle.FillColor)),
                inherited.WithForeground(ResolveColor(actualStyle.TrackColor)),
                ratio);
        }
        else
        {
            RenderVertical(
                canvas,
                bounds,
                actualStyle.Glyphs,
                inherited.WithForeground(ResolveColor(actualStyle.FillColor)),
                inherited.WithForeground(ResolveColor(actualStyle.TrackColor)),
                ratio);
        }
    }

    /// <summary>Normalizes one finite value across ordered finite endpoints without overflowing
    /// subtraction for opposite-sign extreme ranges.</summary>
    [Pure]
    internal static double ResolveRatio(double minimum, double maximum, double value)
    {
        if (maximum <= minimum || value <= minimum)
        {
            return 0;
        }

        if (value >= maximum)
        {
            return 1;
        }

        var range = maximum - minimum;

        if (double.IsFinite(range))
        {
            return Math.Clamp((value - minimum) / range, 0, 1);
        }

        var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        var normalizedMinimum = minimum / scale;
        var normalizedMaximum = maximum / scale;
        var normalizedValue = value / scale;
        return Math.Clamp(
            (normalizedValue - normalizedMinimum) / (normalizedMaximum - normalizedMinimum),
            0,
            1);
    }

    private void RenderHorizontal(
        TerminalCanvas canvas,
        Rect bounds,
        ProgressBarGlyphs glyphs,
        TerminalStyle fillStyle,
        TerminalStyle trackStyle,
        double ratio)
    {
        var progress = ControlGlyphs.Progress;
        var visible = canvas.Bounds.Intersect(bounds);

        if (UseSubCellResolution)
        {
            var totalEighths = (long) (ratio * bounds.Width * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var x = visible.X; x < visible.Right; x++)
            {
                var cellIndex = x - bounds.X;
                var glyph = cellIndex < fullCells
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : cellIndex == fullCells && remainder > 0
                        ? ResolveControlGlyph(progress.HorizontalFractions.Span[(int) remainder])
                        : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = cellIndex < fullCells || (cellIndex == fullCells && remainder > 0)
                    ? fillStyle
                    : trackStyle;
                canvas.DrawRune(glyph, new Point(x, bounds.Y), style, BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (bounds.Width * ratio);

            for (var x = visible.X; x < visible.Right; x++)
            {
                var glyph = x - bounds.X < filled
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = x - bounds.X < filled ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(x, bounds.Y), style, BackgroundMode.Transparent);
            }
        }
    }

    private void RenderVertical(
        TerminalCanvas canvas,
        Rect bounds,
        ProgressBarGlyphs glyphs,
        TerminalStyle fillStyle,
        TerminalStyle trackStyle,
        double ratio)
    {
        var progress = ControlGlyphs.Progress;
        var visible = canvas.Bounds.Intersect(bounds);

        if (UseSubCellResolution)
        {
            var totalEighths = (long) (ratio * bounds.Height * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var y = visible.Y; y < visible.Bottom; y++)
            {
                var cellFromBottom = bounds.Bottom - 1 - y;
                var glyph = cellFromBottom < fullCells
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : cellFromBottom == fullCells && remainder > 0
                        ? ResolveControlGlyph(progress.VerticalFractions.Span[(int) remainder])
                        : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = cellFromBottom < fullCells || (cellFromBottom == fullCells && remainder > 0)
                    ? fillStyle
                    : trackStyle;
                canvas.DrawRune(glyph, new Point(bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (bounds.Height * ratio);
            var emptyEnd = bounds.Bottom - filled;

            for (var y = visible.Y; y < visible.Bottom; y++)
            {
                var glyph = y >= emptyEnd
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = y >= emptyEnd ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
    }

    [Pure]
    private Rune ResolveConfiguredGlyph(ControlGlyph themed) =>
        themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);

    // Raised for every committed Value transition regardless of which public
    // setter caused it — Value directly, or Minimum/Maximum clamping it — so
    // PropertyChanged(Value) and ValueChanged subscribers observe the same
    // history. Callers commit _value before calling this, so every endpoint
    // and Value are already coherent for both notifications raised here. A
    // clamp that leaves Value unchanged stays silent.
    private void NotifyValueChanged(
        double previousValue,
        double currentValue,
        ref CallbackTransitionTransaction transition)
    {
        PublishTransitionProperty(
            ref transition,
            nameof(Value),
            InvalidationImpact.Render);
        PublishValueChanged(previousValue, currentValue, ref transition);
    }

    private void PublishValueChanged(
        double previousValue,
        double currentValue,
        ref CallbackTransitionTransaction transition)
    {
        transition.PublishCurrent(
            ValueChanged,
            this,
            new ProgressValueChangedEventArgs(previousValue, currentValue));
        transition.ThrowIfFailed();
    }
}
