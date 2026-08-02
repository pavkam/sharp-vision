// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Displays a visual progress indicator using block characters with optional sub-cell resolution.</summary>
[PublicAPI]
public sealed class ProgressBar: Control
{
    private static readonly StyleContract<ProgressBarStyle> _styleContract = new(
        ThemeRole.Control,
        static profile => new ProgressBarStyle(
            ProgressBarStyle.Default.FillColor,
            ProgressBarStyle.Default.TrackColor,
            ProgressBarStyle.Default.IndeterminateColor,
            ProgressBarStyle.Default.Glyphs,
            profile),
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ResolveColor(previous.FillColor, previousTheme) != ResolveColor(current.FillColor, currentTheme) ||
            ResolveColor(previous.TrackColor, previousTheme) != ResolveColor(current.TrackColor, currentTheme) ||
            ResolveColor(previous.IndeterminateColor, previousTheme) != ResolveColor(current.IndeterminateColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None,
        static style => style.Appearance);
    private double _value;
    private ProgressBarStyle? _actualStyleCache;
    private ProgressBarStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

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

    /// <summary>Gets or sets the upper bound of the progress range. Value is clamped to [Minimum, Maximum]. Default is 1.</summary>
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

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic control profile.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public ProgressBarStyle? Style
    {
        get;
        set => _ = SetControlStyle(
            ref field,
            value,
            _styleContract.Resolve,
            _styleContract.CompareStructure,
            _styleContract.Appearance,
            nameof(Style),
            nameof(ActualStyle));
    }

    /// <summary>Gets the complete local presentation or the library fill-and-track mechanics completed with the semantic control profile.</summary>
    public ProgressBarStyle ActualStyle =>
        ResolveContractStyle(
            _styleContract,
            ref _actualStyleCache,
            ref _actualStyleCacheKey,
            ref _actualStyleCacheTheme,
            Style,
            Theme);

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => _styleContract.Role;

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile => ActualStyle.Appearance;

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        GetContractAppearanceProfile(_styleContract, Style, theme);

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetContractThemeChangeImpact(
            _styleContract,
            Style,
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        GetContractResolvedStylePropertyName(_styleContract, Style, previous, current, nameof(ActualStyle));

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
        var actualStyle = ActualStyle;

        if (IsIndeterminate)
        {
            var style = inherited.WithForeground(ResolveColor(actualStyle.IndeterminateColor));
            var glyph = ResolveConfiguredGlyph(actualStyle.Glyphs.IndeterminateGlyph);

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
                actualStyle.Glyphs,
                inherited.WithForeground(ResolveColor(actualStyle.FillColor)),
                inherited.WithForeground(ResolveColor(actualStyle.TrackColor)),
                ratio);
        }
        else
        {
            RenderVertical(
                canvas,
                actualStyle.Glyphs,
                inherited.WithForeground(ResolveColor(actualStyle.FillColor)),
                inherited.WithForeground(ResolveColor(actualStyle.TrackColor)),
                ratio);
        }
    }

    private void RenderHorizontal(
        TerminalCanvas canvas,
        ProgressBarGlyphs glyphs,
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
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : cellIndex == fullCells && remainder > 0
                        ? ResolveControlGlyph(progress.HorizontalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(glyphs.TrackGlyph);
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
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = x - Bounds.X < filled ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(x, Bounds.Y), style, BackgroundMode.Transparent);
            }
        }
    }

    private void RenderVertical(
        TerminalCanvas canvas,
        ProgressBarGlyphs glyphs,
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
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : cellFromBottom == fullCells && remainder > 0
                        ? ResolveControlGlyph(progress.VerticalFractions.Span[remainder])
                        : ResolveConfiguredGlyph(glyphs.TrackGlyph);
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
                    ? ResolveConfiguredGlyph(glyphs.FillGlyph)
                    : ResolveConfiguredGlyph(glyphs.TrackGlyph);
                var style = y >= emptyEnd ? fillStyle : trackStyle;
                canvas.DrawRune(glyph, new Point(Bounds.X, y), style, BackgroundMode.Transparent);
            }
        }
    }

    private Rune ResolveConfiguredGlyph(ControlGlyph themed) =>
        CellGlyphResolver.Resolve(themed.Value, themed.Fallback, CellPolicy.AmbiguousWidth);

    private Color ResolveColor(ColorValue value) => ResolveColor(value, Theme);

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
