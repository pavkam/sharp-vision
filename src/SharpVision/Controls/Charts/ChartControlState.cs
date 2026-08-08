// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Stores and validates common public state for one concrete chart control.</summary>
internal sealed class ChartControlState: IDisposable
{
    private readonly ChartDataObserver _observer;
    private readonly IChartControl _owner;
    private ChartLegendPlacement _legendPlacement;
    private ChartScale _scale;
    private bool _showCategoryLabels;
    private bool _showValueLabels;

    /// <summary>Initializes common state with family-specific presentation defaults.</summary>
    internal ChartControlState(
        IChartControl owner,
        ChartScale scale,
        ChartLegendPlacement legendPlacement,
        bool showCategoryLabels,
        bool showValueLabels)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _scale = scale;
        _legendPlacement = legendPlacement;
        _showCategoryLabels = showCategoryLabels;
        _showValueLabels = showValueLabels;
        _observer = new ChartDataObserver(owner);
    }

    /// <summary>Gets or replaces borrowed chart series.</summary>
    internal IReadOnlyList<ChartSeries> Series
    {
        get => _observer.Current;
        set
        {
            _owner.Control.VerifyMutable();
            _observer.Replace(value);
        }
    }

    /// <summary>Gets or sets authored scale policy.</summary>
    internal ChartScale Scale
    {
        get => _scale;
        set => Set(ref _scale, value, InvalidationImpact.Render, nameof(Scale));
    }

    /// <summary>Gets or sets legend placement.</summary>
    internal ChartLegendPlacement LegendPlacement
    {
        get => _legendPlacement;
        set
        {
            value.ValidateDefined(nameof(value), "The chart legend placement is unknown.");

            Set(ref _legendPlacement, value, InvalidationImpact.Measure, nameof(LegendPlacement));
        }
    }

    /// <summary>Gets or sets whether category labels may consume cells.</summary>
    internal bool ShowCategoryLabels
    {
        get => _showCategoryLabels;
        set => Set(ref _showCategoryLabels, value, InvalidationImpact.Measure, nameof(ShowCategoryLabels));
    }

    /// <summary>Gets or sets whether value labels may consume cells.</summary>
    internal bool ShowValueLabels
    {
        get => _showValueLabels;
        set => Set(ref _showValueLabels, value, InvalidationImpact.Measure, nameof(ShowValueLabels));
    }

    /// <inheritdoc/>
    public void Dispose() => _observer.Dispose();

    private void Set<T>(ref T field, T value, InvalidationImpact impact, string propertyName)
    {
        _owner.Control.VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        _owner.OnChartPropertyChanged(propertyName, impact);
    }
}
