# Dock

## Dock contract

`Dock` consumes left, top, right, or bottom edges in child order and optionally
assigns the remaining rectangle to the final visible child.

## API

- `Children` follows managed ownership.
- Attached `Side` defaults left and accepts left, top, right, or bottom.
- `LastChildFills` defaults true.
- `Spacing` defaults zero and is a non-negative cell gap after each consumed
  non-final child.

Each child measures against the remaining candidate size. Arrange saturates the
remaining rectangle at zero; no negative bounds are produced when children
request more than available.

Left/right children resolve width against the current remaining width and use
ordinary height/alignment across it; top/bottom children do the converse.
Percentages therefore resolve against each iteration's remaining axis, not the
original panel. The last non-collapsed child fills both resolved axes when
`LastChildFills` is true. Collapsed children consume neither an edge nor
spacing.

Changing an attached side validates the enum and dispatcher affinity before
mutation, then invalidates measure only when the child belongs to a Dock.

## Example

```csharp
var shell = new Dock { LastChildFills = true };
Dock.SetSide(sidebar, DockSide.Left);
shell.Children.Add(sidebar);
shell.Children.Add(main);
```

## Test obligations

Cover all sides, ordering, fill on/off, spacing, fixed/percent/auto sizes,
over-consumption, collapsed children, zero/tiny bounds, resize, ownership,
navigation order, clipping, and exact bounds/cells.
