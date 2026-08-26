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

Valid Kitty CSI `u` events are consumed before terminal-description key lookup,
so a database string cannot shadow enhanced keyboard input. When Kitty is not
active, the selected profile's exact key map is authoritative; only the explicit
built-in ANSI profile supplements it with generic VT/xterm grammar.

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
defines that combination as undefined. `KittyKeyboardEnhancementMode` exposes
replace (1), set (2), and clear (3).

`Input.InputDecoder` recognizes `CSI key:shifted:base;modifiers:event;text…u`
without allocation or retained parser spans. Modifier values are decoded as the
wire value minus one across Shift, Alt, Control, Super, Hyper, Meta, Caps Lock,
and Num Lock. Event 1/2/3 maps to press/repeat/release. The immutable `Stroke`
preserves the main logical code, native number, optional shifted and PC-101
base-layout Runes, modifiers, and action; up to 32 validated associated text
scalars follow as ordered `Text` values.

Escape, Enter, Tab, Backspace, lock keys, Print Screen, Pause, Menu, and F13-F35
have named logical codes. Other valid PUA functional values remain
`Code.Unknown` with their native number.

> [!IMPORTANT]
>
> **Implementation gap:** the keypad, media, and modifier-key blocks of the
> functional range have no named logical codes. Keys such as keypad Enter, the
> keypad arrows, and the media transport keys decode to `Code.Unknown` with only
> their native number preserved, so applications cannot bind them as typed
> codes.

Impossible scalars, modifier values outside 1-256, event values outside 1-3,
extra groups, DEL and C1 control codepoints (0x7F-0x9F) in associated text, and
excessive text fields report one redacted diagnostic and recover to the next
input. Other control codepoints — including Enter (13) and Tab (9) reported as a
key's own associated text — pass through as text scalars unchanged. Legacy
decoding remains active for terminals where
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

Source accessed 2026-07-28.

## Expected behavior

| Layer       | Required evidence                                                                        |
| ----------- | ---------------------------------------------------------------------------------------- |
| Negotiation | Exact query/push/pop bytes, supported flags, nesting, timeout, and cleanup.              |
| Decoder     | Official examples, all fields/events/splits, unknown keys, malformed recovery, and text. |
| Integration | Enhanced and legacy input coexist without terminal-description key shadowing.            |
