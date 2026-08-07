// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Coordinates shared internal behavior across concrete public chart controls.</summary>
internal interface IChartControl
{
    /// <summary>Gets the concrete control owner.</summary>
    public ControlBase Control { get; }

    /// <summary>Gets the current series snapshot.</summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>Gets the authored scale.</summary>
    public ChartScale Scale { get; }

    /// <summary>Gets the legend policy.</summary>
    public ChartLegendPlacement LegendPlacement { get; }

    /// <summary>Gets whether category labels may consume cells.</summary>
    public bool ShowCategoryLabels { get; }

    /// <summary>Gets whether value labels may consume cells.</summary>
    public bool ShowValueLabels { get; }

    /// <summary>Gets the resolved chart style.</summary>
    public ChartStyle ActualStyle { get; }

    /// <summary>Validates a complete prospective series membership snapshot.</summary>
    public void ValidateSeries(IReadOnlyList<ChartSeries> series) => ArgumentNullException.ThrowIfNull(series);

    /// <summary>Publishes one observed model change onto the owning control.</summary>
    public void OnChartDataChanged(InvalidationImpact impact, bool seriesMembershipChanged);

    /// <summary>Publishes one changed common chart property.</summary>
    public void OnChartPropertyChanged(string propertyName, InvalidationImpact impact);
}
