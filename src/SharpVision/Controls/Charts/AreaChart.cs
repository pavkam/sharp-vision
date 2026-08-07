// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays ordered values as connected colored lines with baseline fill.</summary>
[PublicAPI]
public sealed class AreaChart: Control<ChartStyle>, IChartControl
{
    private readonly ChartControlState _state;

    /// <summary>Initializes an empty passive area chart with automatic trend scaling.</summary>
    public AreaChart() : base(ChartStyle.Definition)
    {
        HitTestVisible = false;
        _state = new ChartControlState(
            this,
            new ChartScale(null, null, includeZero: false),
            ChartLegendPlacement.Automatic,
            true,
            false);
    }

    /// <summary>Gets or sets the borrowed observable series source.</summary>
    public IReadOnlyList<ChartSeries> Series { get => _state.Series; set => _state.Series = value; }

    /// <summary>Gets or sets authored scale bounds and zero inclusion.</summary>
    public ChartScale Scale { get => _state.Scale; set => _state.Scale = value; }

    /// <summary>Gets or sets legend placement and automatic visibility.</summary>
    public ChartLegendPlacement LegendPlacement { get => _state.LegendPlacement; set => _state.LegendPlacement = value; }

    /// <summary>Gets or sets whether category labels consume plot cells when they fit.</summary>
    public bool ShowCategoryLabels { get => _state.ShowCategoryLabels; set => _state.ShowCategoryLabels = value; }

    /// <summary>Gets or sets whether numeric point values are drawn when they fit.</summary>
    public bool ShowValueLabels { get => _state.ShowValueLabels; set => _state.ShowValueLabels = value; }

    ControlBase IChartControl.Control => this;

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
        return new Size(30, 10);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        AreaChartRenderer.Render(this, canvas, ResolvedStyle);

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
