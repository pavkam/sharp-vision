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

![The HorizontalBarChart control rendered in the live showcase](../../images/controls/horizontal-bar-chart.png)

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
bar is clipped to the plot. The same `ChartStyle.FillMode` contract as
[VerticalBarChart](vertical-bar-chart.md) applies along the horizontal axis:
fractional bars fill their band's rows and end on an eighth-cell boundary, and
the glyph mode keeps one-cell whole-rounded bars. A value label that would not
fit beyond its bar clamps inside the plot and draws over the bar's tail rather
than disappearing. Labels are removed when they would leave no useful bar width.
Empty and zero values render safely without inventing magnitude.
