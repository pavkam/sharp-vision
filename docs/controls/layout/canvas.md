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

Measure reports the finite union of positioned intrinsic children. Percentage
offsets defer under unbounded measure and resolve against final content size.
Negative final coordinates are allowed but clipped by the parent policy.

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
