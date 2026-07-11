# ANSI and DEC VT compatibility

## ANSI and VT contract

Primary sources are [ECMA-48](ecma-48.md#ecma-48-contract), the
[VT100 programmer information](https://vt100.net/docs/vt100-ug/chapter3.html),
and the
[VT220 programmer reference](https://vt100.net/docs/vt220-rm/chapter4.html),
accessed 2026-07-11.

“ANSI” is not a terminal identity. SharpVision models ECMA control functions,
DEC VT behavior, and named extensions separately. `TERM`, device attributes, and
user overrides select a conservative `Capabilities` profile; they never silently
enable every sequence associated with an emulator name.

The application uses VT-compatible cursor addressing, erase, scrolling,
keypad/cursor-key modes, save/restore, and device reports only when the profile
declares them. VT52 mode is documented and decoded diagnostically but is not an
output target.

## First milestone contract

Typed output covers the VT100/VT220-compatible subset required by screen
rendering and input. Conflicting DEC, ANSI, and xterm interpretations are named
in the corresponding typed command rather than hidden behind a generic code.

## Fallback and tests

Unknown terminals receive the conservative VT baseline. Tests pair each command
with its exact bytes and prove that capability fallback chooses a documented
alternative or omission.
