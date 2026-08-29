# Control Sequence Introducer

## Overview

CSI uses `ESC [` followed by parameter bytes, optional intermediate bytes, and
one final byte. Private prefixes such as `?`, `>`, and `<` are part of a typed
grammar, not decorations to strip.

Parameters are decimal and culture-independent. Empty parameters retain their
protocol-defined default; an absent list is not always equivalent to a literal
zero. The parser bounds parameter count and numeric accumulation before integer
overflow. Unsupported private forms become diagnostic events.

## Supported features

Typed commands cover cursor movement/position, erase, insert/delete, scroll
up/down, scroll-region set/reset, mode set/reset/query (including the mouse
tracking and coordinate modes), SGR, device attributes, and the terminal
size/cell reports required by the renderer and capability detector.

The encoder omits an optional default parameter only where the protocol defines
the shorter form as byte-equivalent. It never accepts negative parameters or
writes locale-formatted digits.

## Typed API and behavior

`Parameters` enumerates semicolon fields and colon subparameters without
allocating or flattening their meaning. It exposes an initial private marker and
reports default, value, invalid, overflow, count-limit, and end states.

`Csi` encodes relative movement, absolute position, display/line erase,
character/line insert and delete, scroll up/down, scroll-region set and reset
(DECSTBM), ANSI cursor save and restore, DA1/DA2, cursor-position DSR, DECRQM,
and xterm window-operation queries 14, 16, and 18 for text-area pixels,
character-cell pixels, and text-area cells. `XtermResponses.TryMetricsCsi`
accepts only matching 4/6/8 reports with positive dimensions no greater
than 65535. `ProtocolModes` encodes cursor visibility, alternate screen 1049,
focus 1004, bracketed paste 2004, synchronized output 2026, Kitty clipboard mode
5522, and the mouse tracking (9/1000/1002/1003) and coordinate
(1005/1006/1015/1016) modes.

For an ANSI-compatible profile, the frame encoder detects contiguous rows that
have moved vertically between committed and target frames. When the saved cell
bytes exceed the finite cost of the operation, it emits DECSTBM for the smallest
affected region, `CSI Ps S` or `CSI Ps T` for the shift, resets DECSTBM in the
same batch, and repaints only newly exposed or otherwise changed cells. The
comparison includes Kitty Unicode-placeholder cells, so an assigned virtual
image scrolls with ordinary text instead of forcing cursor-anchored replacement.
Profiles that are not ANSI-compatible, frames with unprojected graphics, and
shifts that do not reduce output retain ordinary absolute repaint.

DECSTBM is terminal-global state. If a transport write or flush tears after the
region was set, the renderer marks both the frame and the scroll region
uncertain. The forced full repair starts with `CSI r` before style reset and
clear, preventing a stranded margin from constraining subsequent output.

Tabulation control (HTS/TBC/CHT/CBT) has no typed commands by design: the frame
encoder addresses every cell absolutely through the description's `cup` program,
so hardware tab stops play no role in full-screen output.

## Recovery and tests

Malformed parameters, excess intermediates, overflow, CAN/SUB, split finals, and
back-to-back CSI sequences have exhaustive recovery tests. Exact-byte tests
cover absent, zero, default, maximum, and rejected values.

## Sources

- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines CSI byte classes, parameter and intermediate ranges, final bytes, and
  standard control functions.
- [XTerm Control Sequences, patch level 410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines the xterm and DEC-compatible private forms used by the supported
  profile.
- [VT510 Programmer Information: DECSTBM](https://vt100.net/docs/vt510-rm/DECSTBM.html)
  defines top and bottom margins, their scrolling effect, and cursor relocation.

Sources accessed 2026-08-29.

## Expected behavior

| Layer   | Required evidence                                                                             |
| ------- | --------------------------------------------------------------------------------------------- |
| Encoder | Exact private/intermediate/parameter/final bytes and numeric bounds.                          |
| Parser  | Every split, empty/subparameter forms, malformed bytes, overflow, cancellation, and recovery. |
| Router  | Typed known sequences and observable unknown sequences preserve order and offsets.            |
