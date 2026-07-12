# tmux passthrough

## tmux contract

Primary source:
[tmux manual](https://man7.org/linux/man-pages/man1/tmux.1.html), accessed
2026-07-11. With `allow-passthrough`, a pane can wrap data for the outer
terminal as `DCS tmux ; escaped-data ST`. Embedded ESC bytes are doubled for the
tmux layer.

`TERM` and `TMUX` indicate a multiplexer context, not outer-terminal features.
Capabilities use conservative filtering unless explicit queries survive or the
caller overrides the outer profile. Correlated replies must return to the
requesting pane.

## Current implementation

[`Tmux.WritePassthrough`](../../src/SharpVision.Terminal/Protocols/Tmux.cs)
provides the typed output boundary for one already-validated terminal sequence.
It writes `DCS tmux ;`, doubles every embedded ESC byte, and terminates the
outer envelope with ST in one synchronous `IBufferWriter<byte>` advance. The
matching `TryUnwrap` operation accepts only parser-delivered `tmux;` payloads
with valid ESC pairs and rejects malformed input before destination mutation.
The caller keeps approval of what may be tunneled; controls never invoke this
raw protocol layer. Capability-policy integration and nested-depth management
remain outside the implemented boundary, so callers retain the documented
conservative fallback.

## First milestone contract

Provide typed passthrough wrapping for approved query/clipboard sequences,
bounded unwrapping of replies, correct ESC doubling, nested-depth limits, and
explicit policy when passthrough is disabled. Do not tunnel arbitrary payloads
from controls.

## Tests

Test single/nested wrapping, split ST, ESC doubling, disabled/hidden pane
fallback, size limits, reply correlation, misleading `TERM`, and a real tmux
pseudoterminal smoke path where available.
