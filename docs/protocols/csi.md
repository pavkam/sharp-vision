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

## Recovery and tests

Malformed parameters, excess intermediates, overflow, CAN/SUB, split finals, and
back-to-back CSI sequences have exhaustive recovery tests. Exact-byte tests
cover absent, zero, default, maximum, and rejected values.
