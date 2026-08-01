# Window

## Overview

`Window` is a [`FloatingSurface`](../../concepts/floating-surfaces.md#overview)
that frames its inherited `Content` as a titled terminal surface. The Window
object itself is the retained, rendered, hit-tested, and optionally modal
identity — there is no separate presentation wrapper to manage.

## API

| Member                                                 | Default                   | Purpose                                                               |
| ------------------------------------------------------ | ------------------------- | --------------------------------------------------------------------- |
| `Content`                                              | `null`                    | Owns one child inside the titled frame.                               |
| `Header`, `HeaderPlacement`                            | Empty, `Left`             | Supply and align non-null header text within the top edge.            |
| `CanMove`, `CanClose`, `CloseOnEscape`                 | `true`, `false`, `false`  | Configure Overlay drag movement and explicit close requests.          |
| `CanResize`                                            | `false`                   | Enable pointer-driven resizing from the bottom-right corner.          |
| `ClosePlacement`                                       | `Left`                    | Places close chrome after the left corner or before the right corner. |
| Inherited `Border`, `CloseGlyph`                       | Window profile, `■`       | Configure the complete frame and the close-button glyph.              |
| Inherited `Face`                                       | Window profile            | Paints the theme-defined opaque window body.                          |
| Inherited `Shadow`                                     | Window profile            | Configures visual overflow outside the border box.                    |
| Inherited `ActualFace`, `ActualBorder`, `ActualShadow` | Read-only resolved values | Expose the current theme-, state-, and caller-composed appearance.    |
| `SurfaceBounds`                                        | Empty, read-only          | Reports the committed window rectangle while presented.               |
| `IsActive`                                             | `false`, read-only        | Reports whether this is the owning Application's active Window.       |
| `Shown`, `Closing`, `Closed`                           | No subscribers            | Observe presentation and the ordered close lifecycle.                 |

## Layout and positioning

The inherited `Content` slot uses managed capacity-one ownership, and the child
is arranged inside the one-cell physical frame. Replacing content detaches the
old control without disposing it; disposing the Window disposes only whatever
content is still assigned at that point. Automatic measurement includes the
child, margins, frame, header, and the complete close chrome.

A Window is normally a direct child of an
[`Overlay`](../layout/overlay.md#overview). A Window that fits and has no
explicit position centers on each centered axis. The attached `Left`, `Top`,
`Right`, and `Bottom` offsets select explicit placement. On every arrangement,
the Overlay keeps the complete border box inside its latest content bounds
without changing the offsets you authored. An oversized Window starts at the
leading edge and clips normally.

When `CanMove` is true, a primary drag on unoccupied title-bar chrome captures
the pointer and writes Overlay `Left` and `Top` offsets from the absolute
pointer movement. The border box stays inside the parent content bounds, and
release, pointer leave, capture loss, disable, hide, detach, or disposal all end
the drag.

When `CanResize` is true, a primary drag from the single bottom-right corner
cell captures the pointer and writes `Width` and `Height` from the absolute
pointer movement. The top-left corner stays fixed because the gesture also
writes Overlay `Left` and `Top` offsets from the corner's starting position —
so, just as a title drag does, a resize converts the window to an explicitly
positioned one, regardless of whatever alignment or `Right`/`Bottom` anchoring
placed it beforehand. The result is clamped to `MinWidth`/`MaxWidth`,
`MinHeight`/`MaxHeight`, and the parent content bounds. The corner hit is
checked before the title-bar hit, so a minimum-height window resizes rather than
drags when the two targets coincide. Only the bottom-right corner is an
interactive target; the other three corners and the four edges are not resize
handles. Release, pointer leave, capture loss, disable, hide, detach, or
disposal ends a resize the same way as a drag.

## Chrome and interaction

`Header` is non-null text, clipped before either corner. `HeaderPlacement`
aligns it left, center, or right within the lane left over after close chrome.
Automatic width reserves the Unicode-measured header and close cells. Ampersands
in the header are literal; they do not declare Window access keys.

When `CanClose` is true, one close affordance renders in the selected title
edge. At full width the chrome is `[■]` with two frame glyphs on each side;
narrow widths degrade to a single close glyph, or omit it entirely when no
interior cell can be represented. The pointer target supports hover, capture,
press, leave, reentry, release, and unavailable cleanup. Activating it requests
closure: the Window raises `Closing` and, unless the application cancels the
request, hides itself before raising `Closed`.

> [!IMPORTANT] **Implementation gap:** Today every close path — the close
> affordance, Escape, modal `Dismiss`, and `RequestClose()` — only raises the
> non-cancellable `Closing` event; nothing hides or disposes the Window unless
> an application handler does it. A window whose `Closing` is unsubscribed
> visibly ignores its own close button. Issue #223 tracks close-by-default with
> a cancellation hook.

Pointer ancestry does not restyle the Window face, frame, or shadow. The close
mark can still react independently while its target is hovered or pressed.

The owning Application publishes at most one active Window through
`Application.ActiveWindow`, and the matching Window reports `IsActive`. A
modal-eligible primary press on Window chrome, content, or any descendant
activates the nearest Window before routed pointer handlers run. Generic pointer
focus is bounded by that Window, so clicking non-focusable chrome or background
does not move keyboard focus out into the application shell. A committed
programmatic, pointer, keyboard, or modal focus transition into a Window also
activates its nearest Window ancestor. A qualifying press or a committed focus
move outside all Windows clears activation.

Activating another Window atomically deactivates the previous one. Activation
should also raise the activated Window above its sibling windows, and closing or
hiding the active Window should activate the most recently active remaining one.

> [!IMPORTANT] **Implementation gap:** Activation currently changes `IsActive`
> only: the activated Window is not promoted in Overlay z-order (a clicked
> window can stay buried while its border paints the active color), and hiding,
> collapsing, disabling, detaching, disposing, or shutting down the active
> Window clears `Application.ActiveWindow` without restoring an older Window.
> Issue #224 tracks raise-on-activate and activation history. The default Window
> profile maps `IsActive` onto its existing `FocusWithin` appearance
> contribution, changing only the frame foreground to `ThemeColor.ActiveBorder`.
> `ContainsFocus` and `IsFocused` keep their independent keyboard-focus
> meanings.

Unhandled Enter and Escape search the owned descendants in deterministic
ownership order for the first enabled, visible `Button` marked `IsDefault` or
`IsCancel`. When no cancel button exists, Escape raises `Closing` only when both
`CloseOnEscape` and `CanClose` are true.

## Presentation and modality

An attached, visible Window is a presented surface. Changing `Visibility` to
visible opens it, updates `SurfaceBounds`, raises `Shown`, and modelessly
focuses the first eligible descendant, or the Window itself. Changing visibility
away from visible exits any active surface scope and performs the common focus,
capture, bounds, and lifecycle cleanup.

`ShowModal(outsideInteraction, initialFocus)` makes the Window visible and
returns its application-owned `ModalScope`. The default
`OutsideInteraction.Ignore` consumes outside input without requesting closure.
`Dismiss` raises `Closing`; it does not implicitly hide the Window. One Window
cannot own two live modal presentations. Disposing the returned scope from
outside ends modality without changing visibility, so the same surface can
continue modelessly. The shared
[modality contract](../../concepts/modality.md#popup-and-window-presentations)
owns validation, confinement, nesting, and focus restoration.

Dialogs do not select a Window role through an enum. Instead,
[`Dialog<TResult>`](../../dialogs/index.md#dialog-catalog) derives from Window
and sets the fixed placement, centered header, close, Escape, typed completion,
and modal lifecycle policy.

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

Callers can rely on the documented behavior across the whole surface:
inheritance without role enums; the caller defaults and overrides; the
Application and Window activation identity, including primary presses on chrome,
background, content, and descendants, programmatic and keyboard focus
activation, switching between Windows, clearing on outside presses, modal
rejection, focus independence, unavailable cleanup, shutdown, and active-border
rendering; header clipping, placement, and Unicode measurement; the exact close
chrome and its pointer states; zero and tiny bounds; content and collapsed
geometry; the opaque surface and shadows; ownership; default/cancel/Escape
discovery through private slots; Overlay centering, offsets, title drag,
clamping, resize, and the oversized fallback; the presentation lifecycle and its
rollback; Ignore and Dismiss modality; rejection of duplicate presentations;
initial-focus validation; Tab confinement; focus restoration; visibility-driven
exit; external scope disposal; the final semantic cells; and cleanup after
callback failures.
