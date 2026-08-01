# ANSI and DEC VT compatibility

## Overview

Primary sources are [ECMA-48](ecma-48.md#overview), the
[VT100 programmer information](https://vt100.net/docs/vt100-ug/chapter3.html),
and the
[VT220 programmer reference](https://vt100.net/docs/vt220-rm/chapter4.html),
accessed 2026-07-11. The Windows built-in profile additionally follows the
[Microsoft Console Virtual Terminal Sequences](https://learn.microsoft.com/windows/console/console-virtual-terminal-sequences)
and
[`SetConsoleMode`](https://learn.microsoft.com/windows/console/setconsolemode)
contracts, accessed 2026-07-19.

“ANSI” is not a terminal identity. SharpVision models ECMA control functions,
DEC VT behavior, and named extensions separately. `TERM`, device attributes, and
user overrides select a conservative `Capabilities` profile; they never silently
enable every sequence associated with an emulator name.

The profile-driven application path resolves and validates a description before
application construction. The session uses its exact alternate-screen,
cursor-visibility, and application-key mode pairs. The renderer uses exact
description cursor addressing, erase, rendition, color/default, reset, and
optional cursor-shape programs; description-key decoding is profile-driven. VT52
mode is documented and decoded diagnostically but is not an output target.

## Supported features

Typed output covers the VT100/VT220-compatible subset required by screen
rendering and input. Conflicting DEC, ANSI, and xterm interpretations are named
in the corresponding typed command rather than hidden behind a generic code.

## Implemented output

`Csi`, `Sgr`, `Osc`, and `Modes` provide the exact-byte output subset documented
in their linked contracts. `Parser` preserves unknown valid ESC/CSI functions as
borrowed events instead of guessing a terminal identity. The built-in Windows VT
description owns the exact compiled subset described below. Runtime preflight
selects its profile, and the session consumes the matched lifecycle subset.

## Windows VT built-in description

The Windows console host records VT evidence only after it has successfully
enabled `ENABLE_VIRTUAL_TERMINAL_PROCESSING` for output and
`ENABLE_VIRTUAL_TERMINAL_INPUT` for input. `DescriptionLoader` selects the
built-in `windows-vt` profile only when that immutable connection fact is
present; an operating-system name alone is not evidence.

The profile compiles exact programs for BEL, cursor positioning and movement,
display and line erasure, reset, Microsoft-documented bold, underline, reverse,
basic/indexed/RGB color grammar and color defaults, alternate-screen entry and
exit, cursor visibility, and the matched `smkx`/`rmkx` pair. The pair emits
DECCKM plus DECKPAM on entry and restores normal cursor plus numeric keypad mode
on exit. The fixed key map owns the documented normal/application cursor keys,
Home, End, Insert, Delete, Page Up/Down, F1-F12, and the existing decoder's
control and BackTab spellings.

The generic VT/xterm key grammar is not a universal decoder fallback. It belongs
only to `TerminalProfile.CreateAnsi`, while the Windows VT description owns its
finite fixed map and ncurses profiles own their database strings. Exact
described signatures override generic ANSI key meanings, but registered replies,
paste framing, mouse/focus reports, and Kitty keyboard events retain precedence.

The fixed map requires application mode only because it contains SS3 cursor,
Home, and End bindings (`ESC O A`–`D`, `H`, and `F`). SS3 F1–F4 spellings
(`ESC O P`–`S`) do not by themselves require `smkx`. When application bindings
are present, the session emits the complete `smkx`/`rmkx` pair and restores it
after cursor visibility and alternate screen in exact reverse acquisition order.

Microsoft documents that classic Windows Console accepts indexed and RGB SGR but
projects extended values to its configurable 16-color table. Consequently the
built-in profile records `colors=16` and `ColorDepth.Basic16`; the retained
indexed/RGB programs describe accepted grammar, not guaranteed color fidelity.
It records automatic margins because the host explicitly enables
`ENABLE_WRAP_AT_EOL_OUTPUT` together with delayed newline auto-return. It does
not claim back-color erase because the Microsoft contract does not guarantee
terminfo `bce` semantics.

Dim, italic, blink, conceal, strike, styled underline, underline color,
overline, synchronized output, focus, paste, mouse, Kitty protocols, OSC 52,
graphics, sixel, and iTerm images remain absent programs or unknown semantic
features. Environment or later bounded query evidence may refine semantic
features; it cannot rewrite these built-in programs.

The built-in profile uses the same accepted-snapshot accounting units as the
ncurses provider: UTF-8 bytes for the retained description name; each present
Boolean name plus one value byte; each present numeric name plus four value
bytes; each retained program identifier plus its raw source bytes; and each
retained key sequence's raw bytes. Derived enums, compiler operations, and
literal pools are not charged a second time, matching ncurses' source-snapshot
rule. The fixed `windows-vt` profile is exactly 631 bytes by that rule. A limit
below 631 returns `ProviderFailed` with a `DescriptionLimit` diagnostic before
constructing `Programs` or `KeyMap`; 631 is accepted.

## Legacy input implementation

The terminal input decoder maps the VT/xterm keyboard subset required by the UI
without treating `TERM` as a protocol identity. CSI A/B/C/D and H/F map cursor
and Home/End keys; CSI tilde parameters map Insert/Delete, Page Up/Down,
Home/End, and F1-F12; CSI Z maps Shift-Tab; CSI P/Q/R/S and SS3 A-D, H/F, and
P-S map their functional equivalents. A second CSI parameter uses the xterm
modifier convention of encoded value minus one.

Plain UTF-8 is decoded as Unicode scalar values. A lone ESC is resolved by the
documented timeout in the
[input routing contract](../concepts/input-routing.md#terminal-input-values);
ESC plus printable text represents Alt-modified text. Unknown valid tilde keys
remain `Code.Unknown` with their numeric parameter, while malformed parameter
grammars report a redacted diagnostic and recover at the next sequence.

## Selection, fallback, and tests

An explicit `TerminalProfile` wins before any provider call. Unix requests use
the ncurses provider; Windows requests use the built-in provider only after VT
mode is established. A description request rejects Windows-VT evidence paired
with a non-Windows platform before retaining any request state.
Missing-or-generic, failed, accepted unsuitable, and Windows non-VT results
remain typed results and never silently become ANSI. `AllowAnsiFallback`
defaults to false and permits the built-in ANSI helper only for an unavailable
Unix provider. It does not replace the ambiguous missing-or-generic result
because that result may represent an accepted generic entry, and it never hides
provider failure.

Tests pair each built-in command with its exact compiled source bytes, compare
the fixed key map byte-for-byte, and prove provider-selection precedence and
fallback exclusions. Existing typed command tests repeat representative input at
every split. The frame encoder is independently checked against a semantic
virtual terminal in the [rendering oracle](../testing/rendering.md#overview).

## Sources

- [ECMA-48, fifth edition](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
- [VT100 programmer information](https://vt100.net/docs/vt100-ug/chapter3.html)
- [VT220 programmer reference](https://vt100.net/docs/vt220-rm/chapter4.html)
- [Microsoft Console Virtual Terminal Sequences](https://learn.microsoft.com/windows/console/console-virtual-terminal-sequences)

Sources accessed 2026-07-28.

## Expected behavior

| Layer       | Required evidence                                                                  |
| ----------- | ---------------------------------------------------------------------------------- |
| Description | Exact built-in programs, keys, suitability, and provider/fallback precedence.      |
| Input       | Representative legacy sequences at every split and deterministic unknown recovery. |
| Output      | Exact bytes plus semantic virtual-terminal equivalence across capability tiers.    |
