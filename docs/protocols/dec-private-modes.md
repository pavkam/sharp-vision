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
