# Device attributes and capability queries

## Overview

DA1 uses `CSI c` or `CSI 0 c`; DA2 uses `CSI > c`. DEC private mode reports use
DECRQM/DECRPM as specified in the
[DEC mode contract](dec-private-modes.md#overview). xterm and Kitty add color,
cell-size, keyboard, graphics, and clipboard queries.

Queries are typed transactions with correlation where the protocol supplies an
identifier. Startup applies environment/multiplexer hints first, sends only safe
bounded queries, and completes after all replies or a configured timeout. Late
and unsolicited replies remain observable without mutating an immutable
published `Capabilities` instance.

Terminal replies are untrusted input. Numeric, textual, Base64, and color fields
are bounded and validated before use. Replies cannot enable behavior outside the
query's declared feature.

## Supported features

Support DA1/DA2, relevant DECRQM modes, cell/pixel metrics, and the Kitty
keyboard and graphics probes needed by implemented runtime features, plus the
finite
[xterm DECRQSS and XTGETTCAP subset](xterm.md#status-and-capability-queries).
Callers may override every detected value for SSH, tmux, GNU screen, CI, and
known lies. Kitty OSC 5522 defines a DECRQM probe and typed correlation family,
but the startup batch does not issue it; see the
[Kitty clipboard implementation gap](kitty-clipboard.md#supported-features).

## Detection outcomes

No response, partial, late, duplicate, conflicting, or spoofed responses leave
the profile conservative. Multiplexer hints remain subordinate to explicit
overrides, published capability values stay immutable, and timeouts never
promote unknown support.

## Typed API and behavior

`Responses.TryCsi` owns validated DA1, DA2, cursor-position DSR, and DECRPM
values. `Responses.TryMetricsCsi` accepts only the xterm window-operation
reports `CSI 4 ; height ; width t`, `CSI 6 ; height ; width t`, and
`CSI 8 ; height ; width t`; both extents must be from 1 through 65535 before a
`MetricsResponse` is constructed. `Responses.TryOsc` owns `PaletteResponse`
values for OSC 4, OSC 10, and OSC 11. One-to-four-digit hexadecimal RGB
components are validated and normalized to 16-bit values before publication. The
[runtime router](runtime-routing.md#overview) delivers those typed values
without allowing them to fall through as keyboard input. `QueryTracker` admits
at most `QueryLimits.MaxConcurrentQueries`, one active uncorrelated query per
family, and distinct Kitty clipboard IDs. Completed, cancelled, and timed-out
correlations remain in a bounded grace window so duplicates and late replies
cannot mutate a published profile.

`StatusResponse` and `CapabilityResponse` own the validated DCS families. Typed
tracker registration retains the exact `StatusName` or `CapabilityName`;
family-only registration is invalid. Wrong-name and identity-less failure
replies remain observable and cannot consume an exact active request. Unknown
valid DECRQSS replies are observable diagnostics. XTGETTCAP accepts only the
finite public allowlist and strict bounded hex pairs; its parser-owned response
snapshots only a non-empty success or empty failure. It cannot query arbitrary
resources or transplant raw program bytes into a terminal profile.

Kitty graphics probe decoding belongs to the graphics extension and is
documented there.

DA1 parameter 4 is the sixel query boundary. A validated primary-attributes
reply containing 4 publishes supported query evidence; a reply omitting it
publishes unsupported query evidence. Explicit `Settings.Sixel` true or false
retains override precedence. The evidence belongs to the existing append-only
primary-attributes transaction, so no new `QueryKind` ordinal is required.

## Kitty keyboard detection

`Responses.TryCsi` recognizes a bounded `CSI ? flags u` reply as
`ResponseKind.Keyboard`, accepting only the defined five-bit range.
`QueryTracker` tracks the uncorrelated `QueryKind.Keyboard` family. Detection
sends the keyboard status query followed by DA1, as required by the
[Kitty keyboard protocol](kitty-keyboard.md#implemented-api-and-grammar). A
keyboard reply before DA matches both queries and proves support; DA arriving
while keyboard status is still pending closes that query as unsupported. A later
status reply is therefore late evidence and cannot silently enable the mode.

## Runtime startup batch

The runtime emits one description-first bounded batch. DA1 is always admitted.
When Kitty status is still unknown and the limit has at least two slots, its
status query appears immediately before DA1. DA2 follows DA1 when another slot
is available. Later slots refine only unknown or tentative DECRQM modes 2026,
1004, 2004, 1006, and 1016; definitive database evidence and explicit overrides
suppress their corresponding probes.

Local host geometry has precedence over terminal replies. On Unix the session
samples `TIOCGWINSZ` before constructing the batch; the portable console path
similarly samples its current cell dimensions. The runtime emits `CSI 14 t` only
when text-area pixels are missing, `CSI 16 t` only when complete derived cell
metrics are missing, and `CSI 18 t` only when text-area cells are missing.
Remaining slots query palette index zero and the OSC 10/11 default foreground
and background for diagnostics and caller-directed theme adaptation. These color
replies never rewrite `Capabilities.ColorDepth` or semantic theme colors.
Because OSC 4 correlation is its requested index, only an index-zero reply may
complete the startup palette transaction. A valid reply for another index is
still delivered through the runtime router but is classified as unsolicited and
does not consume, extend, or otherwise alter the pending transaction.

For a non-Kitty xterm hint, remaining capacity then appends XTGETTCAP `RGB` when
color evidence needs refinement, followed by DECRQSS `>4m`. A matched status
reply proves xterm enhanced-key support. Validated positive `RGB` data may
replace default or environment-only semantic color depth with query evidence;
database, prior query, and override evidence win. An explicit
`Settings.ColorDepth` suppresses the XTGETTCAP registration and bytes entirely,
so the slot remains available to the bounded batch.

One absolute exclusive deadline is captured before the first registration and
shared by every emitted query. Out-of-order replies observed strictly before it
match their typed family or private-mode number. At or after it, the first timer
or read observation atomically expires the whole batch and rejects the response
as late. Missing replies leave query evidence absent; explicit false evidence
requires a validated reply. The runtime publishes through the existing default,
environment, query, and override precedence without mutating an earlier profile.
Queried cell metrics, or a validated window-pixel/window-cell pair, may update
inference only for pointer bytes decoded afterward. Local geometry remains
authoritative, and a late query can never reinterpret a pointer event already
delivered.

The public `ResponseKind` and `QueryKind` numeric values are explicit and
append-only. Existing family ordinals remain stable when a later protocol family
is added.

## Sources

- [XTerm Control Sequences, Patch #410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines current DA, DSR, window, color, mouse-pixel, and private-mode query
  forms used by the xterm-compatible profile.
- [Kitty keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/)
  defines the status query ordering used for keyboard enhancement detection.
- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  provides the standard control-function grammar beneath those extensions.

Sources accessed 2026-07-20.

## Expected behavior

| Layer       | Required evidence                                                                         |
| ----------- | ----------------------------------------------------------------------------------------- |
| Encoder     | Exact atomic startup batch and each individual query spelling.                            |
| Correlation | Matched, duplicate, late, unknown, malformed, and contradictory replies.                  |
| Integration | Publication precedes optional modes and first frame while ordinary input remains ordered. |
