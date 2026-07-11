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

## Phase 2 implementation

`Csi`, `Sgr`, `Osc`, and `Modes` provide the exact-byte output subset documented
in their linked contracts. `Parser` preserves unknown valid ESC/CSI functions as
borrowed events instead of guessing a terminal identity. Application cursor and
keypad modes, scroll regions, tabulation, and a complete VT compatibility
profile are not yet implemented.

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

## Fallback and tests

Unknown terminals receive the conservative VT baseline. Tests pair each command
with its exact bytes and prove that capability fallback chooses a documented
alternative or omission.
