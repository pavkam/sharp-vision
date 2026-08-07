# Sparkline

## Overview

`Sparkline` compresses one ordered series into fractional lower-block cells. It
is intended for compact trend context rather than labeled comparison.

## API

`Series` accepts zero or one `ChartSeries`; a second series is rejected before
state changes. `Scale` defaults automatic without forcing zero. The control has
no legend, category-label, or value-label properties.

## Example

```csharp
var sparkline = new Sparkline
{
    Series = [new ChartSeries("Load", points)],
    Width = Length.Cells(20),
};
```

## Expected behavior

The most recent points that fit are rendered. Eight fractional levels provide
sub-cell vertical resolution, taller bounds use full cells beneath the fraction,
and empty data leaves the content clear. Resize and deep value changes repaint
without replacing the series.
