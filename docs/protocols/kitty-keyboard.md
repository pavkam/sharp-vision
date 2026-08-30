# Kitty keyboard protocol

## Overview

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

Valid Kitty CSI `u` events and cursor/function-key forms such as `CSI B` and
`CSI 1;1:1B` are consumed before terminal-description key lookup, so a database
string cannot shadow enhanced keyboard input. Repeat and release carry a
colon-separated event type and are unambiguously Kitty-specific. Press has event
type 1 by default and may omit that sub-field, so its otherwise ambiguous form
is recognized only after the Session successfully acquires a Kitty
disambiguation lease. When Kitty is not active, the selected profile's exact key
map is authoritative; only the explicit built-in ANSI profile supplements it
with generic VT/xterm grammar.

## Supported features

SharpVision enables the minimum flags needed for unambiguous modified keys and
event types, decodes press/repeat/release plus associated text, and restores the
previous mode on exit. When the protocol is unavailable it falls back to legacy
xterm/VT key decoding.

## Implemented API and grammar

`Kitty.Keyboard.KittyKeyboard` writes the official query (`CSI ? u`), push
(`CSI > flags u`), pop (`CSI < number u`), and direct set/clear forms. The
`KittyKeyboardEnhancement` flags are Disambiguate (1), EventTypes (2),
AlternateKeys (4), AllKeys (8), and AssociatedText (16). Unknown bits are
rejected before output; AssociatedText without AllKeys is rejected because Kitty
defines that combination as undefined — except in the clear mode, where removing
AssociatedText alone is well defined and accepted.
`KittyKeyboardEnhancementMode` exposes replace (1), set (2), and clear (3).

`Input.InputDecoder` recognizes `CSI key:shifted:base;modifiers:event;text…u`
without allocation or retained parser spans. Modifier values are decoded as the
wire value minus one across Shift, Alt, Control, Super, Hyper, Meta, Caps Lock,
and Num Lock. Event 1/2/3 maps to press/repeat/release. The immutable `Stroke`
preserves the main logical code, native number, optional shifted and PC-101
base-layout Runes, modifiers, and action; up to 32 validated associated text
scalars follow as ordered `Text` values.

The decoder also recognizes Kitty's legacy-functional forms, including
`CSI [ABCDEFHPQS]` for an unmodified press and
`CSI 1;modifiers:event [ABCDEFHPQS]` when fields are present, independently of
the built-in ANSI compatibility grammar while the negotiated lease is active.
Press, repeat, and release remain distinct typed actions across arbitrary
transport fragmentation.

Escape, Enter, Tab, Backspace, lock keys, Print Screen, Pause, Menu, F13-F35,
the keypad block, the media transport and volume keys, and the modifier-as-key
block (bare presses of Left/Right Shift, Control, Alt, Super, Hyper, Meta, and
the ISO Level3/Level5 Shift keys) have named logical codes. The keypad
Begin/center key maps to the same `Code.Begin` used for the legacy
keypad-5-with-NumLock-off key. Other valid PUA functional values remain
`Code.Unknown` with their native number.

A bare modifier press already sets the matching `Modifiers` flag on its own
`Stroke` - unlike every other named key, whose code and modifiers are
independent - so `KeyGesture.Matches` excludes a Left/Right Shift, Control, Alt,
Super, Hyper, or Meta code's own flag from its modifier comparison. The ISO
Level3/Level5 Shift codes have no corresponding `Modifiers` bit, so no exclusion
is needed for them.

Impossible scalars, modifier values outside 1-256, event values outside 1-3,
extra groups, C0, DEL, and C1 control codepoints (below 0x20 and 0x7F-0x9F) in
associated text, and excessive text fields report one redacted diagnostic and
recover to the next input. Space and all other valid non-control scalars remain
accepted. Legacy decoding remains active for terminals where
[`QueryTracker`](device-attributes.md#kitty-keyboard-detection) does not prove
support.

## Input coverage

Supported decoding includes the official full-field examples plus every
modifier/event kind, Enter/Tab/Backspace/Escape, known and unknown functional
keys, pure Unicode text, alternate keys, malformed fields, all split points,
exact negotiation bytes, query ordering, and legacy coexistence. Runtime
lifecycle tests own nesting and cleanup.

## Sources

- [Kitty comprehensive keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/)
  defines enhancement flags, CSI-u fields, functional keys, events, and text.

Source accessed 2026-08-30.

## Expected behavior

| Layer       | Required evidence                                                                        |
| ----------- | ---------------------------------------------------------------------------------------- |
| Negotiation | Exact query/push/pop bytes, supported flags, nesting, timeout, and cleanup.              |
| Decoder     | Official examples, all fields/events/splits, unknown keys, malformed recovery, and text. |
| Integration | Enhanced and legacy input coexist without terminal-description key shadowing.            |
