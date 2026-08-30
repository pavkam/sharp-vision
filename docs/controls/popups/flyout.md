# Flyout

## Overview

`Flyout` is declared `public sealed class Flyout : Popup`. It is a direct
[`Popup`](popup.md#overview) specialization for interactive, anchored,
light-dismiss content. The Flyout object is the popup surface itself; it does
not forward state to a private Popup.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- ContentControl
    ContentControl <|-- FloatingSurfaceBase
    FloatingSurfaceBase <|-- Popup
    Popup <|-- Flyout
```

## API

| Member                          | Type             | Default | Description                                                             |
| ------------------------------- | ---------------- | ------- | ----------------------------------------------------------------------- |
| Inherited `Content`             | `ControlBase?`   | `null`  | The flyout body; the flyout owns it.                                    |
| Inherited `Anchor`              | `ControlBase?`   | `null`  | Identifies the sibling used for placement.                              |
| Inherited `Placement`           | `PopupPlacement` | `Below` | Selects the preferred anchor-relative placement.                        |
| Inherited `IsOpen`              | `bool`           | `false` | Controls the direct inherited popup presentation.                       |
| Inherited `CloseOnEscape`       | `bool`           | `true`  | Lets a bubbling Escape close the surface.                               |
| Inherited `FocusOnOpen`         | `bool`           | `true`  | Transfers focus to the first eligible descendant.                       |
| Inherited `ShowAnchorIndicator` | `bool`           | `false` | Draws an arrow toward the anchor when enabled.                          |
| `ShowAt(ControlBase anchor)`    | `void`           | —       | Validates, assigns the anchor, and opens the same Flyout instance.      |
| Inherited `Closing`             | `EventHandler`   | —       | Raised when closure is requested or after closing state commits.        |
| Inherited `Closed`              | `EventHandler`   | —       | Raised only after the surface becomes unavailable and its bounds clear. |

## Keyboard

| Key    | Behavior                                                               |
| ------ | ---------------------------------------------------------------------- |
| Escape | Closes the open Flyout through the inherited `CloseOnEscape` behavior. |

## Light dismiss

`ShowAt(anchor)` validates the anchor, assigns it, and opens the same Flyout
instance. Opening installs light-dismiss observation, closes any other open
sibling Flyout beneath the same logical root, preserves open ancestor Flyouts,
and keeps normal routed ancestry. A primary press outside the Flyout and its
anchor closes it without replaying the press to the background and restores the
focus held before opening. When the anchor belongs to an older modal plane, the
same rule applies both inside and outside that plane: only the Flyout closes,
and the older modal scope remains active. Moving or resizing the anchor closes
the open Flyout, so a stale placement is never kept on screen.

The inherited Popup lifecycle owns the registration itself. The Flyout supplies
only primary-button dismissal, anchor inclusion, and its close callback.
Registration begins after committed presentation, follows an anchor replacement,
and is released by close, hide, detach, or disposal. Peer exclusion uses the
shared Popup snapshot and revalidation engine while filtering candidates to the
Flyout family, so unrelated owner-managed Popups remain open.

Automatic `IsOpen` presentation uses light dismissal without creating a modal
scope. The inherited `OpenModal` API remains available when a caller explicitly
needs application modality. Placement, flipping, root clamping, elevation,
frame, shadow, content ownership, lifecycle, and disposal follow
[`Popup`](popup.md#overview) and the
[floating-surface](../../concepts/floating-surfaces.md#overview) contracts.

## Example

![The Flyout control rendered in the live showcase](../../images/controls/flyout.png)

```csharp
var flyout = new Flyout
{
    Content = new Stack { Children = { confirmButton, cancelButton } },
};

owner.Children.Add(flyout);
flyout.ShowAt(triggerButton);
```

## Expected behavior

| Scope               | Observable evidence                                                       |
| ------------------- | ------------------------------------------------------------------------- |
| Public API          | Validation, defaults, state changes, and deterministic output.            |
| Integrated behavior | Cross-component behavior through the real ownership and routing boundary. |

- The Flyout is a direct Popup with no nested presentation Popup, and `ShowAt`
  validates its argument and uses exactly that anchor.
- Placement, rendering, and `SurfaceBounds` behave as inherited from Popup.
- A primary press outside dismisses the flyout without replaying the press;
  secondary, middle, back, and forward presses continue routing without closing
  it. The dismissing press restores pre-open focus and does not activate a
  background control or dismiss an older modal plane. Escape closes it, and
  opening transfers focus to the first eligible descendant.
- Opening one flyout closes a sibling under the same logical root, and moving or
  resizing the anchor closes the open flyout. A nested Flyout preserves its open
  ancestor. Sibling closure snapshots and revalidates retained identities across
  callback-driven tree mutation.
- The lifecycle events fire in their documented order, and detaching or
  disposing the flyout cleans it up, including its Popup-owned light-dismiss
  registration.
- Final rendering resolves to the expected semantic cells.
