# VerticalBarChart

## Overview

`VerticalBarChart` compares category values as grouped vertical bars rising or
falling from a resolved numeric baseline.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- ChartControlBase
    ChartControlBase <|-- CartesianChartControlBase
    CartesianChartControlBase <|-- VerticalBarChart
```

## API

| Member                       | Type                                           | Default                | Description                                                      |
| ---------------------------- | ---------------------------------------------- | ---------------------- | ---------------------------------------------------------------- |
| `Series`                     | `IReadOnlyList<ChartSeries>`                   | Empty                  | Borrowed observable series validated before membership changes.  |
| `Scale`                      | `ChartScale`                                   | `ChartScale.Automatic` | Authored bounds and zero-inclusion policy.                       |
| `Selection`                  | `ChartSelection?`                              | `null`                 | Selected bar by series and category indices.                     |
| `SelectionChanged`           | `EventHandler<ChartSelectionChangedEventArgs>` | —                      | Raised after selection changes or clears.                        |
| `LegendPlacement`            | `ChartLegendPlacement`                         | `Automatic`            | Legend location; undefined values throw before mutation.         |
| `ShowCategoryLabels`         | `bool`                                         | `true`                 | Whether labels and their separating axis reserve cells.          |
| `ShowValueLabels`            | `bool`                                         | `false`                | Whether complete formatted values draw above bars when they fit. |
| `ShowZeroAxis`               | `bool`                                         | `true`                 | Whether an interior zero baseline draws.                         |
| `ValueLabelFormat`           | `string`                                       | `"G"`                  | Invariant numeric format validated before mutation.              |
| Inherited `IsHitTestVisible` | `bool`                                         | `true`                 | Allows primary pointer selection.                                |
| `Style`                      | `ChartStyle?`                                  | `null`                 | Complete local chart presentation.                               |
| `ActualStyle`                | `ChartStyle`                                   | Resolved               | Read-only resolved presentation.                                 |

The [shared chart API](index.md#api) documents model observation, binding,
selection repair, and `ChartStyle.SelectionDecoration`.

The intrinsic desired size is 30 by 10 cells. Parent layout may arrange any
other size.

## Keyboard

| Key            | Behavior                                                 |
| -------------- | -------------------------------------------------------- |
| `Left`/`Right` | Select the first bar, then move between categories.      |
| `Up`/`Down`    | Move between series lanes for the current category.      |
| `Home`/`End`   | Select the first or last category in the current series. |
| `Escape`       | Clear selection; bubble when there is nothing to clear.  |

A primary pointer press focuses the chart and selects the nearest bar lane in
the clicked category band. Wheel input continues to an enclosing scroll host.

## Example

![The VerticalBarChart control rendered in the live showcase](../../images/controls/vertical-bar-chart.png)

```csharp
var chart = new VerticalBarChart
{
    Series = [current, previous],
    LegendPlacement = ChartLegendPlacement.Bottom,
    Selection = new ChartSelection(0, 0),
};
```

## Expected behavior

| Scope      | Observable evidence                                                  |
| ---------- | -------------------------------------------------------------------- |
| Public API | Validation, focus, selection, deterministic geometry, and rendering. |

- Categories own disjoint horizontal bands. Series divide each band and keep a
  blank gutter whenever all visible lanes still fit.
- Fractional bars end on eighth-cell boundaries. Glyph mode keeps one-cell
  whole-rounded bars using the authored bar glyph.
- Category labels stay inside their bands below a distinct category axis.
- An enabled zero axis renders consistently for mixed-sign data and never at a
  range edge.
- The selected bar uses `SelectionDecoration` and a point glyph at its value
  endpoint, including for full-block fills.
