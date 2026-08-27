# Mouse reporting

## Overview

Primary source:
[xterm mouse tracking](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
patch 410, accessed 2026-08-27. Tracking modes include X10 9, VT200 1000,
button-event 1002, any-event 1003, UTF-8 encoding 1005, SGR cell encoding 1006,
urxvt encoding 1015, and SGR pixel encoding 1016.

SharpVision prefers SGR 1006 for cell coordinates and 1016 when pixel input is
supported. SGR reports are `CSI < Cb ; Cx ; Cy M` for press/motion and final `m`
for release. Coordinates are one-based on the wire and converted once to
zero-based typed values. Pixel reports retain raw pixel coordinates and derived
cell coordinates plus an `CellPositionInferred` flag.

Buttons, wheel directions, modifiers, press/release/move, and leave-window
events are distinct typed values. Numeric parameters and coordinates are bounded
before conversion.

## Supported features

SharpVision decodes X10 and VT200 compatibility, SGR cell/pixel, wheel, motion,
extra buttons, modifiers, and Kitty's pixel-mode leave notification. It encodes
safe mode leases and restores previous tracking on shutdown.

`Input.InputDecoder` accepts three compatibility families: the three-field X10
report following `CSI M`, urxvt decimal reports, and SGR reports with `<`. All
fragmented fields remain bounded. Button codes preserve primary, middle,
secondary, back, and forward buttons; modifier bits, motion, release, four wheel
directions, and the zero-coordinate leave sentinel remain distinct.

The X10 field reader honors the negotiated `Protocols.MouseCoordinates`,
threaded into `Input.InputOptions.MouseCoordinates` from the same
`Runtime.TerminalOptions.Coordinates` value that selects the write-side DECSET
mode: raw single-byte fields under `MouseCoordinates.Default`, UTF-8 scalar
fields under `MouseCoordinates.Utf8`. Xterm's bit-128 selectors preserve buttons
8 through 11 as `Back`, `Forward`, `Extended10`, and `Extended11`; motion and
SGR release retain the same typed button. Values beyond button 11 remain
malformed because their modifier encoding is ambiguous. The two coordinate
encodings are mutually ambiguous for field bytes at or above `0x80`, so the
decoder cannot infer which is in force from the byte stream alone — the input
and output sides must agree. `0x7F` (DEL) is a legal field byte (coordinate 95)
under both encodings and is fed to a pending X10 report rather than being
treated as a keystroke.

Cell reports subtract the wire's one-based origin exactly once. With
`Input.InputOptions.PixelMouse`, SGR coordinates are retained as zero-based
pixels; validated `Geometry.CellMetrics` derive optional cells and set
`CellPositionInferred` — from exact total dimensions when both grids are known,
or by dividing through the nominal cell size when only a cell-size report
arrived. Without any metrics, pixels remain available and cells stay null.
Undefined extended buttons, negative/zero ordinary coordinates, overlong X10
fields, invalid UTF-8, and malformed decimal forms report once and recover at
the next input.

Exact metrics preserve total cell and pixel dimensions. For an in-window pixel
coordinate, each axis maps as `floor(pixel * cellCount / pixelCount)` using a
checked 64-bit intermediate. Uneven grids therefore retain every final column
and row instead of truncating one nominal cell size. On the exact path,
coordinates outside the known pixel rectangle refuse to map and are not clamped
into the terminal; the nominal path has no window rectangle, so an out-of-window
pixel there maps to an out-of-grid cell instead.

Cell-protocol reports always expose cell coordinates. Pixel-protocol reports
always expose pixels and expose nullable cells only when metrics-based mapping
succeeds. Pointer leave has neither coordinate. Ordinary hit testing requires
cells; an existing capture may receive pixel-only motion or release for a
documented pixel-aware behavior. Missing metrics never fabricate top-left cell
zero.

## Input and lifecycle coverage

Supported decoding covers every button/modifier/action family, vertical and
horizontal wheel deltas, cell and pixel conversion, maximum coordinates, leave,
malformed values, X10 UTF-8 coordinates, and every split. Mode combinations and
cleanup are proved by the runtime session; the UI layer routes these values
through pointer capture and hit testing to final control output.

`ProtocolModes.Mouse` owns exact mode 9/1000/1002/1003 tracking and
1005/1006/1015/1016 coordinate commands. `Runtime.TerminalOptions` selects the
pair; `Runtime.Session` enables cell input only with proven `CellMouse` support
and pixel input only with proven `PixelMouse` support, then restores coordinate
and tracking modes in reverse. Tentative terminal-name hints never activate
them.

## Sources

- [XTerm Control Sequences, patch level 410](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html)
  defines modes 9, 1000, 1002, 1003, 1005, 1006, 1015, and 1016.

Source accessed 2026-07-28.

## Expected behavior

| Layer       | Required evidence                                                                    |
| ----------- | ------------------------------------------------------------------------------------ |
| Decoder     | Exact buttons/modifiers/motion/release, every split, bounds, and malformed recovery. |
| Coordinates | One-based wire conversion, retained pixels, inferred cells, resize, and capture.     |
| Lifecycle   | Capability-gated mode pairs, exact order, partial failure, and reverse restoration.  |
