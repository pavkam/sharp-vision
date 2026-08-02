# Lifecycle and runtime events

## Overview

The `Application` events are `Starting`, `Started`, `Stopping`, `Stopped`,
`Idle`, `UnhandledException`, `FrameRendered`, `Resize`, `Diagnostic`,
`ResponseReceived`, `PaletteResponseReceived`, `MetricsResponseReceived`,
`StatusResponseReceived`, `CapabilityResponseReceived`, and
`CapabilitiesChanged`. Terminal key, text, pointer, paste, and focus values
enter the control tree through typed routed events. Session closure and faults
drive shutdown instead of leaking terminal callbacks through the UI API.

Every `FloatingSurface` that closes through the shared `CloseSurface` engine
(the whole `Popup` family, and `Dialog<TResult>`'s typed completion) first
raises `CloseRequested`, a pre-commit veto hook
(`SurfaceCloseRequestedEventArgs.Cancel`) that leaves the surface untouched and
skips `Closing`/`Closed` entirely when set. `Popup` then publishes `Closing` and
`Closed`. `Window` publishes a `Closing` request and, by default, collapses
itself afterward: the visibility transaction completes the common cleanup and
the close request then publishes `Closed`. A `Closing` handler that itself
changes visibility takes responsibility for the outcome instead — if it leaves
the Window visible and presented, `Closed` is suppressed. Changing a Window's
visibility directly performs the cleanup without publishing `Closing` or
`Closed`. Window's close affordance, `CloseOnEscape`, and modal dismiss do not
yet raise `CloseRequested` — see
[floating-surfaces.md](floating-surfaces.md#shared-lifecycle). Modal scopes
publish `DismissRequested` and one committed `Exited` notification under the
[modal lifetime contract](modality.md#nested-scopes-and-lifetime).

## Ordering

```mermaid
sequenceDiagram
    participant Host
    participant Application
    participant Session
    participant Root
    participant Renderer

    Host->>Application: StartAsync
    Application-->>Host: Starting
    Application->>Session: Start input, resize, and mode leases
    Session-->>Application: Capability profile and first resize
    Application-->>Host: CapabilitiesChanged
    Application->>Root: Attach, measure, and arrange
    Application-->>Host: Resize
    Application->>Renderer: Render, write, and flush first frame
    Renderer-->>Application: Frame committed
    Application-->>Host: FrameRendered
    Application-->>Host: Started
    Application-->>Host: Idle when no work remains

    Host->>Application: StopAsync
    Application-->>Host: Stopping
    Application->>Session: Reverse cleanup and dispose ownership
    Application-->>Host: Stopped
```

`Starting` fires before terminal modes are exposed to controls. `Started` fires
after the initial capabilities, root layout, and first committed frame. The
cancellable `Stopping` request occurs once, and `Stopped` fires after cleanup
attempts and pending invocation completion.

The five response events are dispatcher-affine and preserve their mutual
transport order across numeric, palette, metrics, DECRQSS, and XTGETTCAP
records. Typed DCS records received during startup negotiation stay queued
behind the initial capability publication and then dispatch in their original
order; matched, unsolicited, duplicate, and late classifications do not suppress
them. `StatusResponseEventArgs` rejects an empty status sentinel, and
`CapabilityResponseEventArgs` rejects null. Both expose application-owned data
that remains valid after parser callbacks return and the session read buffer is
reused. The
[runtime routing contract](../protocols/runtime-routing.md#inbound-consumption-surface)
owns the complete inbound surface.

Initial root attachment is one staged ownership publication. The application
first commits the dispatcher, Unicode policy, and theme context, installs the
focus, pointer-capture, and modality managers across the tree, and only then
invokes the control `OnAttached` callbacks. A callback can therefore use the
protected focus and capture helpers immediately, enter a modal scope through the
application service, and observe every sibling with the same complete inherited
context. The application root supplied by the host must be both detached and
unowned.

Runtime insertion, removal, replacement, and disposal use the
[owned-control transaction](../controls/control.md#children-and-ownership).
Removal first performs guarded availability cleanup against the still-coherent
old tree: focus is released, capture state clears before the cancellation
callbacks run, and active modal scopes remove unavailable included roots or
unwind from an unavailable primary root before `OnUnavailable`. Disposing a
control that owns focus, capture, or modality may perform the corresponding root
cleanup after `OnUnavailable`. Membership, parent, dispatcher, Unicode, theme,
and manager context then commit as one new tree. Parent, theme, detached, and
attached notifications publish from committed state, and the slot impact is
invalidated exactly once before the slot notification. A callback failure cannot
roll the tree back or suppress the later cleanup; an unexpected earlier failure
still requests invalidation from the transaction's `finally` path, and the first
failure is rethrown afterward. Direct child disposal uses only
`ReleaseReason.Disposed`, even though clearing the attached context still
publishes the normal `OnDetached` lifecycle hook.

Resize follows the ordering in the
[runtime event loop](../architecture/runtime-event-loop.md#resize-ordering).
`FrameRendered` reports only a completed transport write and its damage and byte
metrics. Failed frames produce diagnostics and force invalidation instead.

`Idle` fires once per transition into a state with no ready or pending work,
after input, timer callbacks, layout, and rendering, directly before the loop
waits. `DispatcherTimer` posts coalesced ordinary dispatcher work and never
emulates ticks by repeatedly invoking idle callbacks. A tick that invalidates
rendering is followed by the normal render and frame-completion order before the
next application idle transition.

The dispatcher primitive enforces the empty ready/pending transition and the
handler-posted-work rule. `Application` connects terminal input, layout, and
renderer pending leases to that primitive. A render holds one pending lease
until its completion callback runs on the dispatcher, so `Idle` can never
precede flush, `FrameRendered`, or `Started`.

The terminal `Runtime.Session` supplies ordered resize, input, closure, and
fault records plus reversible mode ownership. It does not raise the application
starting, started, stopping, stopped, frame-rendered, or `Idle` events; the
application dispatcher owns those callbacks. This separation keeps transport
waits from masquerading as application idleness.

Zero-cell dimensions are delivered as a valid suspended `Dimensions` value.
Positive cell and pixel dimensions derive `Geometry.Metrics` only when both axes
produce a positive cell size. `Application` coalesces those terminal records
into committed layout and the public resize event ordering described above.

## Expected behavior

The exact startup and shutdown, resize, timer, frame, exception, and idle
orderings hold as described, including under cancellation, handler exceptions,
work queued from event handlers, timer-driven invalidation, invalidation from
resize handlers, transport failure, repeated stop requests, modal unwind with
exit-callback failure, and fake waits that never spin. The five response events
keep their mutual transport order, including queued startup DCS records, and
their values remain owned and valid after the transport buffer is reused.
