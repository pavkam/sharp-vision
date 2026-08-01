# Sixel graphics

## Overview

Primary sources:

- [DEC VT330/VT340 Programmer Reference, Chapter 14](https://vt100.net/docs/vt3xx-gp/chapter14.html)
  defines sixel data, repeat, raster attributes, palette selection, graphics
  carriage return/newline, transparent background, and DCS framing;
- [XTerm Control Sequences, Patch #410, 2026-04-19](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  records the current `DCS Pa ; Pb ; Ph q data ST` form and DA1 parameter 4.

Sources accessed 2026-07-20.

SharpVision emits canonical 7-bit framing: `ESC P 0 ; 1 ; 0 q`, sixel data, then
`ESC \`. Parameter 2 is 1, so zero-valued raster pixels leave the existing
background unchanged. Raster attributes are emitted before data as
`"1;1;width;height`. Color definitions use `#Pc;2;R;G;B`, where each component
is from 0 through 100. `!Pn` compresses runs of four or more identical sixels,
`$` returns to the left edge of the current six-pixel band, and `-` advances to
the next band.

## Implemented encoder

The encoder accepts owned sRGB RGBA only. It never decodes PNG. Fully
transparent pixels remain unchanged; every nonzero alpha is treated as opaque.
Each component is mapped without dithering to a fixed 6 by 6 by 6 RGB cube. Only
used colors are published, sorted by cube index and assigned dense stable
palette identifiers. This makes identical input and geometry byte-for-byte
deterministic regardless of color discovery order.

Contain, cover, and stretch use checked nearest-neighbor sampling over the
explicit clipped source rectangle. The source is sampled exactly once into a
bounded indexed raster. Before plane emission, the encoder computes a checked
conservative bound covering framing, every palette definition, every possible
color plane, carriage returns, and band advances. A proposal exceeding the
caller byte policy fails before destination mutation. Six-row band planes are
then populated from the indexed raster; they do not rescan source pixels for
every palette color.

## Renderer backend

Sixel has no retained remote image identity. The backend requires measured
cell-pixel geometry and maps each destination cell rectangle to its exact pixel
boundaries. It does not invent a pixel size when metrics are missing. Each image
is anchored by pane-local CUP, emitted after ordinary cell output, and followed
by restoration of the frame's semantic cursor.

Because sixel pixels are non-retained, placement movement, removal, RGBA-to-PNG
replacement, resize, invalidation, or cell-metric change forces complete cell
reconstruction before every target sixel is repainted. Ordinary cell damage
intersecting an unchanged placement repaints that placement after the cells;
later overlapping sixel or iTerm2 placements are replayed transitively in paint
order. If any upper overlap cannot be encoded, every affected lower image also
remains on ordinary cell fallback rather than covering it. Unrelated cell damage
emits no sixel. A failed transport transaction invalidates the backend and the
next frame reconstructs cells and all encodable placements. Shutdown is
byte-quiet because the protocol contract owns no remote IDs or delete command.

Direct output is supported. An explicit tmux route wraps each complete DCS
independently while CUP remains pane-local. GNU Screen routes are unavailable
because its passthrough cannot represent a nested ST-terminated DCS. Route
failure occurs during preparation, before transport I/O.

## Detection and selection boundary

A validated DA1 reply containing parameter 4 records sixel support with query
origin. A validated DA1 reply without 4 records unsupported query evidence.
Explicit `Settings.Sixel` true or false wins over either result. Environment and
terminal-description hints remain tentative and do not authorize output.

Application selects the sixel backend lazily after negotiated profile and first
resize publication, using authoritative evidence and the live exact metrics. The
public Image control always paints ordinary cell fallback before recording its
semantic placement. Missing metrics, incompatible PNG, conservative occlusion,
or rejected multiplexer routing therefore leaves that fallback visible without
direct DCS leakage.

## Encoding and runtime coverage

Encoder tests pin exact DCS parameters, raster attributes, palette ordering,
transparency, repeat runs, source clipping, six-row bands, graphics carriage
return/newline, and canonical ST. They also prove PNG rejection, destination
failure fidelity, atomic output-limit rejection, checked large bounds, and one
source sample per destination pixel.

DA1 parameter 4 runs through the real streaming protocol router at every split;
negative evidence and both override directions prove precedence. Backend and
real renderer tests cover exact and uneven metrics, missing metrics, movement,
removal, unsupported replacement, intersecting cell damage, resize-style full
repair, authorized tmux routing, Screen rejection, cursor restoration,
allocation-free commit/invalidation, byte-quiet cleanup, partial transport
failure, and full retry reconstruction. Application integration tests
additionally require public Image fallback bytes before the sixel DCS and the
exact uneven pixel grid in raster geometry.

## Sources

- [DEC VT330/VT340 Programmer Reference, Chapter 14](https://vt100.net/docs/vt3xx-gp/chapter14.html)
- [XTerm Control Sequences, Patch #410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)

Sources accessed 2026-07-28.

## Expected behavior

| Layer     | Required evidence                                                                       |
| --------- | --------------------------------------------------------------------------------------- |
| Encoder   | Exact framing, raster attributes, palette, runs, transparency, limits, and determinism. |
| Selection | DA1 parameter 4 or explicit evidence, pixel metrics, media/stretch, and route policy.   |
| Rendering | Cell fallback, cursor, damage repair, failure retry, resize, final bytes, and cleanup.  |
