# Lifecycle and runtime events

## Lifecycle event contract

The Phase 4 `Application` events are `Starting`, `Started`, `Stopping`,
`Stopped`, `Idle`, `UnhandledException`, `FrameRendered`, `Resize`, and
`Diagnostic`. Terminal key, text, pointer, paste, and focus values enter the
control tree through typed routed events. Session closure and faults drive
shutdown rather than leaking terminal callbacks through the UI API. Phase 5
windows and popups add their own opening/opened and closing/closed events.

## Ordering

Starting occurs before terminal modes are exposed to controls. Started occurs
after initial capabilities, root layout, and first committed frame. A
cancellable stopping request occurs once; stopped occurs after cleanup attempts
and pending invocation completion.

Resize follows the ordering in the
[runtime event loop](../architecture/runtime-event-loop.md#resize-ordering).
Frame rendered reports only a completed transport write and its damage/byte
metrics. Failed frames produce diagnostics and force invalidation instead.

`Idle` fires once per transition into no ready or pending work, after input,
layout, and rendering, directly before waiting. Phase 4 exposes no timer or tick
API; a later scheduler must post ordinary dispatcher work and must never emulate
ticks by repeatedly invoking idle callbacks.

The dispatcher primitive enforces the empty ready/pending transition and
handler-posted-work rule. `Application` now connects terminal input, layout, and
renderer pending leases to that primitive. A render holds one pending lease
until its completion callback runs on the dispatcher, so `Idle` cannot precede
flush, `FrameRendered`, or `Started`.

The terminal `Runtime.Session` supplies ordered resize, input, closure, and
fault records plus reversible mode ownership. It does not raise application
starting/started/stopping/stopped, frame-rendered, or `Idle`; the application
dispatcher owns those callbacks. This separation prevents transport waits from
masquerading as application idleness.

Zero-cell dimensions are delivered as a valid suspended `Dimensions` value.
Positive cell and pixel dimensions derive `Geometry.Metrics` only when both axes
produce a positive cell size. `Application` coalesces those terminal records
into committed layout and the public resize event ordering described above.

## Tests

Record exact startup/shutdown, resize, frame, exception, and idle order. Cover
cancellation, handler exceptions, work queued from events, invalidation from
resize handlers, transport failure, repeated stop, and no-spin fake waits.
