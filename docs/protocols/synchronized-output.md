# Synchronized output

## Synchronized output contract

Synchronized output uses DEC private mode 2026: `CSI ? 2026 h` begins a buffered
update and `CSI ? 2026 l` ends it. It is a terminal extension rather than an
ECMA-48 guarantee and must be capability-gated.

The renderer includes both mode transitions in one complete bounded batch only
when that batch contains frame output. Cancellation or transport failure records
terminal state as unknown, attempts a separate disable-and-flush under a finite
independent timeout, and forces full invalidation before later incremental
output.

The library never holds mode 2026 while waiting for input, timers, application
callbacks, or backpressure beyond the current bounded write operation.

## First milestone contract

Use synchronized output when explicitly detected or overridden. Otherwise emit
the same frame without the wrapper. Safe degradation changes presentation
atomicity, never screen contents.

## Tests

Exact bytes cover enabled/disabled profiles, empty/no-op frames, exceptions,
cancellation, partial writes, flush failure, and cleanup failure. A terminal
model proves wrapped and unwrapped frames end in identical semantic state.

## Sources

- [Contour synchronized output specification](https://contour-terminal.org/vt-extensions/synchronized-output/)
  defines DEC private mode 2026, DECRQM detection, enable and disable sequences,
  and buffer-presentation behavior.
- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines the underlying CSI grammar; mode 2026 itself is an extension.

Sources accessed 2026-07-17.

## Test obligations

| Layer      | Required evidence                                                             |
| ---------- | ----------------------------------------------------------------------------- |
| Capability | DECRQM support states, explicit override, and unsupported fallback.           |
| Encoder    | Exact mode 2026 enable/frame/disable bytes and no empty-frame wrapper.        |
| Failure    | Partial writes, cancellation, and shutdown attempt exact reverse restoration. |
