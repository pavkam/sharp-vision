# LineChart

## Overview

`LineChart` presents ordered values as connected colored series with visible
point cells.

## API

The control uses the [shared chart API](index.md#api). Its automatic scale does
not force zero, preserving small changes around a large value. Callers can use
an explicit `ChartScale` when comparisons require fixed bounds.

## Example

```csharp
var chart = new LineChart
{
    Series = [new ChartSeries("CPU", points)],
    LegendPlacement = ChartLegendPlacement.Bottom,
};
```

## Expected behavior

Point order defines the horizontal coordinate, finite values define the vertical
coordinate, and connections use deterministic cell geometry. Point glyphs remain
visible over connecting cells. Values outside explicit bounds are clipped to the
nearest plot edge.
