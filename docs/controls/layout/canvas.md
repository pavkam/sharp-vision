# Canvas

## Canvas contract

`Canvas` positions managed children by optional left/top/right/bottom offsets
and explicit or intrinsic sizes. It is intended for overlays and diagrams, not
general responsive layout.

## API

- `Children` follows managed ownership.
- Attached offsets are finite cell or percentage lengths; contradictory pairs
  stretch only when the corresponding child size is automatic.
- `ClipToBounds` defaults true.

Offsets are nullable and default unset. `Auto` and `Star` offsets throw before
mutation; cells and percentages are valid. Attached changes validate dispatcher
affinity and invalidate measure only when the child belongs to a Canvas.

Measure reports the finite union of positioned intrinsic children. Percentage
offsets defer under unbounded measure and resolve against final content size.
Negative final coordinates are allowed but clipped by the parent policy.

Fixed offsets contribute to intrinsic union; percentage offsets contribute zero
until arrange. Left/top take precedence when both opposing offsets exist with an
explicit child size. With an automatic size, opposing offsets define the
resolved outer slot. Right/bottom placement may produce a negative origin when
the child is larger than the final content box.

Rendering and hit testing use collection z-order. `ClipToBounds = false` retains
the ancestor clip for descendants, while the Canvas's own drawing remains
clipped. `IsHitTestVisible` allows a top child to be pointer-transparent without
suppressing its rendering.

## Example

```csharp
var canvas = new Canvas();
Canvas.SetRight(badge, Length.Cells(1));
Canvas.SetTop(badge, Length.Cells(0));
canvas.Children.Add(badge);
```

## Test obligations

Cover every offset combination, stretch rules, percentages, intrinsic union,
negative/off-screen placement, clipping/hit testing, z-order, zero/tiny bounds,
resize, ownership, Unicode child width, and exact bounds/cells.
