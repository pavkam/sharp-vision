# DEC private modes

## Overview

DECSET is `CSI ? Pm h`, DECRST is `CSI ? Pm l`, and DECRQM queries a mode with
`CSI ? Ps $ p`; DECRPM replies with `CSI ? Ps ; Pm $ y`. Mode meanings are
cross-checked against
[xterm control sequences](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
and DEC manuals, accessed 2026-07-11.

Modes are typed identifiers with capability requirements and cleanup policy.
They are not arbitrary integers passed from controls. Enabling a mode records
the restoration action. Nested owners use leases so one component cannot disable
a mode still needed by another.

## Supported features

Typed modes cover cursor-key/application keypad behavior, origin/wrap, cursor
visibility, alternate screen, mouse families, focus 1004, bracketed paste 2004,
synchronized output 2026, and Kitty clipboard paste 5522. Queries are bounded
and correlated; response values 0 and 4 mean unsupported where the defining
protocol states that rule, and value 3 ("permanently set") means supported for
every mode except 2026, whose value encodes an in-progress update rather than a
feature toggle.

## Restoration lifecycle

Shutdown, cancellation, transport failure, and exceptions attempt reverse-order
restoration. Cleanup failure is diagnostic and never hides the original error.
The observable evidence covers nesting, duplicate enable/disable, partial
initialization, missing or contradictory responses, and every failure exit.

Session leases own exact enable and disable bytes captured before their first
write. Description lifecycle modes use complete compiled pairs; typed focus,
paste, mouse, and Kitty keyboard modes use their validated protocol encoders
only with supported database, bounded-query, or explicit-override evidence. A
default or environment-only origin is never enough for optional mode output.
Every attempted enable is recorded before transport I/O, so a partial write,
cancellation, or failed flush receives the exact conservative restoration
attempt. Cleanup continues through later leases in reverse order and preserves
the original exception.

## Typed API and behavior

`Modes` provides exact DECSET/DECRST bytes for modes 9, 25, 1000, 1002, 1003,
1004, 1005, 1006, 1015, 1016, 1049, 2004, 2026, and 5522. `Csi.QueryPrivateMode`
emits DECRQM; `XtermResponses.TryCsi` validates DECRPM and maps states 1/2/3 to
supported while 0/4 remain unsupported, except mode 2026 where state 3 is also
treated as unsupported (see above). `QueryTracker` bounds in-flight queries,
rejects ambiguous duplicate uncorrelated requests, correlates Kitty IDs, and
distinguishes duplicate from late replies using an injected `TimeProvider`. Mode
ownership and reverse-order terminal restoration are implemented by
[`Runtime.Session`](../architecture/runtime-event-loop.md#terminal-session-implementation).

`Modes.Mouse` validates the tracking and coordinate enums before writing. Enable
order is tracking then coordinate encoding; disable order is coordinate then
tracking. This makes each pair reversible and prevents partially written output
for invalid combinations.

## Sources

- [XTerm Control Sequences, patch level 410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines the supported DECSET, DECRST, DECRQM, and DECRPM forms.

Source accessed 2026-07-28.

## Expected behavior

| Layer     | Required evidence                                                               |
| --------- | ------------------------------------------------------------------------------- |
| Encoder   | Exact enable, disable, and query bytes for every typed mode.                    |
| Ownership | Nested leases, partial acquisition, duplicate release, and reverse cleanup.     |
| Query     | Supported/unsupported/unknown, duplicate, late, malformed, and timeout replies. |
