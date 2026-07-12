# Terminal protocol specifications

## Protocol families

The low-level library recognizes the ECMA-48 control architecture and selected
DEC, xterm, Kitty, iTerm2, sixel, tmux, and GNU screen extensions. The
[coverage matrix](coverage-matrix.md#coverage) is the only support claim.

- [ECMA-48](ecma-48.md#ecma-48-contract) defines the control-function model.
- [ANSI and VT](ansi-vt.md#ansi-and-vt-contract) defines compatibility scope.
- [CSI](csi.md#csi-contract) defines parameterized control sequences.
- [OSC](osc.md#osc-contract) defines operating-system command strings.
- [DCS and string commands](dcs-strings.md#dcs-and-string-command-contract)
  defines bounded string parsing.
- [Runtime protocol routing](runtime-routing.md#runtime-routing-contract)
  defines typed dispatch, owned extension values, and runtime fallback.
- [DEC private modes](dec-private-modes.md#dec-private-mode-contract) defines
  application modes and lifecycle restoration.
- [xterm](xterm.md#xterm-contract) defines the modern compatibility baseline.
- [SGR](sgr.md#sgr-contract) defines colors and text attributes.
- [Mouse reporting](mouse.md#mouse-reporting-contract) defines cell and pixel
  input.
- [Paste and focus](paste-focus.md#paste-and-focus-contract) defines input
  boundaries.
- [Synchronized output](synchronized-output.md#synchronized-output-contract)
  defines atomic frame presentation.
- [Device attributes](device-attributes.md#device-attribute-contract) defines
  capability queries.
- [Kitty keyboard](kitty-keyboard.md#kitty-keyboard-contract) defines
  unambiguous key events.
- [Kitty clipboard](kitty-clipboard.md#kitty-clipboard-contract) defines OSC 52
  and OSC 5522 behavior.
- [Kitty graphics](kitty-graphics.md#kitty-graphics-contract) defines the image
  extension boundary.
- [iTerm2](iterm2.md#iterm2-contract) defines proprietary OSC extensions.
- [Sixel](sixel.md#sixel-contract) defines DEC raster graphics boundaries.
- [tmux](tmux.md#tmux-contract) and
  [GNU screen](gnu-screen.md#gnu-screen-contract) define multiplexer wrapping.
