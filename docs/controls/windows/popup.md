# Popup

## Popup contract

`Popup` is a [`ContentControl`](../content-control.md#contentcontrol-contract)
that displays its inherited `Content` on an opaque, one-cell rounded frame
relative to an optional anchor. It is intentionally non-modal: callers compose
it with an owner and decide what reopening or dismissal means. Elevation is
automatic; a caller does not need to assign a higher ordinary z-order. Its
surface always clears before its content renders, so a drop-down cannot visually
blend with the content behind it.

An open popup is promoted into the shared popup layer after ordinary sibling
rendering and before final pointer targeting. It stays visually and
interactively above later layout siblings such as FIGlet output or document text
without reparenting or changing routed ancestry. Promotion omits the surface
from the ordinary pass and renders it exactly once in the popup pass. Dedicated
popup slots remain preferred metadata, while current ordinary owners receive the
same behavior through the Popup's intrinsic promotion.

## API

- Inherited `Content` uses managed capacity-one ownership and is collapsed while
  `IsOpen` is false, so closed content cannot receive focus, pointer input, or
  rendering. Replacement detaches the previous content without disposal and
  preserves the last `Visibility` the Popup forced on it; newly committed
  content is forced visible while open or collapsed while closed.
- `Anchor` and `Placement` define position. `Below`, `Above`, `Right`, and
  `Left` use the anchored edge and flip to the natural opposite side before
  clamping when the preferred side does not fit.
- `Glyphs` defaults to `Glyphs.Rounded`; `BorderColor` optionally overrides only
  its foreground. `Background` optionally overrides the opaque surface,
  otherwise the resolved inherited background fills it.
- `SurfaceBounds` reports the committed rectangle including its one-cell frame.
- `IsOpen` controls surface and content arranging, rendering, hit testing, and
  focus transfer to the first focusable descendant across every ownership slot.
- `CloseOnEscape` defaults to true and closes an open popup when Escape bubbles
  from its content.
- `Closing` occurs before content is collapsed, allowing a composite owner to
  restore focus. `IsOpen` is already false, so the surface no longer renders or
  hit tests even though its previous `SurfaceBounds` remains committed and
  readable during this event. `Closed` follows after content becomes unavailable
  and `SurfaceBounds` has cleared.

An `IsOpen` change commits, invalidates, and publishes `PropertyChanged` before
its dependent work. Opening then exposes current content and requests focus.
Closing begins with `IsOpen == false`: it raises `Closing` while current content
retains its pre-close availability and the previous bounds remain readable, but
the surface is already ineligible for rendering and hit testing. It then
collapses current content and releases its focus and capture, clears
`SurfaceBounds`, and raises `Closed`, in that order. Every stage is attempted
after a callback failure, and the earliest failure is rethrown after the
complete transition. A callback cannot reenter `IsOpen`; attempting to do so
throws `InvalidOperationException` without reversing the outer transition.

Disposing a Popup clears `Closing` and `Closed` subscribers before disposing its
currently assigned content. Content removal still publishes the inherited
committed `Content == null` transition; callback failures cannot interrupt
disposal, and the earliest failure remains authoritative. Previously replaced
content remains detached and caller-owned.

Placement constrains the framed surface to the popup host. The content occupies
only the deflated interior; root resize runs normal layout again, so an open
popup repositions before the next frame. Closed layout still enters the base
collapsed-content measure and arrange transactions, clearing stale desired size
and bounds without invoking content overrides.

## Interaction

Closed popups neither draw their surface nor participate in hit testing. An open
popup directs pointer and keyboard input to its content through normal routing;
its frame itself is also a hit-testable surface. The FIGlet showcase picker
demonstrates the composition as `ComboBox → Popup → List`.

## Example

```csharp
var popup = new Popup
{
    Content = details,
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
content, focus discovery across private slots, closing-focus restoration,
callback-failure completion and first-failure order, reentrancy rejection,
ownership, resize repositioning, collapsed geometry clearing, and final cells.
