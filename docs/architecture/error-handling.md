# Error handling and diagnostics

## Error handling contract

Programmer errors throw immediately before observable mutation. Environmental
failures degrade or terminate through typed results/events and structured
diagnostics according to whether safe continuation is possible.

## Programmer errors

Examples include null required values, invalid dimensions/percentages, enum
values outside the contract, parenting cycles, cross-thread mutation,
disposed-object access, invalid span lengths, and ownership violations. Public
XML docs name the exception and validation rule.

## Environmental failures

Unsupported capabilities, missing query replies, malformed/oversized terminal
input, permission denial, timeout, transport closure, and cleanup failure are
environmental. The library preserves parser sync where possible, emits a
redacted diagnostic, and chooses the documented fallback. A disconnected
transport stops the application after restoration attempts.

## Diagnostic model

The protocol `Diagnostic` carries only a stable `DiagnosticCode`, sequence kind,
stream offset, and discarded-byte count. Its invariant-culture text contains
those structural fields and no input payload. Kitty packet values, clipboard
bytes, passwords, names, and terminal-provided secrets are never included.
Transaction state and sanitized correlation identifiers remain in their typed
objects rather than being copied into diagnostics. Severity, runtime operation,
and captured exceptions belong to later host-level diagnostic envelopes; they
are not falsely claimed by the protocol value.

## Exception preservation

Mode restoration and disposal execute in `finally`. When cleanup also fails, the
original exception remains primary and cleanup is attached diagnostically.
Unhandled application exceptions raise the runtime event once, then follow the
configured stop/continue policy at a safe dispatcher boundary.

`Renderer.LastCleanupException` and `Runtime.Session.LastCleanupException`
retain the first bounded recovery failure without replacing a primary write,
flush, startup, read, resize, input-handler, or cancellation exception. Normal
transport EOF is a typed closure callback, not a fabricated fault.
