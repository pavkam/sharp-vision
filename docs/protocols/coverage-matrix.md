# Terminal protocol coverage matrix

## Coverage

The state describes current repository evidence, not planned behavior.

| Protocol                                                               | Current state                                                                                                                      | First milestone contract                                                |
| ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| [ECMA-48](ecma-48.md#first-milestone-contract)                         | Typed and implemented: bounded C0/C1, ESC, CSI, OSC, DCS, APC, PM, SOS, and ST framing.                                            | C0/C1, ESC, CSI, OSC, DCS, APC, PM, SOS, and ST framing.                |
| [ANSI and VT](ansi-vt.md#first-milestone-contract)                     | Typed and implemented: renderer output plus legacy VT/xterm key decoding; the wider compatibility profile remains phased.          | VT100/220-compatible application behavior used by the renderer.         |
| [CSI](csi.md#first-milestone-contract)                                 | Typed and implemented: bounded parameters plus the documented cursor, edit, report, and mode subset.                               | Cursor, erase, insert/delete, modes, reports, and limits.               |
| [OSC](osc.md#first-milestone-contract)                                 | Typed and implemented: titles, hyperlinks, color queries, OSC 52, and bounded raw framing.                                         | Titles, hyperlinks, colors, OSC 52, bounded payloads.                   |
| [DCS and strings](dcs-strings.md#first-milestone-contract)             | Decoded and observable: bounded DCS, SOS, OSC, PM, and APC are owned and routed with cancellation and recovery.                    | Bounded framing, recovery, and diagnostic observation.                  |
| [DEC modes](dec-private-modes.md#first-milestone-contract)             | Typed and implemented: cursor, screen, paste, focus, mouse, synchronized-output, clipboard, and reverse lifecycle cleanup.         | Application, cursor, screen, paste, focus, mouse, and restore modes.    |
| [xterm](xterm.md#first-milestone-contract)                             | Decoded and observable: xterm-compatible framing is present; the complete compatibility profile is deferred.                       | Modern xterm control baseline required by the UI.                       |
| [SGR](sgr.md#first-milestone-contract)                                 | Typed and implemented: shipped attributes, group resets, indexed color, RGB color, and defaults.                                   | Attributes, indexed/RGB colors, underline variants, resets.             |
| [Mouse](mouse.md#first-milestone-contract)                             | Typed and implemented: X10/UTF-8/urxvt/SGR input, cell/pixel geometry, mode leases, and capability-gated cleanup.                  | X10, VT200, SGR cell, SGR pixel, wheel, motion, and modifiers.          |
| [Paste and focus](paste-focus.md#first-milestone-contract)             | Typed and implemented: bounded owned paste, focus values, mode leases, truncation, overflow, and recovery.                         | Bracketed paste and focus in/out events.                                |
| [Synchronized output](synchronized-output.md#first-milestone-contract) | Typed and implemented: non-empty bounded frame wrapping, finite recovery cleanup, and full invalidation on failure.                | DEC private mode 2026 with guaranteed cleanup.                          |
| [Device attributes](device-attributes.md#first-milestone-contract)     | Typed and implemented: startup actively negotiates DA1, Kitty status, and selected DECRQM modes with one bounded deadline.         | DA1/DA2, DECRQM/DECRPM, bounded timeouts, overrides.                    |
| [Kitty keyboard](kitty-keyboard.md#first-milestone-contract)           | Typed and implemented: exact negotiation, ordered detection, alternate keys, text, modifiers, and press/repeat/release.            | Progressive enhancement and typed press/repeat/release events.          |
| [Kitty clipboard](kitty-clipboard.md#first-milestone-contract)         | Typed and implemented: OSC 52 and bounded OSC 5522 packet, writer, and transaction behavior.                                       | OSC 52 plus OSC 5522 MIME, permission, paste, and correlation behavior. |
| [Kitty graphics](kitty-graphics.md#first-milestone-contract)           | Unsupported with a specific reason: only capability evidence exists; the bounded extension API is not built.                       | Sourced grammar, detection hook, and bounded raw extension only.        |
| [iTerm2](iterm2.md#first-milestone-contract)                           | Unsupported with a specific reason: only capability evidence exists; the proprietary OSC boundary is not built.                    | Detection and documented proprietary OSC boundary.                      |
| [Sixel](sixel.md#first-milestone-contract)                             | Unsupported with a specific reason: only capability evidence exists; the bounded DCS extension is not built.                       | Detection and bounded DCS extension boundary; no rasterizer.            |
| [tmux](tmux.md#first-milestone-contract)                               | Extension API with safe fallback: typed DCS passthrough framing and ESC doubling are available; reply policy remains conservative. | Safe DCS passthrough and capability override behavior.                  |
| [GNU screen](gnu-screen.md#first-milestone-contract)                   | Extension API with safe fallback: typed DCS passthrough framing is available; reply policy remains conservative.                   | Conservative feature filtering and DCS passthrough behavior.            |

The matrix may use only: typed and implemented; decoded and observable;
extension API with safe fallback; or unsupported with a specific reason. Raster
graphics and multiplexer passthrough remain explicit unsupported boundaries in
[Kitty graphics](kitty-graphics.md#first-milestone-contract),
[Sixel](sixel.md#first-milestone-contract),
[iTerm2](iterm2.md#first-milestone-contract),
[tmux](tmux.md#first-milestone-contract), and
[GNU screen](gnu-screen.md#first-milestone-contract); generic parser framing is
not misreported as semantic support.

## Discovery and output facade

The `TerminalProtocol`/`Capabilities.Support`/`Features` discovery facade and
the `ITerminalServices` output surface, both described in the
[protocol index](index.md#discovery-and-output-facade), are reporting and
consumption layers over this matrix, not new support claims.
`Capabilities.Support(TerminalProtocol.KittyGraphics)`,
`Support(TerminalProtocol.Sixel)`, and `Support(TerminalProtocol.ItermImages)`
report the same real `Unsupported` state as this table, and `ITerminalServices`
exposes no member for graphics, sixel, or iTerm2 images — only the bell, window
title, and clipboard, matching their rows above.
