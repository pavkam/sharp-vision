// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays one compact ordered series with sub-cell vertical resolution.</summary>
[PublicAPI]
public sealed class Sparkline: Control<ChartStyle>, IChartControl
{
    private readonly ChartControlState _state;

    /// <summary>Initializes an empty passive sparkline with automatic trend scaling.</summary>
    public Sparkline() : base(ChartStyle.Definition)
    {
        HitTestVisible = false;
        _state = new ChartControlState(
            this,
            new ChartScale(null, null, includeZero: false),
            ChartLegendPlacement.Hidden,
            false,
            false);
    }

    /// <summary>Gets or sets the single borrowed observable series source.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The source contains more than one series.</exception>
    public IReadOnlyList<ChartSeries> Series
    {
        get => _state.Series;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Count > 1)
            {
                throw new ArgumentException("A sparkline accepts at most one series.", nameof(value));
            }

            _state.Series = value;
        }
    }

    /// <summary>Gets or sets authored scale bounds and zero inclusion.</summary>
    public ChartScale Scale { get => _state.Scale; set => _state.Scale = value; }

    ChartLegendPlacement IChartControl.LegendPlacement => ChartLegendPlacement.Hidden;

    bool IChartControl.ShowCategoryLabels => false;

    bool IChartControl.ShowValueLabels => false;

    ControlBase IChartControl.Control => this;

    void IChartControl.ValidateSeries(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Count > 1)
        {
            throw new ArgumentException("A sparkline accepts at most one series.", nameof(series));
        }
    }

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
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(20, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        SparklineRenderer.Render(this, canvas, ResolvedStyle);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _state.Dispose();
        }
    }
}
