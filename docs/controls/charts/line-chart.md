# LineChart

## Overview

`LineChart` presents ordered values as connected colored series with visible,
selectable point cells.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- ChartControlBase
    ChartControlBase <|-- CartesianChartControlBase
    CartesianChartControlBase <|-- LineChart
```

## API

| Member                       | Type                                           | Default                   | Description                                                          |
| ---------------------------- | ---------------------------------------------- | ------------------------- | -------------------------------------------------------------------- |
| `Series`                     | `IReadOnlyList<ChartSeries>`                   | Empty                     | Borrowed observable series validated before membership changes.      |
| `Scale`                      | `ChartScale`                                   | Automatic (zero excluded) | Authored bounds; excluding zero preserves small trend changes.       |
| `Selection`                  | `ChartSelection?`                              | `null`                    | Selected marker by series and point indices.                         |
| `SelectionChanged`           | `EventHandler<ChartSelectionChangedEventArgs>` | —                         | Raised after selection changes or clears.                            |
| `LegendPlacement`            | `ChartLegendPlacement`                         | `Automatic`               | Legend location; undefined values throw before mutation.             |
| `ShowCategoryLabels`         | `bool`                                         | `true`                    | Whether category labels reserve one plot row.                        |
| `ShowValueLabels`            | `bool`                                         | `false`                   | Whether complete formatted values draw beside markers when they fit. |
| `ShowZeroAxis`               | `bool`                                         | `true`                    | Whether an interior zero rule draws.                                 |
| `ValueLabelFormat`           | `string`                                       | `"G"`                     | Invariant numeric format validated before mutation.                  |
| Inherited `IsHitTestVisible` | `bool`                                         | `true`                    | Allows nearest-point pointer selection.                              |
| `Style`                      | `ChartStyle?`                                  | `null`                    | Complete local chart presentation.                                   |
| `ActualStyle`                | `ChartStyle`                                   | Resolved                  | Read-only resolved presentation.                                     |

The [shared chart API](index.md#api) documents model observation, binding,
selection repair, and `ChartStyle.SelectionDecoration`.

The intrinsic desired size is 30 by 10 cells. Parent layout may arrange any
other size.

## Keyboard

| Key            | Behavior                                                |
| -------------- | ------------------------------------------------------- |
| `Left`/`Right` | Select the first point, then move through the series.   |
| `Up`/`Down`    | Move to the same or nearest point in another series.    |
| `Home`/`End`   | Select the first or last point in the current series.   |
| `Escape`       | Clear selection; bubble when there is nothing to clear. |

A primary pointer press focuses the chart and selects the plotted point nearest
the clicked plot cell. Wheel input continues to an enclosing scroll host.

## Example

![The LineChart control rendered in the live showcase](../../images/controls/line-chart.png)

```csharp
var chart = new LineChart
{
    Series = [new ChartSeries("CPU", points)],
    LegendPlacement = ChartLegendPlacement.Bottom,
    ShowValueLabels = true,
};
```

## Expected behavior

| Scope      | Observable evidence                                                  |
| ---------- | -------------------------------------------------------------------- |
| Public API | Validation, focus, selection, deterministic geometry, and rendering. |

- Point order defines horizontal position and finite value defines vertical
  position. Values outside explicit bounds clip to the nearest plot edge.
- Quadrant mode rasterizes deterministic half-cell line segments. Glyph mode
  uses the authored line glyph in whole cells.
- Series dash phases remain continuous through every segment. Glyph mode ignores
  dash patterns because its authored glyph owns appearance.
- Category labels clip inside disjoint bands instead of overwriting neighbors.
  Point markers remain visible over segments and selected markers receive the
  configured decoration.
