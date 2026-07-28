# Transient Surfaces

## Choose the surface

| Need                                                   | Use                   |
| ------------------------------------------------------ | --------------------- |
| Titled, movable application surface with actions       | `Window`              |
| Anchored dropdown, picker, or context surface          | `Popup`               |
| Command hierarchy with keyboard and pointer navigation | `Menu` and `MenuItem` |
| Visual layer without interaction isolation             | `Overlay`             |

Visibility and Window modality are separate. A surface must be in the mounted
ownership tree to render, receive input, and participate in focus. Popup opening
is modal by default; use `ShowModal` for a modal Window. Do not simulate
modality by disabling an unrelated background tree.

## Window composition

Host movable Windows directly in a Canvas. Place the initial top edge below
persistent menu chrome and use a trailing inset when appropriate. Canvas clamps
the complete Window border box inward after resize without mutating authored
offsets; an oversized Window anchors at the leading content edge and clips.

For a modeless tool Window, set `Visibility = Visibility.Visible` and move focus
explicitly when appropriate; the editor, menus, and other application surfaces
remain interactive. Call `ShowModal` only when the task requires focus and input
confinement. Floating position, a frame, or a shadow does not imply modality.

Use a Grid for dialog content:

- Auto rows for fields and footer;
- Auto label column;
- Star field column with a useful minimum;
- Auto action columns;
- status or validation spanning the flexible leading columns;
- Close/Cancel in the trailing column.

Use percentage Window width bounded by cell Min/Max. Avoid fixed field widths:
the flexible field column should absorb the available space.

Wire `IsDefault` and `IsCancel` so Enter and Escape follow platform conventions.
Keep destructive actions visually and spatially distinct from routine actions.

## Popup composition

Set `Anchor` and the preferred `Placement`; Popup flips and clamps when the
preferred side does not fit. Do not calculate coordinates manually or raise its
z-index. Its framed surface is opaque and automatically elevated.

Set `IsOpen = true` for the ordinary Dismiss presentation. Outside input and an
unhandled in-plane wheel close the Popup without replaying to background
controls. Use `OpenModal(...)` only to select another outside policy or explicit
initial focus; callers do not opt into basic Popup modality.

## Menu composition

Menus and submenus share coordinated modal presentation. Give every advertised
command an invoke handler and ensure the owning Menu/Popup is attached to the
mounted tree. Avoid replacing menu modality with custom preview handlers.

Context menus should anchor to the relevant control or pointer policy and use
Popup/Menu placement rather than Canvas coordinates.

## Resize and focus review

For every transient surface, verify:

- opening focus and intended background interaction;
- default/cancel activation;
- Escape and explicit close behavior;
- resize repositioning or inward Window constraint;
- pointer capture release after dragging or closure;
- tiny viewport access to essential actions.

For modal surfaces, additionally verify Tab confinement, outside interaction
policy, consumed input, and focus restoration to the opener.
