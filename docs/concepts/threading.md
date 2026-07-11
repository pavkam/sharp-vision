# Threading and dispatcher

## Threading contract

The dispatcher thread exclusively owns the visual tree, control properties,
styles, focus, pointer capture, layout, rendering, and user callbacks.

`CheckAccess` reports ownership; `VerifyAccess` throws before an invalid
mutation. `Post` queues fire-and-observe work with diagnostic failure handling.
`InvokeAsync` returns a completion representing execution, cancellation, or
exception on the dispatcher.

Transport readers, resize watchers, and background tasks enqueue immutable
records through thread-safe bounded queues. They never call controls. Queue
backpressure, coalescing policy, and shutdown behavior are explicit per record
type.

## Locks and reentrancy

No user callback, control method, layout callback, or renderer hook runs under
an internal lock. Dispatcher callbacks may enqueue work but nested run loops are
unsupported. Layout/render invalidation during callbacks is coalesced for the
next safe phase.

## Tests

Cover off-thread failure before mutation, posted/invoked success, exception and
cancellation propagation, FIFO ordering within priority, resize coalescing,
shutdown races, bounded queues, callback reentrancy attempts, and absence of
busy waits using a fake clock/waiter.
