# Device attributes and capability queries

## Device attribute contract

DA1 uses `CSI c` or `CSI 0 c`; DA2 uses `CSI > c`. DEC private mode reports use
DECRQM/DECRPM as specified in the
[DEC mode contract](dec-private-modes.md#dec-private-mode-contract). xterm and
Kitty add color, cell-size, keyboard, graphics, and clipboard queries.

Queries are typed transactions with correlation where the protocol supplies an
identifier. Startup applies environment/multiplexer hints first, sends only safe
bounded queries, and completes after all replies or a configured timeout. Late
and unsolicited replies remain observable without mutating an immutable
published `Capabilities` instance.

Terminal replies are untrusted input. Numeric, textual, Base64, and color fields
are bounded and validated before use. Replies cannot enable behavior outside the
query's declared feature.

## First milestone contract

Support DA1/DA2, relevant DECRQM modes, cell/pixel metrics, and the Kitty
keyboard/clipboard/graphics probes needed by implemented features. Callers may
override every detected value for SSH, tmux, GNU screen, CI, and known lies.

## Tests

Cover no response, partial/late/duplicate/conflicting responses, spoofed values,
timeouts with a fake clock, multiplexer hints, explicit overrides, immutability,
and conservative unknown-terminal defaults.
