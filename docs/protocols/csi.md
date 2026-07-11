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
restore, DA1/DA2, cursor-position DSR, and DECRQM. `Modes` encodes cursor
visibility, alternate screen 1049, focus 1004, bracketed paste 2004,
synchronized output 2026, and Kitty clipboard mode 5522. Tabulation, scroll
regions, terminal-size reports, mouse modes, and lifecycle leases remain in
later roadmap phases and are not claimed as implemented here.

## Recovery and tests

Malformed parameters, excess intermediates, overflow, CAN/SUB, split finals, and
back-to-back CSI sequences have exhaustive recovery tests. Exact-byte tests
cover absent, zero, default, maximum, and rejected values.
