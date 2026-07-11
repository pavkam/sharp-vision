# DEC private modes

## DEC private mode contract

DECSET is `CSI ? Pm h`, DECRST is `CSI ? Pm l`, and DECRQM queries a mode with
`CSI ? Ps $ p`; DECRPM replies with `CSI ? Ps ; Pm $ y`. Mode meanings are
cross-checked against
[xterm control sequences](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
and DEC manuals, accessed 2026-07-11.

Modes are typed identifiers with capability requirements and cleanup policy.
They are not arbitrary integers passed from controls. Enabling a mode records
the restoration action. Nested owners use leases so one component cannot disable
a mode still needed by another.

## First milestone contract

Track cursor-key/application keypad behavior, origin/wrap, cursor visibility,
alternate screen, mouse families, focus 1004, bracketed paste 2004, synchronized
output 2026, and Kitty clipboard paste 5522. Queries are bounded and correlated;
response values 0 and 4 mean unsupported where the defining protocol states that
rule.

## Restoration and tests

Shutdown, cancellation, transport failure, and exceptions attempt reverse-order
restoration. Cleanup failure is diagnostic and never hides the original error.
Tests cover nesting, duplicate enable/disable, partial initialization, missing
responses, contradictory responses, and all failure exits.

## Phase 2 implementation

`Modes` provides exact DECSET/DECRST bytes for modes 25, 1004, 1049, 2004, 2026,
and 5522. `Csi.QueryPrivateMode` emits DECRQM; `Responses.TryCsi` validates
DECRPM and maps states 1/2 to supported while 0/4 remain unsupported.
`QueryTracker` bounds in-flight queries, rejects ambiguous duplicate
uncorrelated requests, correlates Kitty IDs, and distinguishes duplicate from
late replies using an injected `TimeProvider`. Mode ownership and reverse-order
terminal restoration are Phase 3 lifecycle work.
