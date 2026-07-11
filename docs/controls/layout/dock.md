# Dock

## Dock contract

`Dock` consumes left, top, right, or bottom edges in child order and optionally
assigns the remaining rectangle to the final visible child.

## API

- `Children` follows managed ownership.
- Attached `Side` accepts left, top, right, or bottom.
- `LastChildFills` defaults true.
- `Spacing` is a non-negative cell gap after each consumed child.

Each child measures against the remaining candidate size. Arrange saturates the
remaining rectangle at zero; no negative bounds are produced when children
request more than available.

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
