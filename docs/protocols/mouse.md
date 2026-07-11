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

## Tests

Test every button/modifier/action, cell and pixel boundaries, malformed values,
split sequences, mode combinations, pointer capture, wheel propagation, resize
conversion, and cleanup. End-to-end tests route decoded pointer events through
hit testing to final control output.
