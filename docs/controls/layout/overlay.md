# Overlay

## Overlay contract

`Overlay` owns overlapping managed children, optional absolute positioning, and
deterministic z-order. Unpositioned children share the complete content box;
positioned children resolve attached offsets against that same box.

## API

| Member                                    | Default | Purpose                                                         |
| ----------------------------------------- | ------- | --------------------------------------------------------------- |
| `Children`                                | Empty   | Owns overlapping controls in stable collection order.           |
| Attached `Left`, `Top`, `Right`, `Bottom` | `null`  | Optionally position a child from one or both axis edges.        |
| Attached `ZIndex`                         | `0`     | Orders rendering low-to-high and hit testing high-to-low.       |
| `ClipToBounds`                            | `true`  | Clips descendant drawing and hit testing to the overlay bounds. |

## Behavior

- `Children` follows managed ownership.
- Position offsets accept non-negative cells or percentages. Undefined,
  negative, automatic, and proportional values throw before attached state
  changes. Clearing an offset restores ordinary shared-box layout on that edge.
- Attached `ZIndex` is any integer; equal values preserve child order.
- `ClipToBounds` defaults true and controls descendant drawing/hit-test clipping
  to the overlay bounds. This is a hard visual boundary: descendant shadows do
  not escape it. The overlay's own drawing remains clipped. Setting it false
  preserves the inherited ancestor aperture for ordinary content and shadow
  overflow.

An unpositioned child contributes its margin-inclusive desired size and resolves
length and alignment independently against the complete content box. A
positioned child resolves fixed and percentage offsets from that box. One
leading offset chooses the origin; one trailing offset anchors the trailing
edge. Opposing offsets stretch an automatic dimension, while an explicit
dimension keeps its extent and gives the leading edge origin precedence.

During bounded measure, percentage offsets resolve from the available extent.
During unbounded measure, only finite cell-offset unions contribute to desired
size; percentage-only coordinates cannot manufacture an intrinsic bound.
Saturated arithmetic keeps extreme offsets deterministic. A trailing-anchored
child wider or taller than the content box may receive a negative origin and is
then clipped by normal policy.

Windows implement the internal Overlay position constraint. A fitting Window
without authored offsets centers inside the content box. Every arrange clamps
its complete border box after authored offsets resolve, including after resize,
without rewriting those offsets. A larger Window starts at the leading content
edge and clips normally. Title-bar dragging writes `Left` and `Top` offsets.

Hit testing visits highest visible z-order first and respects clipping and
`IsHitTestVisible` pointer transparency. Rendering visits low to high z-order.
Equal values are stable in collection order, while default focus traversal
always remains collection order. The same stable z-order governs elevated popup
descendants: higher-z branches render later and hit-test first, including when a
generated scrollbar occupies the same cells.

When `AutoScroll` is armed, ordinary z-ordered content renders and hit-tests
only inside the committed viewport. Generated scrollbar parts render above
ordinary content and receive pointer input before it. Elevated popup branches
remain the highest layer; among them the same stable `ZIndex` order still
applies.

Modality does not change this visual order, reparent children, or synthesize a
scrim. A modal Window still needs ordinary Overlay placement, and Popup
promotion remains authoritative. The
[rendering and layout contract](../../concepts/modality.md#rendering-and-layout)
separates visual layering from interaction-plane membership.

Attached values use weak ownership and validate dispatcher affinity before
mutation. Changing a child z-order invalidates only its owning Overlay's render
phase. Ordering uses cleared pooled child storage and retains no controls after
the synchronous render or hit-test operation.

## Example

```csharp
var overlay = new Overlay();
overlay.Children.Add(content);
overlay.Children.Add(statusPopup);
Overlay.SetRight(statusPopup, Length.Cells(1));
Overlay.SetTop(statusPopup, Length.Cells(0));
Overlay.SetZIndex(statusPopup, 10);
```

## Expected behavior

Cover unpositioned shared layout; every leading, trailing, and opposing-edge
combination; cells and percentages; invalid values; explicit-size precedence;
finite intrinsic unions; saturated and negative coordinates; z-order
ties/changes; hit testing; pointer-transparent children; clipping; collapsed
children; zero/tiny bounds; Window drag and resize constraints; ownership;
focus-order independence; popup z-order; viewport clipping; scrollbar
precedence; removal damage; and exact cells.

Mounted cross-layer coverage in
[`OverlaySurfaceTests`](../../../tests/SharpVision.Tests/Controls/Layout/OverlaySurfaceTests.cs)
proves z-order visual and pointer precedence, reordering, removal damage and
lower-layer reveal, plus percentage sizing and trailing-alignment resize.
