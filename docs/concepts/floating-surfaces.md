# Floating surfaces

## Overview

A floating surface is a retained `ContentControl` presented above ordinary
application content. The public surface object is the same identity that is
mounted, rendered, hit-tested, made modal, removed, and disposed; floating
controls never hide a second Window or Popup behind a forwarding wrapper.

```mermaid
classDiagram
    ContentControl <|-- FloatingSurfaceBase
    FloatingSurfaceBase <|-- Window
    FloatingSurfaceBase <|-- Popup
    FloatingSurfaceBase <|-- Toast
    Window <|-- Dialog~TResult~
    Dialog~TResult~ <|-- FileDialogBase~TResult~
    Dialog~MessageBoxResult~ <|-- MessageBox
    Popup <|-- Flyout
    Popup <|-- Tooltip
```

`FloatingSurfaceBase` lives in `SharpVision.Surfaces`. Its `Border`/`Shadow`
authoring surface is public but gated behind
`ControlBase.EnableChromeAuthoring()` until a derived control opts in; the two
floating surfaces whose whole purpose is letting a caller author their own
chrome directly - `Window` and `Popup`, each in its own feature namespace - call
it from their own constructors. `Dialog<TResult>` derives from `Window`, and the
file dialogs and `MessageBox` are direct dialog surfaces. `Flyout` and `Tooltip`
derive from `Popup` and render their inherited popup surface directly. An
externally defined typed surface family derives from `FloatingSurfaceBase`
directly and declares [`IStyled<TStyle>`](styling.md#overview) itself, the same
contract any other control uses to add a primary `Style`/`ActualStyle` slot -
see [Appearance](styling.md#overview) for the full mechanism.
[`Toast`](../controls/notifications/toast.md#overview) is the non-modal direct
surface sibling: it mounts itself in the owning Screen or Overlay presentation
plane, stacks by screen edge, and supplies its own `ToastStyle` contract.

## Shared lifecycle

`FloatingSurfaceBase` owns the replaceable `Content`, the committed
`SurfaceBounds`, the ordered `Closing` and `Closed` lifecycle, focus and
pointer-capture cleanup, and at most one application-owned `ModalScope`. Each
concrete family owns its public open state and chrome:

- `Window` uses `Visibility` and titled window chrome.
- `Popup` uses `IsOpen`, anchor-relative placement, and popup chrome.
- `Dialog<TResult>` adds one-shot typed completion and deterministic host
  removal and disposal.
- `Flyout` fixes interactive light-dismiss behavior.
- `Tooltip` fixes passive, delayed, non-modal behavior.
- `Toast` uses `Show(owner)` and `Dismiss()` with a timed, non-modal lifetime.

Disposal releases every shared lifecycle subscription, including `Opened`,
`CloseRequested`, `Closing`, and `Closed`, so retaining a disposed surface does
not retain subscriber graphs.

Presenting a Window, or explicitly entering modality, requires an attached,
available, undisposed surface. A detached Popup may stage `IsOpen = true`: it
presents - and, under automatic modal behavior, enters modality - when it is
later attached. A surface cannot present twice or reenter an opening or closing
transaction.

`Opened` runs after the common presentation commit. Popup-family and Toast
opening treat an observer failure as a failed public open and roll back both
family and common presentation state; Window keeps the committed presentation
and completes its later `Shown` and focus stages before rethrowing the earliest
failure.

Before any of that commits, `FloatingSurfaceBase` raises `CloseRequested` with a
`SurfaceCloseRequestedEventArgs.Cancel` flag a handler can set to veto the
request: nothing changes, and neither `Closing` nor `Closed` follows. Popup-
family closure first makes the family ineligible for rendering and input, then
publishes `Closing`, exits modality, makes the content unavailable, clears
`SurfaceBounds`, and publishes `Closed`. Changing a Window's visibility away
from visible performs the same common cleanup directly but publishes neither
lifecycle event.

`CloseRequested` fires wherever the shared `CloseSurface` engine runs — the
whole Popup family, and `Dialog<TResult>`'s own typed-completion path — and also
from Window's close affordance, `CloseOnEscape`, and modal dismiss. The
hand-rolled path still raises `CloseRequested` first via the same
`FloatingSurfaceBase.RaiseCloseRequested` helper the engine uses and is guarded
so repeated closure after committed cleanup raises nothing, honoring the same
idempotency and veto contract the engine already guarantees. Repeating the same
close request synchronously from `CloseRequested` is a no-op; the outer request
remains authoritative, while reentry from the later `Closing` transaction
remains invalid.

Toast raises the same vetoable request before manual, keyboard, pointer, or
timer dismissal. Showing does not enter modality or transfer focus. Its display
timer starts only after entrance completes, and detach or disposal releases the
timer and stack registration.

> [!IMPORTANT]
>
> **Implementation gap:** Window's close affordance, `CloseOnEscape`, and modal
> dismiss all funnel into one hand-rolled close sequence instead of routing
> through the shared `CloseSurface` engine the rest of the Popup family and
> `Dialog<TResult>` use. Window has no separate _public_ open flag -
> `Visibility == IsVisible` is its open state - and a `Closing` handler may
> retain the Window by touching `Visibility`, which the engine's own
> `commitClosingState` does not support. A never-attached, still-visible Window
> has no presentation to tear down and so no `IsSurfacePresented` transition a
> repeat could be detected from; Window's `RequestClose` keeps a private latch,
> cleared whenever `Visibility` next becomes visible, as the substitute bit for
> exactly that case.

An ordinary Window close affordance, Escape action, or modal dismiss request
first raises `CloseRequested`; an uncancelled request then publishes `Closing`,
then, by default, collapses the Window itself: the visibility transaction
performs the common cleanup and the close request then publishes `Closed`. A
`Closing` handler that itself changes visibility (hiding it to a different
state, restoring it, or disposing the Window) takes responsibility for the
outcome instead — if it leaves the Window visible and presented, the Window
stays open and `Closed` is not published. Cleanup attempts every stage even
after a callback failure and rethrows the earliest failure once state is
coherent. Detachment and disposal release modal, focus, and capture state even
when no normal close path was requested.

## Ownership, elevation, and modality

Elevation changes the render and hit-test order without reparenting the surface
or changing its routed ancestry. Popup-family surfaces are promoted through the
shared popup layer, a structurally separate render and hit-test pass that always
paints after ordinary Overlay content and orders nested popups, flyouts, and
submenus by opener depth — this layer never shares an `Overlay`'s `ZIndex`
space, so it always sits above every Window regardless of Window z-order.
Windows and dialogs are direct children of an `Overlay`, including the private
presentation Overlay owned by `Screen`; `WindowActivationManager` raises the
newly activated Window above its sibling Windows in that shared `ZIndex` space
on every activation, leaving non-Window overlay children and the popup layer
untouched.

Modality is an input-plane policy, not a visual wrapper. `FloatingSurfaceBase`
retains the live scope, while the application
[`ModalityManager`](modality.md#overview) owns confinement, outside interaction,
nested-scope order, capture cleanup, and focus restoration. `Window.ShowModal`
defaults to `OutsideInteraction.Ignore`, while ordinary `Popup` opening defaults
to dismissal. `Flyout` manages light dismissal without a modal scope, and
`Tooltip` never enters modality or pointer targeting.

## Layout and drawing boundaries

[`Overlay`](../controls/layout/overlay.md#overview) is the only public panel for
overlapping children, absolute `Left`/`Top`/`Right`/`Bottom` offsets, and stable
`ZIndex`. Window movement writes Overlay offsets, and Overlay keeps a Window's
border box inside its latest content bounds without changing the authored
offsets.

`SharpVision.Terminal.Rendering.TerminalCanvas` is the frame-owned drawing value
passed to control rendering hooks. It draws graphemes, lines, boxes, fills,
images, and styles; it is not a `Container`, owns no children, and performs no
layout. To put controls above custom drawing, compose the drawing control and
those controls in an Overlay.

## Expected behavior

The public inheritance chain is exactly as diagrammed, and the retired layout
Canvas and proxy surface fields no longer exist. The family-specific attachment
rules hold, including detached-staged Popup opening. Lifecycle order and
rollback, detach and disposal cleanup, the single live modal scope, focus
restoration, capture release, elevated render and hit-test order, logical
ancestry, direct dialog host identity, Popup placement, Flyout light dismiss,
Tooltip delay and passivity, and the final semantic cells all behave as
described above.
