# Operating System Commands

## OSC contract

OSC uses `ESC ]`, a numeric selector, semicolon-delimited content, and a string
terminator. SharpVision emits `ST` (`ESC \`) by default. A compatibility option
may accept BEL termination, but embedded BEL is never part of a payload.

Primary modern behavior is cross-checked against
[xterm control sequences](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
accessed 2026-07-11.

Payloads are bounded before allocation. Text is UTF-8. Control characters,
invalid Base64, invalid color syntax, and unterminated payloads generate
diagnostics and deterministic recovery. Diagnostics redact clipboard data and
sensitive query responses.

## First milestone contract

Typed support covers selectors 0/2 for titles, 4/10/11 for palette/default color
queries where capabilities allow, 8 for hyperlinks, 52 for clipboard text, and
5522 through the dedicated
[Kitty clipboard contract](kitty-clipboard.md#kitty-clipboard-contract).

## Security and tests

Hyperlink targets and terminal replies are untrusted data. APIs do not execute
or automatically open them. Tests cover ST/BEL input, split terminators, payload
bounds, invalid UTF-8/Base64, redaction, and recovery into following
text/control events.
