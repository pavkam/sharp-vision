# Lifecycle and runtime events

## Lifecycle event contract

The `Application` events are `Starting`, `Started`, `Stopping`, `Stopped`,
`Idle`, `UnhandledException`, `FrameRendered`, `Resize`, `Diagnostic`,
`ResponseReceived`, `PaletteResponseReceived`, `MetricsResponseReceived`,
`StatusResponseReceived`, `CapabilityResponseReceived`, and
`CapabilitiesChanged`. Terminal key, text, pointer, paste, and focus values
enter the control tree through typed routed events. Session closure and faults
drive shutdown rather than leaking terminal callbacks through the UI API.
`Popup` publishes `Closing` and `Closed`. `Window` publishes an owner-handled
`Closing` request while remaining visible and presented. When that handler hides
the Window, the visibility transaction completes common cleanup and the close
request publishes `Closed`; retaining visibility suppresses `Closed`. A direct
Window visibility change performs cleanup without publishing `Closing` or
`Closed`. Modal scopes publish `DismissRequested` and one committed `Exited`
notification under the
[modal lifetime contract](modality.md#nested-scopes-and-lifetime).

## Ordering

Starting occurs before terminal modes are exposed to controls. Started occurs
after initial capabilities, root layout, and first committed frame. A
cancellable stopping request occurs once; stopped occurs after cleanup attempts
and pending invocation completion.

The five response events are dispatcher-affine and retain mutual transport order
across numeric, palette, metrics, DECRQSS, and XTGETTCAP records. Typed DCS
records received during startup negotiation remain queued behind initial
capability publication, then dispatch in their original order; matched,
unsolicited, duplicate, and late classifications do not suppress them.
`StatusResponseEventArgs` rejects an empty status sentinel, and
`CapabilityResponseEventArgs` rejects null. Both expose application-owned data
which remains valid after parser callbacks return and the session read buffer is
reused. The
[runtime routing contract](../protocols/runtime-routing.md#inbound-consumption-surface)
owns the complete inbound surface.

Initial root attachment is one staged ownership publication. The application
first commits dispatcher, Unicode policy, and theme context, installs focus and
pointer-capture and modality managers across the tree, and only then invokes
control `OnAttached` callbacks. A callback can therefore use protected focus or
capture helpers immediately, enter a modal scope through the application
service, and observe every sibling with the same complete inherited context. A
supplied application root must be both detached and unowned.

Runtime insertion, removal, replacement, and disposal use the
[owned-control transaction](../controls/control.md#children-and-ownership).
Removal first performs guarded availability cleanup against the coherent old
tree: focus releases, capture state clears before cancellation callbacks, and
active modal scopes remove unavailable included roots or unwind from an
unavailable primary root before `OnUnavailable`. Disposal of a control that owns
focus, capture, or modality may perform the corresponding root cleanup after
`OnUnavailable`. Membership, parent, dispatcher, Unicode, theme, and manager
context then commit as one new tree. Parent, theme, detached, and attached
notifications publish from committed state; the slot impact is then invalidated
exactly once before the slot notification. A callback failure cannot roll the
tree back or suppress later cleanup; an unexpected earlier failure still
requests invalidation from the transaction's `finally` path, and the first
failure is rethrown afterward. Direct child disposal uses only
`ReleaseReason.Disposed`, even though clearing attached context still publishes
the normal `OnDetached` lifecycle hook.

Resize follows the ordering in the
[runtime event loop](../architecture/runtime-event-loop.md#resize-ordering).
Frame rendered reports only a completed transport write and its damage/byte
metrics. Failed frames produce diagnostics and force invalidation instead.

`Idle` fires once per transition into no ready or pending work, after input,
timer callbacks, layout, and rendering, directly before waiting.
`DispatcherTimer` posts coalesced ordinary dispatcher work and never emulates
ticks by repeatedly invoking idle callbacks. A tick that invalidates rendering
is followed by the normal render and frame-completion order before the next
application idle transition.

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

## Test obligations

Record exact startup/shutdown, resize, timer, frame, exception, and idle order.
Cover cancellation, handler exceptions, work queued from events, timer-driven
invalidation, invalidation from resize handlers, transport failure, repeated
stop, modal unwind and exit-callback failure, and no-spin fake waits. Cover the
five response events in mutual transport order, including queued startup DCS
records and owned values after transport-buffer reuse.
