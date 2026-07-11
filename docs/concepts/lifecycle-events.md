# Lifecycle and runtime events

## Lifecycle event contract

Application events are starting, started, stopping, stopped, idle, unhandled
exception, and frame rendered. Terminal events are resize, terminal focus,
key/text/pointer/paste, protocol response, transport closed, and transport
faulted. Window/control events include opening/opened, closing/closed, size and
layout changes, focus, and routed input.

## Ordering

Starting occurs before terminal modes are exposed to controls. Started occurs
after initial capabilities, root layout, and first committed frame. A
cancellable stopping request occurs once; stopped occurs after cleanup attempts
and pending invocation completion.

Resize follows the ordering in the
[runtime event loop](../architecture/runtime-event-loop.md#resize-ordering).
Frame rendered reports only a completed transport write and its damage/byte
metrics. Failed frames produce diagnostics and force invalidation instead.

`Idle` fires once per transition into no ready work, after input, due timers,
layout, and rendering, directly before waiting. Scheduled ticks are separate and
never implemented by repeated idle callbacks.

## Tests

Record exact startup/shutdown, resize, frame, exception, and idle order. Cover
cancellation, handler exceptions, work queued from events, invalidation from
resize handlers, transport failure, repeated stop, and no-spin fake waits.
