# Protocols

## Load this reference when

Changing ANSI, ECMA-48, CSI, OSC, DCS, DEC private modes, SGR, xterm, tmux, GNU
screen, synchronized output, Kitty, iTerm2, or sixel grammar and encoding.

## Normative documentation

- [Protocol families](../../../../docs/protocols/index.md#protocol-families)
- [Coverage matrix](../../../../docs/protocols/coverage-matrix.md#coverage)
- [Runtime routing](../../../../docs/protocols/runtime-routing.md#overview)
- [Terminal protocol evidence](../../../../docs/testing/terminal-protocols.md#required-evidence)

Read the topic page selected by the protocol map. Verify its primary source and
version before changing an evolving extension.

## Code map

- Grammar and typed protocol values: `src/SharpVision.Terminal/Protocols/`
- Extension families: `Kitty/`, `Iterm/`, `Sixel/`, `Xterm/`
- Multiplexer framing: `src/SharpVision.Terminal/Multiplexing/`
- Exact-byte and recovery tests: `tests/SharpVision.Terminal.Tests/Protocols/`

## Workflow

1. State grammar, terminator, parameter defaults, finite bounds, and recovery.
2. Add exact encoder bytes and representative decoder split-point tests.
3. Exercise malformed, oversized, interrupted, unknown, and adjacent sequences.
4. Prove the typed command or event through the real routing boundary.
5. Update the topic contract and coverage state only after observable proof.

## Project-specific traps

- Keep OSC 52 and Kitty OSC 5522 clipboard transactions distinct.
- Do not let tmux or screen wrapping erase the inner protocol's limits.
- Strict diagnostics may promote invalid input to errors; they must not change
  valid output.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Protocols*" \
  --minimum-expected-tests 1 --timeout 60s
```
