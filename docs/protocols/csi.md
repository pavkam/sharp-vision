# Control Sequence Introducer

## CSI contract

CSI uses `ESC [` followed by parameter bytes, optional intermediate bytes, and
one final byte. Private prefixes such as `?`, `>`, and `<` are part of a typed
grammar, not decorations to strip.

Parameters are decimal and culture-independent. Empty parameters retain their
protocol-defined default; an absent list is not always equivalent to a literal
zero. The parser bounds parameter count and numeric accumulation before integer
overflow. Unsupported private forms become diagnostic events.

## First milestone contract

Typed commands cover cursor movement/position, erase, insert/delete, scroll
regions, tabulation, mode set/reset/query, SGR, device attributes, and terminal
size/cell reports required by the renderer and capability detector.

The encoder omits optional defaults only when byte equivalence is specified. It
never accepts negative parameters or writes locale-formatted digits.

## Phase 2 implementation

`Parameters` enumerates semicolon fields and colon subparameters without
allocating or flattening their meaning. It exposes an initial private marker and
reports default, value, invalid, overflow, count-limit, and end states.

`Csi` currently encodes relative movement, absolute position, display/line
erase, character/line insert and delete, scroll up/down, ANSI cursor save and
restore, DA1/DA2, cursor-position DSR, DECRQM, and xterm window-operation
queries 14, 16, and 18 for text-area pixels, character-cell pixels, and
text-area cells. `Responses.TryMetricsCsi` accepts only matching 4/6/8 reports
with positive dimensions no greater than 65535. `Modes` encodes cursor
visibility, alternate screen 1049, focus 1004, bracketed paste 2004,
synchronized output 2026, and Kitty clipboard mode 5522. Tabulation, scroll
regions, terminal-size reports, mouse modes, and lifecycle leases remain in
later roadmap phases and are not claimed as implemented here.

## Recovery and tests

Malformed parameters, excess intermediates, overflow, CAN/SUB, split finals, and
back-to-back CSI sequences have exhaustive recovery tests. Exact-byte tests
cover absent, zero, default, maximum, and rejected values.

## Sources

- [ECMA-48, fifth edition, June 1991](https://ecma-international.org/publications-and-standards/standards/ecma-48/)
  defines CSI byte classes, parameter and intermediate ranges, final bytes, and
  standard control functions.
- [XTerm Control Sequences, Patch #410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines the xterm and DEC-compatible private forms used by the supported
  profile.

Sources accessed 2026-07-20.

## Test obligations

| Layer   | Required evidence                                                                             |
| ------- | --------------------------------------------------------------------------------------------- |
| Encoder | Exact private/intermediate/parameter/final bytes and numeric bounds.                          |
| Parser  | Every split, empty/subparameter forms, malformed bytes, overflow, cancellation, and recovery. |
| Router  | Typed known sequences and observable unknown sequences preserve order and offsets.            |
