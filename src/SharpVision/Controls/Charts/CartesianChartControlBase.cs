// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Provides legend, axis, and label policy shared by full Cartesian chart controls.</summary>
[PublicAPI]
public abstract class CartesianChartControlBase: ChartControlBase
{
    /// <summary>Initializes one full chart with automatic legend and category-label presentation.</summary>
    /// <param name="scale">The authored scale bounds and zero-inclusion policy.</param>
    protected CartesianChartControlBase(ChartScale scale) :
        base(scale, ChartLegendPlacement.Automatic, showCategoryLabels: true, showValueLabels: false)
    {
    }

    /// <summary>Gets or sets legend placement and automatic visibility.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned placement is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public ChartLegendPlacement LegendPlacement
    {
        get => LegendPlacementCore;
        set => LegendPlacementCore = value;
    }

    /// <summary>Gets or sets whether category labels consume plot cells when they fit.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public bool ShowCategoryLabels
    {
        get => ShowCategoryLabelsCore;
        set => ShowCategoryLabelsCore = value;
    }

    /// <summary>Gets or sets whether numeric point values are drawn when they fit.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public bool ShowValueLabels
    {
        get => ShowValueLabelsCore;
        set => ShowValueLabelsCore = value;
    }

    /// <summary>Gets or sets whether a zero axis is drawn when zero falls strictly inside the plot.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public bool ShowZeroAxis
    {
        get;
        set => SetChartProperty(ref field, value, InvalidationImpact.Render, nameof(ShowZeroAxis));
    } = true;

    /// <summary>Gets or sets the invariant numeric format used by visible value labels.</summary>
    /// <exception cref="ArgumentNullException">The assigned format is null.</exception>
    /// <exception cref="ArgumentException">The assigned format is not a valid numeric format.</exception>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public string ValueLabelFormat
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            try
            {
                _ = 0d.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("The value-label format is invalid.", nameof(value), exception);
            }

            SetChartProperty(ref field, value, InvalidationImpact.Render, nameof(ValueLabelFormat));
        }
    } = "G";

    /// <inheritdoc/>
    protected override bool ResolveShowZeroAxis() => ShowZeroAxis;

    /// <inheritdoc/>
    protected override string ResolveValueLabelFormat() => ValueLabelFormat;

    /// <inheritdoc/>
    protected override Size DefaultSize => new(30, 10);
}
