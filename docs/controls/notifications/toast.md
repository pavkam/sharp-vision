# Toast

## Overview

`Toast` is declared `public sealed class Toast : FloatingSurfaceBase` and
implements `IStyled<ToastStyle>` and the internal `IOverlayPositionConstraint`,
which keeps its edge slot inside the host's bounds. It presents one caller-owned
notification object in the attached owner’s Screen or Overlay presentation plane
without entering modality or moving focus. The Toast owns its replaceable
`Content`; callers retain and dispose the Toast itself.

Six edge positions partition independent stacks. New notifications occupy the
edge-nearest slot and older notifications move inward with one cell of spacing.
All mutation and lifecycle work is dispatcher-affine. Invalid positions,
animations, and durations throw before observable state changes.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- ContentControl
    ContentControl <|-- FloatingSurfaceBase
    FloatingSurfaceBase <|-- Toast
    IOverlayPositionConstraint <|.. Toast
```

## API

| Member              | Type                                           | Default    | Description                                                                    |
| ------------------- | ---------------------------------------------- | ---------- | ------------------------------------------------------------------------------ |
| Inherited `Content` | `ControlBase?`                                 | `null`     | Supplies arbitrary retained content owned by the Toast.                        |
| `Title`             | `string?`                                      | `null`     | Supplies the optional single-line header title.                                |
| `Adornment`         | `Affix?`                                       | `null`     | Supplies an optional grapheme before the title.                                |
| `Position`          | `ToastPosition`                                | `TopRight` | Selects one of six screen-edge stacks.                                         |
| `Animation`         | `ToastAnimation`                               | `Fade`     | Selects the deterministic entrance transition.                                 |
| `AnimationDuration` | `TimeSpan`                                     | `200 ms`   | Sets the non-negative entrance duration; zero completes immediately.           |
| `DisplayDuration`   | `TimeSpan`                                     | `5 s`      | Sets visible time after entrance; `Timeout.InfiniteTimeSpan` disables timeout. |
| `IsDismissible`     | `bool`                                         | `true`     | Enables the close affordance and keyboard or pointer dismissal.                |
| `Style`             | `ToastStyle?`                                  | `null`     | Sets one complete local presentation or uses themed Popup fallback.            |
| `ActualStyle`       | `ToastStyle`                                   | Resolved   | Reports the complete resolved presentation.                                    |
| `IsOpen`            | `bool`                                         | `false`    | Reports whether this Toast is currently presented.                             |
| `AnimationProgress` | `double`                                       | `0`        | Reports normalized entrance progress from zero through one.                    |
| `Show(owner)`       | `void`                                         | —          | Mounts the same Toast in the owner’s presentation plane.                       |
| `Dismiss()`         | `void`                                         | —          | Requests vetoable dismissal; a closed Toast is unchanged.                      |
| `CloseRequested`    | `EventHandler<SurfaceCloseRequestedEventArgs>` | —          | Allows cancellation before dismissal changes presentation.                     |
| `Closing`, `Closed` | `EventHandler`                                 | —          | Publish ordered lifecycle around successful dismissal.                         |

`ToastPosition` defines `TopLeft`, `TopCenter`, `TopRight`, `BottomLeft`,
`BottomCenter`, and `BottomRight`. `ToastAnimation` defines `SlideTop`,
`SlideDown`, `SlideLeft`, `SlideRight`, `Expand`, and `Fade`.

## Presentation and timing

`Show(owner)` resolves the attached owner’s stable presentation plane and mounts
the Toast object itself. It never creates a proxy, enters modality, or changes
focus. Showing an already open Toast or changing position, animation, or timing
while it is open throws. Resize recomputes the final edge slot against current
host bounds.

Animation progress derives from the dispatcher’s monotonic clock. `SlideLeft`
and `SlideRight` enter from the named side of the slot, `SlideDown` starts one
toast height above the slot and travels down, `SlideTop` starts at the host
content's top edge, `Expand` grows around its final center, and `Fade` reveals
stable cells in place. The display timer starts after entrance reaches one, so
animation does not consume visible lifetime. Detach and disposal cancel both
timers and release stack membership.

## Appearance and dismissal

`ToastStyle` extends `PopupStyle` with title, adornment, close-glyph, padding,
and spacing fields. `Info`, `Error`, `Warning`, `Success`, and `Trace` are
complete presets, while `Default` aliases `Info`. Severity remains an appearance
choice rather than a closed behavior enum; applications may assign any complete
custom `ToastStyle`. Every preset paints an opaque semantic popup background;
custom styles control that fill through `Face.Background`. Caller content keeps
its own appearance, so layout-only wrappers that should visually belong to the
Toast use a transparent background and inherit the Toast surface beneath them.

A dismissible focused Toast handles Escape, Enter, and Space. The close glyph
uses capture-aware pointer press and release semantics. Every route, including
the display timeout, raises `CloseRequested` first; cancellation leaves the
Toast open and suppresses `Closing` and `Closed`. A synchronous repeated
`Dismiss()` from that request is absorbed by the shared request guard, so one
request produces at most one close lifecycle. A vetoed display timeout leaves
its timer active, so the Toast retries after another `DisplayDuration` instead
of becoming permanently manual-only. Before `Closed` runs, the Toast has left
its coordinator and presentation host and the shared close guard has released. A
handler may therefore show the same Toast again as a distinct presentation; the
completed dismissal does not remove that replacement.

## Example

![The Toast control rendered in the live showcase](../../images/controls/toast.png)

![An error Toast rendered in the live showcase](../../images/controls/toast-error.png)

```csharp
var toast = new Toast
{
    Title = "Upload failed",
    Adornment = new Affix("!"),
    Position = ToastPosition.TopRight,
    Animation = ToastAnimation.SlideLeft,
    Style = ToastStyle.Error,
    Content = new Text("The server rejected the archive.")
};

toast.Show(uploadButton);
```

## Expected behavior

| Scope                 | Observable evidence                                                       |
| --------------------- | ------------------------------------------------------------------------- |
| Public API            | Validation, defaults, state changes, and deterministic output.            |
| Integrated behavior   | Cross-component behavior through the real ownership and routing boundary. |
| Complete runtime path | Final cells, lifecycle ordering, timer cleanup, and mounted input routes. |

- All six positions clamp to the live host and stack independently with newest
  nearest the selected edge.
- Every animation reaches the exact stable final slot from elapsed dispatcher
  time, and automatic dismissal begins only after entrance completes.
- Optional title, adornment, close affordance, arbitrary content, border, and
  semantic preset colors render as complete terminal cells.
- Showing preserves existing focus and modality; successful dismissal removes
  the identical Toast object and publishes `Closing` before `Closed`.
- A failing `Opened` observer rolls back the stack registration, public open
  state, animation state, and common presentation so the Toast can be shown
  again.
- Detach, hide, and disposal release timers, pointer state, and coordinator
  references without leaving a stale stack reservation.
