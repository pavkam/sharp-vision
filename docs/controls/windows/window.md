# Window

## Window contract

`Window` is a
[`FloatingSurface`](../../concepts/floating-surfaces.md#floating-surface-contract)
that frames inherited `Content` as a titled terminal surface. The Window object
is the retained, rendered, hit-tested, and optionally modal identity; it does
not require a presentation wrapper.

## API

| Member                                                 | Default                   | Purpose                                                               |
| ------------------------------------------------------ | ------------------------- | --------------------------------------------------------------------- |
| `Content`                                              | `null`                    | Owns one child inside the titled frame.                               |
| `Header`, `HeaderPlacement`                            | Empty, `Left`             | Supply and align non-null header text within the top edge.            |
| `CanMove`, `CanClose`, `CloseOnEscape`                 | `true`, `false`, `false`  | Configure Overlay drag movement and explicit close requests.          |
| `CanResize`                                            | `false`                   | Enable pointer-driven resizing from the bottom-right corner.          |
| `ClosePlacement`                                       | `Left`                    | Places close chrome after the left corner or before the right corner. |
| Inherited `Border`, `CloseGlyph`                       | Window profile, `■`       | Configure the complete frame and close-button glyph.                  |
| Inherited `Face`                                       | Window profile            | Paints the theme-defined opaque window body.                          |
| Inherited `Shadow`                                     | Window profile            | Configures visual overflow outside the border box.                    |
| Inherited `ActualFace`, `ActualBorder`, `ActualShadow` | Read-only resolved values | Expose the current theme-, state-, and caller-composed appearance.    |
| `SurfaceBounds`                                        | Empty, read-only          | Reports the committed window rectangle while presented.               |
| `IsActive`                                             | `false`, read-only        | Reports whether this is the owning Application's active Window.       |
| `Shown`, `Closing`, `Closed`                           | No subscribers            | Observe presentation and the ordered close lifecycle.                 |

## Layout and positioning

Inherited `Content` uses managed capacity-one ownership and is arranged inside
the one-cell physical frame. Replacement detaches the old content without
disposing it; Window disposal owns only content still assigned at disposal.
Automatic measurement includes the child, margins, frame, header, and complete
close chrome.

A Window is normally a direct child of an
[`Overlay`](../layout/overlay.md#overlay-contract). Unpositioned fitting Windows
center on each centered axis. Attached `Left`, `Top`, `Right`, and `Bottom`
offsets select explicit placement. Every Overlay arrangement constrains the
complete border box inside the latest content bounds without mutating authored
offsets. An oversized Window begins at the leading edge and clips normally.

When `CanMove` is true, a primary drag from unoccupied title-bar chrome captures
the pointer and writes Overlay `Left` and `Top` offsets from absolute pointer
movement. The border box stays inside the parent content bounds; release,
capture loss, disable, hide, detach, or disposal ends the drag.

When `CanResize` is true, a primary drag from the single bottom-right corner
cell captures the pointer and writes `Width`/`Height` from absolute pointer
movement, keeping the top-left corner fixed by also writing Overlay `Left` and
`Top` offsets from the corner's position when the gesture began — the same way a
drag converts the window to an explicitly positioned one, regardless of whatever
alignment or `Right`/`Bottom` anchoring positioned it beforehand. The result is
clamped to `MinWidth`/`MaxWidth`, `MinHeight`/`MaxHeight`, and the parent
content bounds. A corner hit is checked before a title-bar hit, so a
minimum-height window resizes rather than drags when both targets coincide.
Release, capture loss, disable, hide, detach, or disposal ends the resize the
same way as a drag. Only the bottom-right corner is an interactive target; the
other three corners and the four edges are not resize handles.

## Chrome and interaction

`Header` is non-null text clipped before either corner. `HeaderPlacement` aligns
it left, center, or right in the lane that remains after close chrome. Automatic
width reserves Unicode-measured header and close cells. Ampersands do not
declare Window access keys.

`CanClose` renders one close affordance in the selected title edge. Full-width
chrome uses `[■]` with two frame glyphs on each side; narrow widths degrade to
one close glyph or omit it when no interior cell is representable. The pointer
target supports hover, capture, press, leave, reentry, release, and unavailable
cleanup. Activation raises `Closing`; it does not decide whether an application
hides or disposes an ordinary Window.

Pointer ancestry does not restyle the Window face, frame, or shadow. The close
mark may still react independently while its target is hovered or pressed.

The owning Application publishes at most one active Window through
`Application.ActiveWindow`; the matching Window reports `IsActive`. A
modal-eligible primary press on Window chrome, content, or a descendant
activates the nearest Window before routed pointer handlers run. Generic pointer
focus is bounded by that Window, so clicking non-focusable chrome or background
does not move keyboard focus into the application shell. A committed
programmatic, pointer, keyboard, or modal focus transition into a Window also
activates its nearest Window ancestor. A qualifying press or committed focus
outside all Windows clears activation.

Activating another Window atomically deactivates the previous one without
promoting Overlay z-order. Hiding, collapsing, disabling, detaching, disposing,
or shutting down the active Window clears the Application reference; no older
Window is restored implicitly. The default Window profile maps `IsActive` onto
its existing `FocusWithin` appearance contribution, changing only the frame
foreground to `ThemeColor.ActiveBorder`. `ContainsFocus` and `IsFocused` retain
their independent keyboard-focus meanings.

Unhandled Enter and Escape search owned descendants in deterministic ownership
order for the first enabled visible `Button` marked `IsDefault` or `IsCancel`.
When no cancel button exists, Escape raises `Closing` only when both
`CloseOnEscape` and `CanClose` are true.

## Presentation and modality

An attached visible Window is a presented surface. Changing `Visibility` to
visible opens it, updates `SurfaceBounds`, raises `Shown`, and modelessly
focuses the first eligible descendant or the Window itself. Changing visibility
away from visible exits any active surface scope and performs common focus,
capture, bounds, and lifecycle cleanup.

`ShowModal(outsideInteraction, initialFocus)` makes the Window visible and
returns its application-owned `ModalScope`. The default
`OutsideInteraction.Ignore` consumes outside input without requesting closure.
`Dismiss` raises `Closing`; it does not implicitly hide the Window. One Window
cannot own two live modal presentations. Externally disposing the returned scope
ends modality without changing visibility, allowing the same surface to continue
modelessly. The shared
[modality contract](../../concepts/modality.md#popup-and-window-presentations)
owns validation, confinement, nesting, and focus restoration.

Dialogs do not select a Window role enum. Instead,
[`Dialog<TResult>`](../../dialogs/index.md#dialog-catalog) derives from Window
and sets fixed placement, centered header, close, Escape, typed completion, and
modal lifecycle policy.

## Example

![The Window control rendered in the live showcase](../../images/controls/window.png)

```csharp
var window = new Window
{
    Header = "Workspace",
    CanClose = true,
    CloseOnEscape = true,
    Content = root,
};

overlay.Children.Add(window);
Overlay.SetLeft(window, Length.Cells(4));
Overlay.SetTop(window, Length.Cells(2));

var scope = window.ShowModal(
    OutsideInteraction.Ignore,
    initialFocus: firstField);
```

## Expected behavior

Cover inheritance and absence of role enums; caller defaults and overrides;
Application and Window activation identity; primary chrome, background, content,
and descendant presses; programmatic and keyboard focus activation; switching,
outside clearing, modal rejection, focus independence, unavailable cleanup,
shutdown, and active-border rendering; header clipping, placement, and Unicode
measurement; exact close chrome and pointer state; zero and tiny bounds; content
and collapsed geometry; opaque surface and shadows; ownership;
default/cancel/Escape discovery through private slots; Overlay centering,
offsets, title drag, clamping, resize, and oversized fallback; presentation
lifecycle and rollback; Ignore and Dismiss modality; duplicate presentation
rejection; initial-focus validation; Tab confinement; focus restoration;
visibility-driven exit; external scope disposal; final semantic cells; and
callback-failure cleanup.
