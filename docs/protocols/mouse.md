# Mouse reporting

## Mouse reporting contract

Primary source:
[xterm mouse tracking](https://www.invisible-island.net/xterm/ctlseqs/ctlseqs.html),
patch 410, accessed 2026-07-11. Tracking modes include X10 9, VT200 1000,
button-event 1002, any-event 1003, UTF-8 encoding 1005, SGR cell encoding 1006,
urxvt encoding 1015, and SGR pixel encoding 1016.

SharpVision prefers SGR 1006 for cell coordinates and 1016 when pixel input is
supported. SGR reports are `CSI < Cb ; Cx ; Cy M` for press/motion and final `m`
for release. Coordinates are one-based on the wire and converted once to
zero-based typed values. Pixel reports retain raw pixel coordinates and derived
cell coordinates plus an `IsCellPositionInferred` flag.

Buttons, wheel directions, modifiers, press/release/move, and leave-window
events are distinct typed values. Numeric parameters and coordinates are bounded
before conversion.

## First milestone contract

Decode X10 and VT200 compatibility, SGR cell/pixel, wheel, motion, extra
buttons, modifiers, and Kitty's pixel-mode leave notification. Encode safe mode
leases and restore previous tracking on shutdown.

`Input.Decoder` now accepts three compatibility families: the three UTF-8 scalar
fields following X10 `CSI M`, urxvt decimal reports, and SGR reports with `<`.
All fragmented fields remain bounded. Button codes preserve primary, middle,
secondary, back, and forward buttons; modifier bits, motion, release, four wheel
directions, and the zero-coordinate leave sentinel remain distinct.

Cell reports subtract the wire's one-based origin exactly once. With
`Input.Options.PixelMouse`, SGR coordinates are retained as zero-based pixels;
validated `Geometry.Metrics` derive optional cells from exact total dimensions
and set `IsCellPositionInferred`. Without metrics, pixels remain available and
cells stay null. Undefined extended buttons, negative/zero ordinary coordinates,
overlong X10 fields, invalid UTF-8, and malformed decimal forms report once and
recover at the next input.

Exact metrics preserve total cell and pixel dimensions. For an in-window pixel
coordinate, each axis maps as `floor(pixel * cellCount / pixelCount)` using a
checked 64-bit intermediate. Uneven grids therefore retain every final column
and row instead of truncating one nominal cell size. Coordinates outside the
known pixel rectangle are not clamped into the terminal.

Cell-protocol reports always expose cell coordinates. Pixel-protocol reports
always expose pixels and expose nullable cells only when exact mapping succeeds.
Pointer leave has neither coordinate. Ordinary hit testing requires cells; an
existing capture may receive pixel-only motion or release for a documented
pixel-aware behavior. Missing metrics never fabricate top-left cell zero.

## Tests

Decoder tests cover every button/modifier/action family, vertical and horizontal
wheel deltas, cell and pixel conversion, maximum coordinates, leave, malformed
values, X10 UTF-8 coordinates, and every split. Mode combinations and cleanup
are proved by the runtime session; Phase 4 routes these values through pointer
capture and hit testing to final control output.

`Modes.Mouse` now owns exact mode 9/1000/1002/1003 tracking and
1005/1006/1015/1016 coordinate commands. `Runtime.Options` selects the pair;
`Runtime.Session` enables cell input only with proven `CellMouse` support and
pixel input only with proven `PixelMouse` support, then restores coordinate and
tracking modes in reverse. Tentative terminal-name hints never activate them.
