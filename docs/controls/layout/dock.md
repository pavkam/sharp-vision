# Dock

## Overview

`Dock` lays its children out by consuming space from the panel's edges. Each
child docks against the left, top, right, or bottom edge in collection order,
and the last visible child can optionally take whatever rectangle remains.

## API

| Member           | Default   | Purpose                                                         |
| ---------------- | --------- | --------------------------------------------------------------- |
| `Children`       | Empty     | Owns controls in edge-consumption order.                        |
| Attached `Side`  | `Left`    | Selects the physical edge consumed by one child.                |
| `LastChildFills` | `true`    | Gives the final non-collapsed child the remaining rectangle.    |
| `Spacing`        | `0` cells | Inserts a non-negative gap after each consumed non-final child. |

## Behavior

- `Children` follows managed ownership.
- The attached `Side` property defaults to left and accepts left, top, right, or
  bottom.
- `LastChildFills` defaults to `true`.
- `Spacing` defaults to zero. It is a non-negative gap, in cells, inserted after
  each docked child except the final one.

Each child measures against whatever space is still available. Arrangement
saturates the remaining rectangle at zero, so children that request more than is
available never produce negative bounds.

Children docked left or right resolve their width against the current remaining
width and use the ordinary height and alignment rules across it; children docked
top or bottom do the converse. Percentages therefore resolve against the space
remaining at that child's turn, not against the original panel size. When
`LastChildFills` is true, the last non-collapsed child fills both remaining
axes. Collapsed children consume neither an edge nor spacing.

A Star-sized child shares its axis's leftover space with its Star siblings in
proportion to weight, exactly as `Grid` and `StackPanel` split a Star track:
once every fixed, percent, and automatic sibling on the same axis is resolved,
the remainder is divided among the Star children by weight. A Star's
`MinWidth`/`MaxWidth` (or `MinHeight`/`MaxHeight`) reserves or clips only that
Star's own share, and the reserved or clipped amount is redistributed among the
remaining eligible Star siblings rather than dropped or taken from an unrelated
child. Non-Star siblings resolve against the axis with every Star's consumption
excluded from their own basis — a Star claims whatever the non-Star siblings
collectively leave. If a later Percent sibling could see a Star's real rendered
share, the Percent's resolution would depend on a value that in turn depends on
it, with no stable answer. Declaring a Star before or after a Percent sibling
that claims the same nominal share therefore produces the same split either way.

Setting the attached side validates the enum value and dispatcher affinity
before any state changes, and it invalidates measure only when the child
currently belongs to a Dock.

## Example

![The Dock control rendered in the live showcase](../../images/controls/dock.png)

```csharp
var shell = new Dock { LastChildFills = true, Spacing = 1 };
Dock.SetSide(sidebar, DockSide.Left);
shell.Children.Add(sidebar);
shell.Children.Add(main);
```

## Expected behavior

Dock layout is deterministic across every side, child order, and size mix.
Fixed, percentage, and automatic lengths resolve as described above,
over-consuming children saturate at the panel bounds instead of producing
negative geometry, collapsed children are skipped entirely, and zero or tiny
panels stay well-defined. Fill on and off, spacing, resize reflow, managed
ownership, collection-order navigation, clipping, and the exact committed bounds
and cells are all observable guarantees.

Mounted cross-layer coverage in
[`DockSurfaceTests`](../../../tests/SharpVision.Tests/Controls/Layout/DockSurfaceTests.cs)
demonstrates consumption of all four edges, final-child fill, exact region
cells, a real pointer hit target inside the fill child, reflow when the same
instance is resized, and removal of cells an earlier layout no longer owns.
