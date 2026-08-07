# VerticalBarChart

## Overview

`VerticalBarChart` compares category values as grouped vertical bars rising or
falling from the resolved zero baseline.

## API

The control uses the [shared chart API](index.md#api). Its defaults match
`HorizontalBarChart`: automatic zero-inclusive scaling, automatic legend,
visible category labels, and hidden value labels.

## Example

```csharp
var chart = new VerticalBarChart
{
    Series = [current, previous],
    LegendPlacement = ChartLegendPlacement.Bottom,
};
```

## Expected behavior

Categories consume horizontal bands and multiple series remain visually distinct
through explicit or palette colors. Category labels yield before the plot
becomes empty, and bars always stay inside arranged bounds.
