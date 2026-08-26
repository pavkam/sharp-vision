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

Tabulation control (HTS/TBC/CHT/CBT) has no typed commands by design: the frame
encoder addresses every cell absolutely through the description's `cup` program,
so hardware tab stops play no role in full-screen output.

> [!IMPORTANT]
>
> **Implementation gap:** `Csi.SetScrollRegion` and `Csi.ResetScrollRegion` can
> emit `CSI Pt ; Pb r` and its reset form, but nothing in the renderer calls
> them yet. Scroll-shaped damage is still repainted through absolute cursor
> addressing instead of a scroll region, so a one-line scroll still costs a
> repaint of every moved row until that renderer-side optimization is built.

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

Sources accessed 2026-07-20.

## Expected behavior

| Layer   | Required evidence                                                                             |
| ------- | --------------------------------------------------------------------------------------------- |
| Encoder | Exact private/intermediate/parameter/final bytes and numeric bounds.                          |
| Parser  | Every split, empty/subparameter forms, malformed bytes, overflow, cancellation, and recovery. |
| Router  | Typed known sequences and observable unknown sequences preserve order and offsets.            |
