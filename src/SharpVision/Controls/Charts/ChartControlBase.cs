// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Provides the state, style, and <see cref="IChartControl"/> plumbing shared by every
/// concrete chart control.</summary>
[PublicAPI]
public abstract class ChartControlBase: ControlBase, IStyled<ChartStyle>, IChartControl
{
    private readonly StyleSlot<ChartStyle> _style;

    /// <summary>Initializes common chart state with family-specific presentation defaults.</summary>
    /// <param name="scale">The authored scale bounds and zero-inclusion policy.</param>
    /// <param name="legendPlacement">The initial legend policy reported to <see cref="IChartControl"/>
    /// consumers via <see cref="ResolveLegendPlacement"/>.</param>
    /// <param name="showCategoryLabels">The initial category-label policy reported via
    /// <see cref="ResolveShowCategoryLabels"/>.</param>
    /// <param name="showValueLabels">The initial value-label policy reported via
    /// <see cref="ResolveShowValueLabels"/>.</param>
    protected ChartControlBase(
        ChartScale scale,
        ChartLegendPlacement legendPlacement,
        bool showCategoryLabels,
        bool showValueLabels)
    {
        _style = InitializeStyle(ChartStyle.Definition);
        IsHitTestVisible = false;
        State = new ChartControlState(this, scale, legendPlacement, showCategoryLabels, showValueLabels);
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public ChartStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ChartStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the borrowed observable series source.</summary>
    public IReadOnlyList<ChartSeries> Series
    {
        get => State.Series;
        set
        {
            ValidateSeriesCore(value);
            State.Series = value;
        }
    }

    /// <summary>Gets or sets authored scale bounds and zero inclusion.</summary>
    public ChartScale Scale
    {
        get => State.Scale;
        set => State.Scale = value;
    }

    /// <summary>Gets the common state shared with the owning concrete chart.</summary>
    private protected ChartControlState State { get; }

    ControlBase IChartControl.Control => this;

    ChartLegendPlacement IChartControl.LegendPlacement => ResolveLegendPlacement();

    bool IChartControl.ShowCategoryLabels => ResolveShowCategoryLabels();

    bool IChartControl.ShowValueLabels => ResolveShowValueLabels();

    /// <summary>Gets the legend policy reported to <see cref="IChartControl"/> consumers. The
    /// default forwards to <see cref="State"/>; a chart family with a fixed policy (no public
    /// legend surface) overrides this instead of exposing a settable property.</summary>
    protected virtual ChartLegendPlacement ResolveLegendPlacement() => State.LegendPlacement;

    /// <summary>Gets whether category labels are reported to <see cref="IChartControl"/>
    /// consumers. See <see cref="ResolveLegendPlacement"/> for the override convention.</summary>
    protected virtual bool ResolveShowCategoryLabels() => State.ShowCategoryLabels;

    /// <summary>Gets whether value labels are reported to <see cref="IChartControl"/> consumers.
    /// See <see cref="ResolveLegendPlacement"/> for the override convention.</summary>
    protected virtual bool ResolveShowValueLabels() => State.ShowValueLabels;

    void IChartControl.ValidateSeries(IReadOnlyList<ChartSeries> series) => ValidateSeriesCore(series);

    /// <summary>Validates a complete prospective series membership snapshot. The default rejects
    /// only a null snapshot; a chart family with a narrower series-count contract overrides this.</summary>
    /// <param name="series">The prospective series membership snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is null.</exception>
    protected virtual void ValidateSeriesCore(IReadOnlyList<ChartSeries> series) =>
        ArgumentNullException.ThrowIfNull(series);

    void IChartControl.OnChartDataChanged(InvalidationImpact impact, bool seriesMembershipChanged)
    {
        if (seriesMembershipChanged)
        {
            NotifyPropertyChanged(nameof(Series), impact);
        }
        else
        {
            Invalidate(impact);
        }
    }

    void IChartControl.OnChartPropertyChanged(string propertyName, InvalidationImpact impact) =>
        NotifyPropertyChanged(propertyName, impact);

    /// <inheritdoc/>
    protected sealed override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return DefaultSize;
    }

    /// <summary>Gets the fixed size this chart reports regardless of its measure constraint.</summary>
    protected abstract Size DefaultSize { get; }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            State.Dispose();
        }
    }
}
