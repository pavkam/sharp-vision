# VerticalBarChart

## Overview

`VerticalBarChart` compares category values as grouped vertical bars rising or
falling from the resolved zero baseline.

## API

The control uses the [shared chart API](index.md#api). Its defaults match
`HorizontalBarChart`: automatic zero-inclusive scaling, automatic legend,
visible category labels, and hidden value labels.

## Example

![The VerticalBarChart control rendered in the live showcase](../../images/controls/vertical-bar-chart.png)

```csharp
var chart = new VerticalBarChart
{
    Series = [current, previous],
    LegendPlacement = ChartLegendPlacement.Bottom,
};
```

## Expected behavior

Categories consume horizontal bands and multiple series remain visually distinct
through explicit or palette colors. By default (`ChartStyle.FillMode` of
`Fractional`) the series divide each band into bars as thick as the band
affords, keeping a one-cell gutter between categories when there is room, and a
bar's height ends on an eighth-cell boundary rasterized from the shared zero
baseline. A style with `ChartFillMode.Glyph` keeps one-cell bars of the style's
own bar glyph rounded to whole cells. Category labels yield before the plot
becomes empty, and bars always stay inside arranged bounds.
