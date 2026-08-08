# Sparkline

## Overview

`Sparkline` compresses one ordered series into fractional lower-block cells. It
is intended for compact trend context rather than labeled comparison.

## API

`Series` accepts zero or one `ChartSeries`; a second series is rejected before
state changes. `Scale` defaults automatic without forcing zero. The control has
no legend, category-label, or value-label properties.

## Example

![The Sparkline control rendered in the live showcase](../../images/controls/sparkline.png)

```csharp
var sparkline = new Sparkline
{
    Series = [new ChartSeries("Load", points)],
    Width = Length.Cells(20),
};
```

## Expected behavior

The most recent points that fit are rendered. Columns rasterize through the
canvas fractional-bar primitive: eight fractional levels provide sub-cell
vertical resolution, and taller bounds use full cells beneath the fraction. A
style with `ChartFillMode.Glyph` rounds each column to whole cells of the
style's own bar glyph instead. Empty data leaves the content clear. Resize and
deep value changes repaint without replacing the series.
