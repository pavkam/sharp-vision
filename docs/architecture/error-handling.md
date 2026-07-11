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

Diagnostics carry stable category, severity, operation, protocol/feature, offset
or correlation identifier where safe, and an exception when one exists.
Clipboard payloads, passwords, permission tokens, and terminal-provided secrets
are never included by default. No logging framework dependency is required.

## Exception preservation

Mode restoration and disposal execute in `finally`. When cleanup also fails, the
original exception remains primary and cleanup is attached diagnostically.
Unhandled application exceptions raise the runtime event once, then follow the
configured stop/continue policy at a safe dispatcher boundary.
