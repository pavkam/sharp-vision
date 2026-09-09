# CartesianChartControlBase

## Overview

`CartesianChartControlBase` is the abstract authoring role for full charts with
category labels, numeric value labels, legends, and an optional visible zero
axis. Applications normally instantiate one of its four concrete chart types.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- ChartControlBase
    ChartControlBase <|-- CartesianChartControlBase
    CartesianChartControlBase <|-- HorizontalBarChart
    CartesianChartControlBase <|-- VerticalBarChart
    CartesianChartControlBase <|-- LineChart
    CartesianChartControlBase <|-- AreaChart
```

## API

| Member               | Type                                           | Default     | Description                                                                         |
| -------------------- | ---------------------------------------------- | ----------- | ----------------------------------------------------------------------------------- |
| `Series`             | `IReadOnlyList<ChartSeries>`                   | Empty       | Borrowed observable series source validated before membership changes.              |
| `Scale`              | `ChartScale`                                   | Family set  | Authored finite bounds and zero-inclusion policy.                                   |
| `Selection`          | `ChartSelection?`                              | `null`      | Selected point; indices outside current data throw before mutation.                 |
| `SelectionChanged`   | `EventHandler<ChartSelectionChangedEventArgs>` | —           | Raised after a changed selection commits.                                           |
| `LegendPlacement`    | `ChartLegendPlacement`                         | `Automatic` | Legend policy; an undefined enum value throws before mutation.                      |
| `ShowCategoryLabels` | `bool`                                         | `true`      | Whether category labels reserve plot cells when space permits.                      |
| `ShowValueLabels`    | `bool`                                         | `false`     | Whether complete formatted values draw when they fit without replacing data.        |
| `ShowZeroAxis`       | `bool`                                         | `true`      | Whether an axis rule draws when zero is strictly inside the resolved range.         |
| `ValueLabelFormat`   | `string`                                       | `"G"`       | Invariant numeric format; null or an invalid numeric format throws before mutation. |
| `Style`              | `ChartStyle?`                                  | `null`      | Complete local presentation, or null for theme and code-owned fallback.             |
| `ActualStyle`        | `ChartStyle`                                   | Resolved    | Read-only resolved presentation, including `SelectionDecoration`.                   |

The [shared chart API](index.md#api) documents model observation, selection
repair, scaling, and binding.

The intrinsic desired size is 30 by 10 cells. Parent layout may arrange any
other size.

### Authoring seam

`ChartControlBase` declares the following protected members for a chart authored
directly against either base role, rather than through one of the five shipped
chart types:

| Member                                                              | Type                   | Default    | Description                                                                                                            |
| ------------------------------------------------------------------- | ---------------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------- |
| `CreateRenderContext(TerminalCanvas canvas)`                        | `ChartRenderContext`   | —          | Resolves the plot layout, numeric range, and inherited style for one render pass, drawing the legend as a side effect. |
| `LegendPlacementCore`                                               | `ChartLegendPlacement` | Family set | The authored value `LegendPlacement` forwards on this role; see below.                                                 |
| `ShowCategoryLabelsCore`                                            | `bool`                 | Family set | The authored value `ShowCategoryLabels` forwards on this role; see below.                                              |
| `ShowValueLabelsCore`                                               | `bool`                 | Family set | The authored value `ShowValueLabels` forwards on this role; see below.                                                 |
| `CategoriesAreVertical`                                             | `bool`                 | `false`    | Protected virtual; whether keyboard category navigation advances vertically instead of horizontally.                   |
| `TryHitTestSelection(Point position, out ChartSelection selection)` | `bool`                 | —          | Protected virtual; maps a pointer cell to the nearest selectable visible point.                                        |

`LegendPlacementCore`, `ShowCategoryLabelsCore`, and `ShowValueLabelsCore` stay
protected seams distinct from the public properties above them because the two
authoring patterns this base class supports diverge here: this role forwards
each one-to-one as a public settable property, while a fixed-policy family such
as [`Sparkline`](sparkline.md) instead overrides the corresponding `Resolve*`
method (`ResolveLegendPlacement`, `ResolveShowCategoryLabels`,
`ResolveShowValueLabels`) and exposes no public surface for it at all. A
third-party chart picks whichever pattern its own policy needs.

`ChartRenderContext` is a public readonly struct exposing `Layout`
(`ChartPlotLayout`, with `Plot` and `Legend` rectangles), `Range`
(`ChartScaleRange`, with `Minimum` and `Maximum`), `InheritedStyle`, and the
mapping methods `MapX`, `MapY`, `Ratio`, and `ResolveSeriesStyle`.
`MapX(int index, int count)` and `MapY(double value)` map one data point into
`Layout`'s plot rectangle; `Ratio(double value)` normalizes a value into `Range`
alone. `ResolveSeriesStyle` takes a `ChartSeries`, a `ChartDataPoint`, the
series index, and an optional point index, and resolves the point's terminal
style, including selection decoration. Only `CreateRenderContext` constructs a
context; a third-party chart never builds its own.

## Keyboard

| Key          | Behavior                                                          |
| ------------ | ----------------------------------------------------------------- |
| Arrow keys   | Select the first point, then move along category and series axes. |
| `Home`/`End` | Select the first or last point in the current series.             |
| `Escape`     | Clear selection; bubble when there is nothing to clear.           |

A primary pointer press focuses the chart and selects the nearest visible point
or bar lane. Wheel input is not consumed and can continue to an enclosing
scrolling container. An arrow on the series axis is likewise left unhandled when
fewer than two series contain points. Disabled or hidden charts do not change
selection.

## Example

An author-defined Cartesian chart derives from this role and implements only its
geometry and content rendering, resolving one `ChartRenderContext` through
`CreateRenderContext` and mapping every point through it:

```csharp
public sealed class RangeChart : CartesianChartControlBase
{
    public RangeChart() : base(ChartScale.Automatic)
    {
    }

    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var context = CreateRenderContext(canvas);

        for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
        {
            var series = Series[seriesIndex];

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                var point = series.Points[pointIndex];
                var position = new Point(
                    context.MapX(pointIndex, series.Points.Count),
                    context.MapY(point.Value));
                var style = context.ResolveSeriesStyle(series, point, seriesIndex, pointIndex);
                canvas.DrawRune(new Rune('*'), position, style);
            }
        }
    }
}
```

## Expected behavior

| Scope      | Observable evidence                                                          |
| ---------- | ---------------------------------------------------------------------------- |
| Public API | Shared options, validated selection, input routing, and render invalidation. |

- The role centralizes every public presentation option shared by full charts;
  concrete chart controls do not redeclare those properties.
- Selection changes repaint without forcing measure and remain synchronized by
  point identity through observable collection moves.
- Input uses exact unmodified navigation commands and preserves wheel bubbling.
