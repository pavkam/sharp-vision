# Kitty keyboard protocol

## Kitty keyboard contract

Primary source:
[Kitty comprehensive keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/),
accessed 2026-07-11. The protocol extends CSI `u` to report key identity,
modifiers, text, and press/repeat/release events without legacy ambiguity.

SharpVision negotiates progressive enhancement and records the enabled flags.
Decoded events preserve physical/logical key codes, modifiers, event kind, and
associated text as separate values. Text input is not reconstructed from key
names when the terminal supplies text.

Unknown functional key codes remain typed unknown values. Numeric fields,
alternate keys, and text code points are bounded and validated. Legacy key
sequences continue through the same high-level event model when the protocol is
unavailable.

## First milestone contract

Enable the minimum flags needed for unambiguous modified keys and event types,
decode press/repeat/release plus associated text, and restore the previous mode
on exit. Safe fallback is legacy xterm/VT key decoding.

## Tests

Use official examples plus every modifier/event kind, Enter/Tab/Backspace,
functional keys, Unicode text, unknown codes, malformed fields, all split
points, negotiation, nesting, fallback, and cleanup.
