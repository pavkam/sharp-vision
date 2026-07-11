# Overlay

## Overlay contract

`Overlay` gives managed children the same content box and renders them in
deterministic z-order for layered UI.

## API

- `Children` follows managed ownership.
- Attached `ZIndex` is any integer; equal values preserve child order.
- `ClipToBounds` controls drawing/hit-test clipping to the overlay bounds.

Desired size is the maximum margin-inclusive child size. Arrange applies each
child's length/alignment independently. Hit testing visits highest visible
z-order first and respects clipping and pointer transparency.

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
ownership, focus order independence, damage after removal, and exact cells.
