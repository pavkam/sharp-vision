# Sixel graphics

## Sixel contract

Primary source: DEC VT330/VT340 graphics programming as summarized by
[xterm's current sixel controls](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
accessed 2026-07-11. Sixel uses `DCS Pa ; Pb ; Ph q data ST`; six vertical
pixels form the basic encoded unit. DA1 value 4 can advertise sixel support.

Parameters control aspect/background/grid behavior. Data contains raster,
repeat, color, and carriage/newline commands. DCS and decoded dimensions are
bounded before storage or allocation.

## First milestone contract

Provide detection, sourced grammar, bounded framing, and a raw extension
boundary. Raster decoding, palette management, image scaling, cursor-placement
quirks, and rendering are unsupported in the first milestone. The UI uses a
text/cell fallback.

## Security and tests

Tests cover DA detection, DCS framing and all split points, maximum dimensions,
repeat overflow, unterminated/oversized data, capability fallback, and recovery
into following terminal input.
