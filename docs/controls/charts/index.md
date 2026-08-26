# Charts

## Overview

SharpVision charts are passive retained controls that turn finite labeled values
into semantic terminal cells. `HorizontalBarChart`, `VerticalBarChart`,
`LineChart`, `AreaChart`, and `Sparkline` share observable data, automatic
scaling, deterministic colors, and responsive clipping. They never emit terminal
protocol bytes and do not handle pointer or keyboard input.

## API

`ChartSeries` has a non-null `Name`, optional `Color`, optional `LinePattern`,
and observable `Points`. Each `ChartDataPoint` has a non-null `Label`, finite
`Value`, and optional `Color`. A point color overrides its series color; a
series color overrides the control's deterministic theme palette. A series
`LinePattern` overrides `ChartStyle.LinePattern`, the dash pattern line series
draw with in `Quadrant` mode; see [LineChart](line-chart.md) for how the pattern
renders.

Full charts expose `Series`, `Scale`, `LegendPlacement`, `ShowCategoryLabels`,
and `ShowValueLabels`. `Sparkline` exposes only `Series` and `Scale`, accepts at
most one series, and intentionally has no legend or label properties.

`ChartScale` accepts optional finite `Minimum` and `Maximum` bounds and an
`IncludeZero` policy. `ChartScale.Automatic` includes zero. Bar charts use that
default; line, area, and sparkline controls leave zero optional so small trends
remain visible. Empty data resolves to `0..1`, and a constant automatic range is
expanded symmetrically.

`ChartLegendPlacement.Automatic` shows a bottom legend for two or more named
series. `Hidden`, `Top`, `Bottom`, `Left`, and `Right` provide explicit policy.
Each legend entry renders its color marker, one blank cell, and its series name.
Bottom legends are separated from plot cells by a horizontal axis line. Legends
yield to the plot when bounds are too small for a complete entry or separator.

Bar charts render an axis boundary between category labels and plot cells.
Horizontal positive-only bars begin in the first cell after that boundary;
mixed-sign bars render a zero baseline and grow away from it. Vertical bars
render a horizontal category axis above complete centered labels. Line and area
category labels are likewise centered and clipped as complete text rather than
reduced to their first cell. A horizontal bar value label retains one blank cell
between the bar edge and its text; it is suppressed when that complete gap and
label do not fit.

One-way binding uses the ordinary strongly typed extension:

```csharp
var model = new ChartExampleModel();
var chart = new LineChart();
using var binding = chart.Bind(model, source => source.Series);

internal sealed class ChartExampleModel
{
    public IReadOnlyList<ChartSeries>? Series { get; set; }
}
```

The source property is `IReadOnlyList<ChartSeries>?`. Source replacement and
observable collection changes update membership; changes to series, point
collections, labels, values, or colors repaint the existing chart. A null bound
source becomes an empty chart.

## Example

```csharp
var requests = new ChartSeries("Requests", [
    new ChartDataPoint("Mon", 42),
    new ChartDataPoint("Tue", 51),
    new ChartDataPoint("Wed", 47),
]);

var chart = new LineChart
{
    Series = [requests],
    LegendPlacement = ChartLegendPlacement.Bottom,
};
```

## Expected behavior

Applications can rely on validation occurring before observable mutation,
automatic ranges remaining finite and non-empty, observable membership and deep
item changes invalidating the required UI phase, and removed or disposed data
releasing chart subscriptions. Explicit bounds clip values rather than changing
the model. Tiny bounds suppress optional labels and legends before data, and all
text remains clipped at complete grapheme boundaries. Half-cell line
coordinates, category partitions, and eighth-cell bar extents use widened
intermediates and clamp at the terminal drawing boundary, so valid integer-sized
plot geometry remains monotonic instead of wrapping.
