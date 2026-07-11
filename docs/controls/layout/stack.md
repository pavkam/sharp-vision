# Stack

## Stack contract

`Stack` arranges managed children sequentially on a vertical or horizontal axis.
Child order is render, navigation, and default z-order.

## API

- `Children` rejects nulls, duplicates, cycles, and already parented controls.
- `Orientation` is vertical or horizontal.
- `Spacing` is a non-negative cell count between visible non-collapsed children.
- `Reverse` changes visual order and default navigation consistently.

Along the stack axis, automatic children receive intrinsic space and
proportional children divide remaining arranged space. Percentages resolve
against the final inner stack size; overflow follows parent clipping/scrolling.

## Example

```csharp
var actions = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
actions.Children.Add(new Button { Content = new Text("Save") });
actions.Children.Add(new Button { Content = new Text("Cancel") });
```

## Test obligations

Cover every orientation, spacing, reverse, fixed/percent/auto/proportional mix,
collapsed children, alignment, zero/tiny sizes, overflow, resize, ownership,
navigation order, Unicode measurement, and exact bounds/cells.
