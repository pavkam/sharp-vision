# Threading and dispatcher

## Threading contract

The dispatcher thread exclusively owns the attached visual tree, control
properties, style assignment, focus, pointer capture, layout, rendering, and
user callbacks. A mutable `Style` resource may be changed from another thread;
attached subscribers marshal its invalidation back to their dispatcher before
touching control state.

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

`Application` holds a pending lease while renderer I/O is incomplete. The lease
may be released from a renderer continuation, but frame and lifecycle callbacks
are posted back first and run on the owner. The bounded input queue and
newest-resize slot use short locks only to copy records and schedule one wake;
no user callback or terminal I/O runs under them.

## Tests

Cover off-thread failure before mutation, posted/invoked success, exception and
cancellation propagation, FIFO ordering within priority, resize coalescing,
shutdown races, bounded queues, callback reentrancy attempts, and absence of
busy waits using a fake clock/waiter.
