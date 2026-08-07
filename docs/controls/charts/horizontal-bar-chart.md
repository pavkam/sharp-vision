# HorizontalBarChart

## Overview

`HorizontalBarChart` compares category values as grouped horizontal bars.
Positive and negative values grow in opposite directions from the resolved zero
baseline.

## API

The control uses the
[shared chart data, scale, color, legend, and binding API](index.md#api).
`Series` defaults empty, `Scale` defaults to `ChartScale.Automatic`,
`LegendPlacement` defaults to `Automatic`, category labels default visible, and
value labels default hidden.

## Example

```csharp
var chart = new HorizontalBarChart
{
    Series = [new ChartSeries("Change", [
        new ChartDataPoint("North", 8),
        new ChartDataPoint("South", -3),
    ])],
    ShowValueLabels = true,
};
```

## Expected behavior

Categories consume vertical bands, series share each category band, and every
bar is clipped to the plot. Labels are removed when they would leave no useful
bar width. Empty and zero values render safely without inventing magnitude.
