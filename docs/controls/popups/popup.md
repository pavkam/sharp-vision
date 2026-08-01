# Popup

## Overview

`Popup` is a [`FloatingSurface`](../../concepts/floating-surfaces.md#overview)
that displays inherited `Content` on an opaque, one-cell theme-resolved frame
relative to an optional anchor. The Popup object renders and hit-tests that
surface directly; it does not own a second presentation Popup. Setting `IsOpen`
to true in an attached application tree automatically enters a dismissing
[application modal scope](../../concepts/modality.md#popup-and-window-presentations).
`OpenModal` remains available when a caller needs another outside policy or an
explicit initial-focus target. Elevation is automatic; a caller does not need to
assign a higher ordinary z-order. Its surface always clears before its content
renders, so a drop-down cannot visually blend with the content behind it.

An open popup is promoted into the shared popup layer after ordinary sibling
rendering and before final pointer targeting. It stays visually and
interactively above later layout siblings such as FIGlet output or document text
without reparenting or changing routed ancestry. Promotion omits the surface
from the ordinary pass and renders it exactly once in the popup pass. Dedicated
popup ownership slots remain preferred metadata, while ordinary ownership edges
receive the same behavior through intrinsic promotion.

## API

| Member                                                 | Default                   | Purpose                                                                            |
| ------------------------------------------------------ | ------------------------- | ---------------------------------------------------------------------------------- |
| `Content`                                              | `null`                    | Owns one surface child and collapses it while closed.                              |
| `Anchor`, `Placement`                                  | `null`, `Below`           | Position the framed surface relative to a sibling and flip when needed.            |
| `IsOpen`                                               | `false`                   | Controls the visible surface and configured automatic modal lifetime.              |
| `ModalBehavior`                                        | `Auto`                    | Selects automatic Dismiss modality or owner-managed modality.                      |
| `FocusOnOpen`, `CloseOnEscape`                         | `true`, `true`            | Configure focus transfer and Escape dismissal.                                     |
| `ConnectsToAnchor`                                     | `false`                   | Omits the frame edge adjoining the resolved anchor side when initialized true.     |
| `SuppressCloseOtherPopups`                             | `false`                   | Keeps other open Popups under the same logical root when initialized true.         |
| `ShowAnchorIndicator`                                  | `false`                   | Draws one directional frame arrow toward the anchor.                               |
| Inherited `Face`, `Border`, `Shadow`                   | `Popup` theme profile     | Configure complete surface, frame, and shadow appearance.                          |
| Inherited `ActualFace`, `ActualBorder`, `ActualShadow` | Read-only resolved values | Inspect the current theme-, state-, and caller-composed appearance.                |
| `SurfaceBounds`                                        | Empty, read-only          | Reports the committed framed rectangle while open.                                 |
| `Closing`, `Closed`                                    | No subscribers            | Observe the ordered close transition before and after content becomes unavailable. |

## Behavior

- Inherited `Content` uses managed capacity-one ownership and is collapsed while
  `IsOpen` is false, so closed content cannot receive focus, pointer input, or
  rendering. Replacement detaches the previous content without disposal and
  preserves the last `Visibility` the Popup forced on it; newly committed
  content is forced visible while open or collapsed while closed.
- `Anchor` and `Placement` define position. `Below`, `Above`, `Right`, and
  `Left` use the anchored edge and flip to the natural opposite side before
  clamping when the preferred side does not fit.
- `ModalBehavior` accepts only `Auto` or `None`. During opening or later
  attachment and availability reconciliation, `Auto` enters the default
  dismissing scope and `None` leaves modality to the logical owner. Changing the
  value while the surface is already presented does not enter or exit its
  current scope; the new policy applies on the next opening or reconciliation.
  Undefined values throw before mutation. Either policy still permits explicit
  `OpenModal` while the Popup is closed.
- `ConnectsToAnchor` and `SuppressCloseOtherPopups` are initialization-only
  policy. The former removes the frame edge adjoining the resolved anchor side.
  The latter prevents opening from closing other Popup descendants of the same
  logical root. `ShowAnchorIndicator` adds one arrow to the frame edge facing
  the anchor.
- `Face`, `Border`, and `Shadow` resolve from the active theme's `Popup` profile
  unless a caller assigns a complete local composite. `ResetFace()`,
  `ResetBorder()`, and `ResetShadow()` return those values to theme ownership.
- The resolved `ActualShadow` applies to `SurfaceBounds`, not the Popup's
  root-sized layout slot. It remains outside popup hit testing and obeys the
  root frame clip.
- `SurfaceBounds` reports the committed rectangle including its one-cell frame.
- `IsOpen` controls surface and content arranging, rendering, hit testing, and
  configured automatic modal lifetime. A detached open state is staged and
  reconciled when the Popup later attaches. Opening an attached Popup under
  `ModalBehavior.Auto` enters `OutsideInteraction.Dismiss`; an outside or
  unhandled in-plane wheel closes it without replaying that input. `FocusOnOpen`
  defaults to true and transfers focus to the first focusable descendant across
  every ownership slot.
- `OpenModal(outsideInteraction, initialFocus)` opens a closed surface and
  returns its disposable `ModalScope` when a caller needs a non-default policy
  or focus target. One popup cannot own two live modal presentations.
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

An ordinary close raises `Closing` while its modal scope and content are still
available, then exits the scope before content becomes unavailable. Disposing
the returned or application-visible scope closes the Popup; an attached
transient surface never falls back to accidental modeless interaction. A failed
modal entry closes only a popup exposed by that call and rethrows the initiating
failure after rollback. The shared
[presentation contract](../../concepts/modality.md#popup-and-window-presentations)
defines validation, focus selection, and lifetime ownership.

Disposing a Popup performs common surface cleanup before disposing its currently
assigned content. Content removal still publishes the inherited committed
`Content == null` transition; callback failures cannot interrupt disposal, and
the earliest failure remains authoritative. Previously replaced content remains
detached and caller-owned.

Placement constrains the framed surface to the popup host. The content occupies
only the deflated interior; root resize runs normal layout again, so an open
popup repositions before the next frame. Closed layout still enters the base
collapsed-content measure and arrange transactions, clearing stale desired size
and bounds without invoking content overrides. The surface frame overlays
retained content, so child shadows cannot replace its final frame cells.

## Code-owned glyphs

The theme-selected `Border.GlyphStyle` supplies popup chrome. Terminal-safe
glyph repair remains code-owned. Assigning a complete local `Border` overrides
the theme; `ResetBorder()` restores theme resolution.

## Interaction

Closed popups neither draw their surface nor participate in hit testing. An open
popup directs pointer and keyboard input to its content through normal routing;
its frame itself is also a hit-testable surface. The FIGlet showcase picker
demonstrates the composition as `ComboBox → Popup → ListView`.

Popup owns generic elevation, placement fallback, default Dismiss modality, and
open-chain lifetime. It delegates isolation and consume-without-replay behavior
to the application modality manager. Framework consumers may coordinate a larger
logical plane internally: for example, [`Menu`](../menus/menu.md#interaction)
decides when hover switches an armed sibling submenu, while each `MenuItem`
configures that retained popup's preferred direction and menu-specific surface
appearance. A press inside any open descendant popup surface remains inside the
ancestor chain; light dismissal must not collapse an ancestor before the
descendant input route completes.

## Example

![The Popup control rendered in the live showcase](../../images/controls/popup.png)

```csharp
var popup = new Popup
{
    Content = details,
    Anchor = helpButton,
    Placement = PopupPlacement.Below,
    Face = new Face(
        ThemeColor.ControlText,
        Color.Rgb(68, 68, 68),
        ThemeDecoration.NormalText,
        Underline.None,
        Color.Default),
    Border = new Border(
        BorderSide.All,
        BorderGlyphStyle.Rounded,
        Color.Rgb(0, 215, 255),
        Color.Transparent,
        ThemeDecoration.Border),
};

popup.IsOpen = true;
```

## Expected behavior

Cover closed and detached-staged opening, rendering/hit testing, opaque framed
cells, promotion above later siblings, exactly-once promotion from ordinary and
popup slots, suppression by an ineligible intermediate owner, preferred and
fallback placement, Escape from content, descendant-surface light-dismiss
preservation, modal-behavior validation, connected and indicated anchor chrome,
sibling-close suppression, focus discovery across private slots, closing-focus
restoration, callback-failure completion and first-failure order, reentrancy
rejection, ownership, resize repositioning, collapsed geometry clearing, and
final cells.

Modal coverage additionally requires automatic default Dismiss behavior,
duplicate-presentation rejection, initial-focus validation, outside and
in-plane-unhandled wheel consumption, scroll-child retention, Escape and
ordinary-close scope exit, focus restoration, external scope disposal with
visual closure, entry rollback, owner-managed composite planes, and reentrant
callback failure.
