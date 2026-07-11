# Synchronized output

## Synchronized output contract

Synchronized output uses DEC private mode 2026: `CSI ? 2026 h` begins a buffered
update and `CSI ? 2026 l` ends it. It is a terminal extension rather than an
ECMA-48 guarantee and must be capability-gated.

The renderer opens the mode only around a complete encoded frame and writes the
closing sequence in a `finally` path. Nested frame writers share one lease.
Cancellation or transport failure records terminal state as unknown and forces
full invalidation before later incremental output.

The library never holds mode 2026 while waiting for input, timers, application
callbacks, or backpressure beyond the current bounded write operation.

## First milestone contract

Use synchronized output when explicitly detected or overridden. Otherwise emit
the same frame without the wrapper. Safe degradation changes presentation
atomicity, never screen contents.

## Tests

Exact bytes cover enabled/disabled profiles, empty/no-op frames, nested leases,
exceptions, cancellation, partial writes, and cleanup failure. A terminal model
proves wrapped and unwrapped frames end in identical state.
