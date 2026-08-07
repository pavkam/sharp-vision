# AreaChart

## Overview

`AreaChart` presents ordered values as connected lines with colored fill toward
the visible zero baseline.

## API

The control uses the [shared chart API](index.md#api) and the same non-zero
automatic scale as `LineChart`.

## Example

```csharp
var chart = new AreaChart
{
    Series = [requests, latency],
};
```

## Expected behavior

When zero is visible, each column fills between its point and zero. When an
explicit range excludes zero, fill proceeds toward the nearest plot edge.
Connections and point glyphs remain visible over the fill, and multiple series
retain deterministic color precedence.
