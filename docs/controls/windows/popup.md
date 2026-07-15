# Popup

## Popup contract

`Popup` displays one owned child on an opaque, one-cell rounded frame relative
to an optional anchor. It is intentionally non-modal: callers compose it with an
owner and decide what reopening or dismissal means. Elevation is automatic; a
caller does not need to assign a higher ordinary z-order. Its surface always
clears before its child renders, so a drop-down cannot visually blend with the
content behind it.

An open popup is promoted into the shared popup layer after ordinary sibling
rendering and before final pointer targeting. It stays visually and
interactively above later layout siblings such as FIGlet output or document text
without reparenting or changing routed ancestry. Promotion omits the surface
from the ordinary pass and renders it exactly once in the popup pass. Dedicated
popup slots remain preferred metadata, while current ordinary owners receive the
same behavior through the Popup's intrinsic promotion.

## API

- `Child` uses managed ownership and is collapsed while `IsOpen` is false, so a
  closed child cannot receive focus, pointer input, or rendering.
- `Anchor` and `Placement` define position. `Below`, `Above`, `Right`, and
  `Left` use the anchored edge and flip to the natural opposite side before
  clamping when the preferred side does not fit.
- `Glyphs` defaults to `Glyphs.Rounded`; `BorderColor` optionally overrides only
  its foreground. `Background` optionally overrides the opaque surface,
  otherwise the resolved inherited background fills it.
- `SurfaceBounds` reports the committed rectangle including its one-cell frame.
- `IsOpen` controls surface and child arranging, rendering, hit testing, and
  focus transfer to the first focusable descendant across every ownership slot.
- `CloseOnEscape` defaults to true and closes an open popup when Escape bubbles
  from its child.
- `Closing` occurs before the child is collapsed, allowing a composite owner to
  restore focus; `Closed` follows after the child becomes unavailable.

Placement constrains the framed surface to the popup host. The child occupies
only the deflated interior; root resize runs normal layout again, so an open
popup repositions before the next frame.

## Interaction

Closed popups neither draw their surface nor participate in hit testing. An open
popup directs pointer and keyboard input to its child through normal routing;
its frame itself is also a hit-testable surface. The FIGlet showcase picker
demonstrates the composition as `ComboBox → Popup → List`.

## Example

```csharp
var popup = new Popup
{
    Child = details,
    Anchor = helpButton,
    Placement = PopupPlacement.Below,
    Background = Color.Indexed(238),
    BorderColor = Color.Indexed(45),
};
```

## Test obligations

Cover closed rendering/hit testing, opaque framed cells, promotion above later
siblings, exactly-once promotion from ordinary and popup slots, suppression by
an ineligible intermediate owner, preferred and fallback placement, Escape from
child content, focus discovery across private slots, closing-focus restoration,
ownership, resize repositioning, and final cells.
