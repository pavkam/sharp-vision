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

## Custom cell drawing

Controls placed on a Canvas are standard layout children. For freeform cell
rendering, derive a custom `Control` and override `OnRender(TerminalCanvas)`.
The TerminalCanvas provides deterministic drawing primitives that operate on
semantic cells without terminal escape knowledge:

| Primitive                                 | Purpose                                        |
| ----------------------------------------- | ---------------------------------------------- |
| `DrawLine`                                | Bresenham line between two cells               |
| `DrawCircle` / `DrawEllipse`              | Midpoint rasterized outlines                   |
| `DrawBox`                                 | Complete box with topology-merged corners      |
| `DrawHorizontalLine` / `DrawVerticalLine` | Axis-aligned lines with auto-merging junctions |
| `FillShade`                               | Light/Medium/Dark/Solid shade fills            |
| `DrawQuadrants`                           | Quarter-cell block elements                    |
| `Fill` / `Clear` / `ApplyStyle`           | Region fill, clear, restyle                    |
| `Draw`                                    | Full grapheme-cluster text                     |

Line primitives share a topology table: horizontal and vertical segments that
cross the same cell auto-merge to `┼` `├` `┤` `┬` `┴` and corner junctions
without per-intersection code. Block elements ▁▂▃▄▅▆▇█ provide sub-cell vertical
resolution for sparklines and gauges.

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

Mounted cross-layer coverage in
[`CanvasSurfaceTests`](../../../tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs)
proves percentage repositioning with a real wide-cell pointer target, stale
continuation repair after resize, negative trailing placement, clipping, and
later content reveal.
