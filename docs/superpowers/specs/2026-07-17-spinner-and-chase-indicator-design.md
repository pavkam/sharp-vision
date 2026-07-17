# Spinner and Chase Indicator Design

## Goal

Add two non-interactive animated display controls and the dispatcher-owned timer
primitive required to drive them deterministically. `Spinner` presents one
changing glyph. `ChaseIndicator` moves one active glyph through a fixed-length
horizontal track and reverses direction at each endpoint.

Both controls follow the shared
[control](../../controls/control.md#control-contract),
[Unicode geometry](../../concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract),
[threading](../../concepts/threading.md#threading-contract), and
[lifecycle](../../concepts/lifecycle-events.md#lifecycle-event-contract)
contracts. They draw semantic cells only and never emit terminal escape bytes.

## Dispatcher timer

`SharpVision.Threading.DispatcherTimer` is a public sealed, disposable,
dispatcher-affine periodic timer. Its constructor accepts a non-null
`Dispatcher` and an interval from 1 through 2,147,483,647 milliseconds,
inclusive. It exposes `Interval`, read-only `IsRunning`, a `Tick` event, and
`Start`, `Stop`, and `Dispose` methods. Construction does not start the timer.

`Dispatcher.Start` accepts an optional `TimeProvider` and uses
`TimeProvider.System` by default. Every timer associated with that dispatcher
uses the same provider. `Application` accepts an optional final `TimeProvider`
constructor argument and passes the resolved provider to its dispatcher and
other time-aware owned services. This gives one application a coherent clock and
lets mounted tests advance time without wall-clock sleeps.

The provider callback never invokes a user callback or touches a control. It
atomically requests at most one queued dispatcher tick. A busy dispatcher
therefore skips elapsed periods rather than accumulating callbacks or producing
a catch-up burst. A full dispatcher queue drops that period and permits a later
period to try again. Dispatcher shutdown stops the timer without surfacing a
background-thread exception.

`Tick` runs on the owning dispatcher outside internal locks. Handler failures
follow the dispatcher's existing unhandled-exception policy. Changing `Interval`
while running schedules the next tick one complete new interval after the
committed change. `Stop` and `Dispose` invalidate a callback that was posted but
has not begun, so it cannot raise a later tick. `Dispose` is idempotent and may
be called from any thread. Start, stop, interval mutation, and event delivery
remain dispatcher-affine.

The first tick occurs after one complete interval. Initial control frames are
rendered through ordinary attachment and invalidation rather than an immediate
timer callback. The timer is independent of `Idle`; animation never creates an
idle-driven polling loop.

## Spinner

`Spinner` is a sealed `Control` with these public properties:

| Property    | Type             | Default          | Contract                                           |
| ----------- | ---------------- | ---------------- | -------------------------------------------------- |
| `Pattern`   | `SpinnerPattern` | `Braille`        | Selects one built-in frame sequence.               |
| `Interval`  | `TimeSpan`       | 200 milliseconds | Positive supported timer interval.                 |
| `IsPlaying` | `bool`           | `true`           | Enables periodic frame advancement while attached. |

`SpinnerPattern` is a public enum with the following exact cyclic sequences:

| Value          | Frames       |
| -------------- | ------------ |
| `Braille`      | `⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏` |
| `DenseBraille` | `⣿⣷⣯⣟⡿⢿⣻⣽`   |
| `Ascii`        | `\|/-\\`     |

The control measures one by one cell, uses left and top alignment, cannot take
focus, and is excluded from pointer hit testing. Rendering writes the current
frame at the first content cell and safely emits nothing for empty bounds.
Braille patterns are one neutral-width cell under the pinned Unicode policy; the
ASCII pattern is the explicit maximum-compatibility choice. Font-glyph
availability is not inferred from terminal capabilities.

Changing `Pattern` validates the enum, resets the frame index to zero, and
invalidates rendering without relayout. Assigning the current value is silent.

## ChaseIndicator

`ChaseIndicator` is a sealed `Control` with these public properties:

| Property    | Type           | Default          | Contract                                                   |
| ----------- | -------------- | ---------------- | ---------------------------------------------------------- |
| `Pattern`   | `ChasePattern` | `Circle`         | Selects one built-in active/inactive glyph pair.           |
| `Length`    | `int`          | `5`              | Number of horizontal terminal cells; must be at least two. |
| `Interval`  | `TimeSpan`     | 200 milliseconds | Positive supported timer interval.                         |
| `IsPlaying` | `bool`         | `true`           | Enables periodic position advancement while attached.      |

`ChasePattern` defines these exact narrow-policy glyph pairs:

| Value     | Active | Inactive | Wide-policy fallback |
| --------- | ------ | -------- | -------------------- |
| `Circle`  | `●`    | `◯`      | `@` and `o`          |
| `Diamond` | `◆`    | `◇`      | `*` and `.`          |
| `Square`  | `■`    | `□`      | `#` and `.`          |
| `Up`      | `▲`    | `△`      | `^` and `.`          |
| `Down`    | `▼`    | `▽`      | `v` and `.`          |
| `Left`    | `◀`    | `◁`      | `<` and `.`          |
| `Right`   | `▶`    | `▷`      | `>` and `.`          |

The Unicode pairs are East Asian Width ambiguous. The control resolves each pair
against the inherited cell policy before drawing and uses the documented ASCII
pair when ambiguous characters are wide. `Length` therefore always means
terminal cells, and the desired size is always `Length` by one.

The active position begins at zero and follows
`0, 1, ..., Length - 1, Length - 2, ..., 1, 0`. Each endpoint appears once per
bounce. Changing `Pattern` or `Length` resets the position to zero and the next
direction to forward. Pattern changes invalidate render; length changes
invalidate measure. Unknown enum values and lengths below two fail before any
state, timer, or notification changes.

`ChaseIndicator` uses left and top alignment, cannot take focus, and is excluded
from pointer hit testing. It clips through the supplied canvas and safely emits
nothing for empty or narrower arranged bounds.

## Playback lifecycle

Each attached control owns one `DispatcherTimer`. `OnAttached` creates and
starts it when `IsPlaying` is true. `OnDetached` stops and disposes it. A posted
tick that loses the attachment race becomes a no-op. Reattachment resumes from
the retained frame or position after a complete interval.

`OnDisposing` also stops and disposes the timer before attached-root disposal
clears dispatcher context. Detachment followed by disposal remains safe because
timer disposal is idempotent.

Setting `IsPlaying` to false stops the timer and retains the current phase.
Setting it to true starts a fresh interval without resetting phase. Changing
`Interval` validates before mutation and restarts a running timer from the new
interval. Every valid setter is dispatcher-affine after attachment.

A tick advances and invalidates render only when the control is effectively
visible and playing. Hidden or collapsed controls, including controls hidden by
an ancestor, retain phase and produce no render invalidation. They resume after
one subsequent eligible tick. Disabled state does not pause a non-interactive
status indicator.

The two controls own their small phase state directly. They share
`DispatcherTimer` but do not introduce an animated-control base class, virtual
frame provider, custom glyph collection, or animation framework.

## Documentation and showcase

Dedicated `Spinner` and `ChaseIndicator` control specifications join the
display-control catalog. The threading, lifecycle, and control-testing documents
gain the timer and deterministic-animation rules. Public and internal members
receive XML documentation covering defaults, units, lifecycle, validation,
dispatcher affinity, and disposal behavior.

The [Showcase](../../architecture/showcase.md#showcase-contract) gains separate
Spinner and ChaseIndicator pages. The Spinner page displays all three running
patterns plus a paused specimen. The ChaseIndicator page displays all seven
patterns, a non-default length, and a paused specimen. Adjacent text states the
200-millisecond default, `IsPlaying` behavior, wide-policy fallback, and the
fixed built-in pattern scope. Gallery inventory and expected page counts update
in the same change.

## Correctness evidence

`DispatcherTimer` tests use a manual `TimeProvider` and prove the first due
time, periodic delivery on the dispatcher thread, interval replacement,
start/stop/restart, idempotent disposal, validation before mutation, dropped
catch-up periods, one pending callback under dispatcher blockage, queued-tick
suppression, queue saturation, dispatcher shutdown, and handler-failure
reporting.

Spinner tests cover defaults, every exact frame sequence, 200-millisecond
cadence, pattern reset, pause/resume without reset, interval restart,
attach/detach/reattach, effective visibility, dispatcher affinity, validation,
one-cell measurement, zero/tiny bounds, and non-interactive state. Mounted
surface tests advance a deterministic clock and compare consecutive semantic
screens.

ChaseIndicator tests cover every glyph pair, lengths two and five, the complete
forward/reverse cycle without duplicate endpoints, pattern and length reset,
wide-policy fallbacks, clipping, resize, lifecycle, visibility, pause/resume,
validation, exact invalidation impact, and non-interactive state. Mounted
surface tests prove consecutive final frames for both narrow and wide cell
policies.

Showcase tests verify both catalog entries, overview and example content,
representative semantic frames, paused specimens, and live advancement. Focused
red/green runs precede `make format`, `make lint`, `make build`, and
`make test`.

## Out of scope

Version one does not accept caller-defined frame collections, arbitrary glyph
pairs, variable per-frame durations, multiline sprites, vertical chase tracks,
easing, completion events, finite repeat counts, or synchronized groups. Those
features require separate product contracts rather than widening these two
deliberately small indicators.
