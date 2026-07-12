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

## Phase 2 implementation

`Responses.TryCsi` owns validated DA1, DA2, cursor-position DSR, and DECRPM
values. `Responses.TryOsc` owns 16-bit-component foreground/background color
replies. The [runtime router](runtime-routing.md#runtime-routing-contract)
delivers those typed values without allowing them to fall through as keyboard
input. `QueryTracker` admits at most `Limits.MaxConcurrentQueries`, one active
uncorrelated query per family, and distinct Kitty clipboard IDs. Completed,
cancelled, and timed-out correlations remain in a bounded grace window so
duplicates and late replies cannot mutate a published profile.

Cell/pixel metrics and Kitty graphics-specific probe decoders remain assigned to
later Phase 3 work or their documented extension boundary.

## Phase 3 Kitty detection

`Responses.TryCsi` recognizes a bounded `CSI ? flags u` reply as
`ResponseKind.Keyboard`, accepting only the defined five-bit range.
`QueryTracker` tracks the uncorrelated `QueryKind.Keyboard` family. Detection
sends the keyboard status query followed by DA1, as required by the
[Kitty keyboard protocol](kitty-keyboard.md#implemented-api-and-grammar). A
keyboard reply before DA matches both queries and proves support; DA arriving
while keyboard status is still pending closes that query as unsupported. A later
status reply is therefore late evidence and cannot silently enable the mode.
