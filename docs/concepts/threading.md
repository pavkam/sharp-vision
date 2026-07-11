# Threading and dispatcher

## Threading contract

The dispatcher thread exclusively owns the visual tree, control properties,
styles, focus, pointer capture, layout, rendering, and user callbacks.

`CheckAccess` reports ownership; `VerifyAccess` throws before an invalid
mutation. `Post` queues fire-and-observe work with diagnostic failure handling.
`InvokeAsync` returns a completion representing execution, cancellation, or
exception on the dispatcher.

`Dispatcher.Start` creates one named background owner thread and a finite FIFO
queue (4,096 entries by default). `Post` rejects overflow before enqueue and
reports callback failures through `UnhandledException` outside the queue lock;
an unhandled failure stops the loop. `InvokeAsync` runs inline when already on
the owner, otherwise preserves result or exception identity and observes
cancellation before the queued callback begins. Shutdown rejects new work,
cancels queued invocations, waits for the active finite callback, and is
idempotent.

Transport readers, resize watchers, and background tasks enqueue immutable
records through thread-safe bounded queues. They never call controls. Queue
backpressure, coalescing policy, and shutdown behavior are explicit per record
type.

## Locks and reentrancy

No user callback, control method, layout callback, or renderer hook runs under
an internal lock. Dispatcher callbacks may enqueue work but nested run loops are
unsupported. Layout/render invalidation during callbacks is coalesced for the
next safe phase.

The queue resets an idle-transition flag whenever ready work arrives. Internal
pending-phase leases keep asynchronous frame output non-idle. When both ready
and pending work reach zero, `Idle` runs once on the owner thread; work posted
by that handler drains before another wait, and the loop blocks on a condition
rather than polling.

## Tests

Cover off-thread failure before mutation, posted/invoked success, exception and
cancellation propagation, FIFO ordering within priority, resize coalescing,
shutdown races, bounded queues, callback reentrancy attempts, and absence of
busy waits using a fake clock/waiter.
