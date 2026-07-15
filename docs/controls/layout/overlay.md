# Overlay

## Overlay contract

`Overlay` gives managed children the same content box and renders them in
deterministic z-order for layered UI.

## API

- `Children` follows managed ownership.
- Attached `ZIndex` is any integer; equal values preserve child order.
- `ClipToBounds` defaults true and controls descendant drawing/hit-test clipping
  to the overlay bounds. The overlay's own drawing remains clipped.

Desired size is the maximum margin-inclusive child size. Arrange applies each
child's length/alignment independently. Hit testing visits highest visible
z-order first and respects clipping and `IsHitTestVisible` pointer transparency.
Rendering visits low to high z-order. Equal values are stable in collection
order, while default focus traversal always remains collection order. The same
stable z-order governs elevated popup descendants: higher-z branches render
later and hit-test first, including when a generated scrollbar occupies the same
cells.

When `AutoScroll` is armed, ordinary z-ordered content renders and hit-tests
only inside the committed viewport. Generated scrollbar parts render above
ordinary content and receive pointer input before it. Elevated popup branches
remain the highest layer; among them the same stable `ZIndex` order still
applies.

Attached values use weak ownership and validate dispatcher affinity before
mutation. Changing a child z-order invalidates only its owning Overlay's render
phase. Ordering uses cleared pooled child storage and retains no controls after
the synchronous render or hit-test operation.

## Example

```csharp
var overlay = new Overlay();
overlay.Children.Add(content);
overlay.Children.Add(statusPopup);
Overlay.SetZIndex(statusPopup, 10);
```

## Test obligations

Cover z-order ties/changes, hit testing, pointer-transparent children, clipping,
alignment/percent sizing, collapsed children, zero/tiny bounds, resize,
ownership, focus order independence, popup z-order, viewport clipping, scrollbar
render/hit precedence, damage after removal, and exact cells.
