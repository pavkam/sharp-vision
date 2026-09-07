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
synchronized output 2026, grapheme clustering 2027 (see
[grapheme clustering](grapheme-clustering.md#overview)), and Kitty clipboard
paste 5522. Queries are bounded and correlated; response values 0 and 4 mean
unsupported where the defining protocol states that rule, and value 3
("permanently set") means supported for every mode except 2026, whose value
encodes an in-progress update rather than a feature toggle. Mode 2027 has no
such carve-out: a terminal reporting it permanently set is simply always
measuring grapheme clusters the way this library does, which is a usable
feature toggle rather than an in-progress update.

## Restoration lifecycle

Shutdown, cancellation, transport failure, and exceptions attempt reverse-order
restoration. Cleanup failure is diagnostic and never hides the original error.
The observable evidence covers nesting, duplicate enable/disable, partial
initialization, missing or contradictory responses, and every failure exit.

Session leases own exact enable and disable bytes captured before their first
write. Description lifecycle modes use complete compiled pairs; typed focus,
paste, mouse, Kitty keyboard, and xterm modifyOtherKeys leases use their
validated protocol encoders only with supported database, bounded-query, or
explicit-override evidence. A default or environment-only origin is never enough
for optional mode output. Every attempted enable is recorded before transport
I/O, so a partial write, cancellation, or failed flush receives the exact
conservative restoration attempt. Cleanup continues through later leases in
reverse order and preserves the original exception.

Modes 2026 and 2027 are the two exceptions to the session-lease pattern above:
both are read directly off `TerminalCapabilities` by `Renderer` instead of
being owned by a `Session` lease, because both are consumed during rendering
rather than tied to a session-lifetime enable/disable toggle. See
[synchronized output](synchronized-output.md#overview) and
[grapheme clustering](grapheme-clustering.md#overview) for their respective
timing: mode 2026 brackets every non-empty frame batch, while mode 2027 is
asserted once for the renderer's lifetime and reversed at shutdown.

## Typed API and behavior

`ProtocolModes` provides exact DECSET/DECRST bytes for modes 9, 25, 1000, 1002,
1003, 1004, 1005, 1006, 1015, 1016, 1049, 2004, 2026, 2027, and 5522.
`Csi.QueryPrivateMode` emits DECRQM; `XtermResponses.TryCsi` validates DECRPM
and maps states 1/2/3 to supported while 0/4 remain unsupported, with no
mode-specific exceptions of its own. `ActiveQueryDiscoveryStrategy.Accept`
applies the mode 2026 override afterward, treating state 3 as unsupported for
that mode only (see above). `QueryTracker` bounds in-flight queries, rejects
ambiguous duplicate uncorrelated requests, correlates Kitty IDs, and
distinguishes duplicate from late replies using an injected `TimeProvider`. Mode
ownership and reverse-order terminal restoration are implemented by
[`Runtime.Session`](../architecture/runtime-event-loop.md#terminal-session-implementation).

`ProtocolModes.Mouse` validates the tracking and coordinate enums before
writing. It enables the coordinate encoding before event tracking and disables
event tracking before the coordinate encoding. Every event emitted while
tracking is active therefore uses the decoder's selected grammar. Default
coordinates omit the coordinate-mode writes entirely.

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
