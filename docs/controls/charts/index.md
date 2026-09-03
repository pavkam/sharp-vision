# Charts

## Overview

SharpVision charts are retained, focusable controls that turn finite labeled
values into semantic terminal cells. `HorizontalBarChart`, `VerticalBarChart`,
`LineChart`, `AreaChart`, and `Sparkline` share observable data, automatic
scaling, deterministic colors, selection, and responsive clipping. Controls
render through the cell canvas and never emit terminal protocol bytes.

## API

`ChartSeries` has a non-null `Name`, optional `Color`, optional `LinePattern`,
and observable `Points`. Each `ChartDataPoint` has a non-null `Label`, finite
`Value`, and optional `Color`. Point color overrides series color, which
overrides the deterministic six-color style palette. A series `LinePattern`
overrides `ChartStyle.LinePattern` in quadrant line mode.

All charts expose `Series`, `Scale`, nullable `Selection`, `SelectionChanged`,
`Style`, and `ActualStyle` through `ChartControlBase`. A `ChartSelection`
identifies one point by zero-based series and point indices. Assigned indices
are validated before state changes. Observable collection moves preserve the
same selected point by reference and update its indices; removing the selected
series or point clears selection.

`CartesianChartControlBase` adds `LegendPlacement`, `ShowCategoryLabels`,
`ShowValueLabels`, `ShowZeroAxis`, and `ValueLabelFormat` for the four full
charts. `ValueLabelFormat` is an invariant numeric format. Invalid formats are
rejected before replacing the current value. `Sparkline` intentionally omits
legend, label, zero-axis, and value-format properties because it is an inline
trend mark rather than a labeled comparison.

`ChartStyle.SelectionDecoration` controls the complete terminal attributes of
the selected bar, point marker, or sparkline column. Its default is reverse
video, which stays visible across light and dark themes. Selected bars and
sparkline columns also replace their endpoint cell with the chart point glyph,
so selection remains distinguishable when a terminal does not make reversed
full-block cells visually obvious. `AxisColor`, `LabelColor`, six series colors,
`Glyphs`, `FillMode`, `LineMode`, and `LinePattern` provide the rest of the
chart presentation.

`ChartControlBase` owns data observation, dispatcher validation, selection,
focus, input routing, and the common style slot. `CartesianChartControlBase`
owns the presentation options shared by bar, line, and area charts. Concrete
controls supply only family geometry and rendering. See
[CartesianChartControlBase](cartesian-chart-control-base.md) for its authoring
contract.

`ChartScale` accepts optional finite `Minimum` and `Maximum` bounds and an
`IncludeZero` policy. `ChartScale.Automatic` includes zero. Bar charts use that
default; line, area, and sparkline controls leave zero optional so small trends
remain visible. Empty and constant data still resolve to finite, non-empty
ranges. Explicit bounds clip values without changing the model.

`ChartLegendPlacement.Automatic` shows a bottom legend for two or more named
series. `Hidden`, `Top`, `Bottom`, `Left`, and `Right` provide explicit policy.
Every visible legend edge is separated from plot cells by an axis-colored rule.
Legends yield to the plot when bounds are too small for a complete entry and
divider.

Category labels own disjoint bands, so neighboring text cannot overwrite one
another. Bar bands reserve a one-cell gutter whenever all visible series lanes
still fit. A value label is drawn only when the complete number fits without
replacing data cells. `ShowZeroAxis` draws an axis rule only when zero lies
strictly inside the resolved numeric range.

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

Source replacement, observable membership changes, and deep series or point
changes update the existing chart. A null bound source becomes an empty chart.
Once attached, notifications must be raised on the chart dispatcher. An
off-dispatcher notification throws synchronously instead of racing borrowed
collection enumeration. Disposed charts release every subscription.

> [!WARNING]
>
> The synchronous dispatcher exception propagates out of the model's own event
> dispatch. Marshal attached chart model changes to the dispatcher before
> mutating an observable collection or raising `PropertyChanged`.

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
chart.SelectionChanged += (_, args) => ShowDetails(args.Selection);
```

## Expected behavior

Applications can rely on validation before observable mutation, finite ranges,
dispatcher-ordered updates, and deterministic keyboard and pointer selection.
Tiny bounds suppress optional labels and legends before data. Text clips at
complete grapheme boundaries. Half-cell lines, disjoint category bands, and
eighth-cell bar extents use widened arithmetic and clamp at terminal drawing
boundaries instead of wrapping.
