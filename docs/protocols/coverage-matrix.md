# Terminal protocol coverage matrix

## Coverage

The state describes current repository evidence, not planned behavior.

| Protocol                                                               | Current state                                                    | First milestone contract                                                |
| ---------------------------------------------------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------------- |
| [ECMA-48](ecma-48.md#first-milestone-contract)                         | Unsupported: Phase 2 typed parser and encoder do not exist.      | C0/C1, ESC, CSI, OSC, DCS, APC, PM, SOS, and ST framing.                |
| [ANSI and VT](ansi-vt.md#first-milestone-contract)                     | Unsupported: compatibility types do not exist.                   | VT100/220-compatible application behavior used by the renderer.         |
| [CSI](csi.md#first-milestone-contract)                                 | Unsupported: typed CSI encoding/decoding does not exist.         | Cursor, erase, insert/delete, modes, reports, and limits.               |
| [OSC](osc.md#first-milestone-contract)                                 | Unsupported: typed OSC support does not exist.                   | Titles, hyperlinks, colors, OSC 52, bounded payloads.                   |
| [DCS and strings](dcs-strings.md#first-milestone-contract)             | Unsupported: streaming string states do not exist.               | Bounded framing, recovery, and diagnostic observation.                  |
| [DEC modes](dec-private-modes.md#first-milestone-contract)             | Unsupported: lifecycle mode tracking does not exist.             | Application, cursor, screen, paste, focus, mouse, and restore modes.    |
| [xterm](xterm.md#first-milestone-contract)                             | Unsupported: compatibility profile does not exist.               | Modern xterm control baseline required by the UI.                       |
| [SGR](sgr.md#first-milestone-contract)                                 | Unsupported: style encoder does not exist.                       | Attributes, indexed/RGB colors, underline variants, resets.             |
| [Mouse](mouse.md#first-milestone-contract)                             | Unsupported: typed pointer decoder does not exist.               | X10, VT200, SGR cell, SGR pixel, wheel, motion, and modifiers.          |
| [Paste and focus](paste-focus.md#first-milestone-contract)             | Unsupported: input decoder does not exist.                       | Bracketed paste and focus in/out events.                                |
| [Synchronized output](synchronized-output.md#first-milestone-contract) | Unsupported: frame lifecycle does not exist.                     | DEC private mode 2026 with guaranteed cleanup.                          |
| [Device attributes](device-attributes.md#first-milestone-contract)     | Unsupported: query correlation does not exist.                   | DA1/DA2, DECRQM/DECRPM, bounded timeouts, overrides.                    |
| [Kitty keyboard](kitty-keyboard.md#first-milestone-contract)           | Unsupported: keyboard decoder does not exist.                    | Progressive enhancement and typed press/repeat/release events.          |
| [Kitty clipboard](kitty-clipboard.md#first-milestone-contract)         | Unsupported: clipboard transactions do not exist.                | OSC 52 plus OSC 5522 MIME, permission, paste, and correlation behavior. |
| [Kitty graphics](kitty-graphics.md#first-milestone-contract)           | Extension API with safe fallback is planned but not implemented. | Sourced grammar, detection hook, and bounded raw extension only.        |
| [iTerm2](iterm2.md#first-milestone-contract)                           | Extension API with safe fallback is planned but not implemented. | Detection and documented proprietary OSC boundary.                      |
| [Sixel](sixel.md#first-milestone-contract)                             | Extension API with safe fallback is planned but not implemented. | Detection and bounded DCS extension boundary; no rasterizer.            |
| [tmux](tmux.md#first-milestone-contract)                               | Unsupported: passthrough wrapping does not exist.                | Safe DCS passthrough and capability override behavior.                  |
| [GNU screen](gnu-screen.md#first-milestone-contract)                   | Unsupported: multiplexer profile does not exist.                 | Conservative feature filtering and DCS passthrough behavior.            |

The matrix may use only: typed and implemented; decoded and observable;
extension API with safe fallback; or unsupported with a specific reason.
