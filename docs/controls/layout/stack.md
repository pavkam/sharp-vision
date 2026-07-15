# Stack

## Stack contract

`Stack` arranges managed children sequentially on a vertical or horizontal axis.
Child order is render, navigation, and default z-order.

## API

- `Children` rejects nulls, duplicates, cycles, and already parented controls.
- `Orientation` defaults vertical and accepts vertical or horizontal.
- `Spacing` defaults zero and is a non-negative cell count between non-collapsed
  children. Hidden children participate; collapsed children do not consume a
  track or adjacent spacing.
- `Reverse` defaults false and changes geometry, rendering, and default focus
  navigation consistently without reparenting children. Elevated popup
  descendants follow that same order, so reversing the stack also reverses popup
  drawing and hit priority.

Along the stack axis, automatic children receive intrinsic space and
proportional children divide remaining arranged space. Percentages resolve once
against the final inner stack size; fixed, percentage, automatic, and
proportional border-box extents are then allocated after external margins and
saturated spacing reserve their cells. When minimums cannot fit, containment
wins and later tracks may shrink to zero. Overflow follows ancestor clipping or
scrolling policy.

The cross axis uses the ordinary child length and alignment contract. Layout
uses pooled temporary track storage and clears retained child references before
returning it.

## Example

```csharp
var actions = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
actions.Children.Add(primaryAction);
actions.Children.Add(cancelAction);
```

## Test obligations

Cover every orientation, spacing, reverse, fixed/percent/auto/proportional mix,
collapsed children, alignment, zero/tiny sizes, overflow, resize, ownership,
navigation order, Unicode measurement, and exact bounds/cells.
