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

## Discovery and output facade

`SharpVision.Terminal.Capabilities.TerminalProtocol` names each optional feature
a `Capabilities` profile can report: `SynchronizedOutput`, `FocusReporting`,
`BracketedPaste`, `PixelMouse`, `CellMouse`, `KittyKeyboard`, `Osc52`,
`KittyClipboard`, `KittyGraphics`, `Sixel`, `ItermImages`, `StyledUnderlines`,
`UnderlineColor`, and `Overline`. `Capabilities.Support(TerminalProtocol)` maps
one named protocol to its `Feature` evidence, and `Capabilities.Features`
returns every protocol paired with its `Feature` as an
`IReadOnlyList<ProtocolSupport>`, replacing an earlier anonymous feature list.
Both members report each protocol's real `Support` state. `Support(...)` for
Kitty graphics, sixel, and iTerm2 images never reports `Supported`; it returns
`Unknown` by default, `Tentative` under a vendor-hinted terminal, and
`Unsupported` only under a detected multiplexer — so the discovery facade never
fabricates support for a protocol this table lists as unsupported; the
[coverage matrix](coverage-matrix.md#coverage) remains the only support claim.

`SharpVision.Runtime.ITerminalServices` (`Application.Terminal`) exposes the
implemented **output** protocols behind small interfaces: `IBell.Ring()` emits a
BEL byte, `SetTitle(string)` emits OSC 2, and `IClipboard` writes and requests
OSC 52/Kitty clipboard selections when `IsSupported`
(`Osc52.IsSupported || KittyClipboard.IsSupported`) is `true`, otherwise
`Write`/`Request` are safe no-ops. All three post their encoded bytes through
the
[ordered out-of-band write path](../architecture/runtime-event-loop.md#out-of-band-protocol-writes)
so they never interleave a frame. Kitty graphics, sixel, and iTerm2 images are
not exposed by this facade — the coverage matrix continues to state them as
unsupported, and the runtime never fabricates output support for them either.
Inbound consumption of protocol replies (typed responses, capability changes,
and redacted diagnostics) is unchanged and documented in
[runtime routing](runtime-routing.md#inbound-consumption-surface).
