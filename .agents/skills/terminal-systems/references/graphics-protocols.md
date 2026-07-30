# Graphics Protocols

## Load this reference when

Changing Kitty graphics, sixel, iTerm2 inline images, graphics capability
evidence, backend authorization, payload chunking, identifiers, or cleanup
bytes.

## Normative documentation

- [Kitty graphics](../../../../docs/protocols/kitty-graphics.md#kitty-graphics-contract)
- [Sixel](../../../../docs/protocols/sixel.md#sixel-contract)
- [iTerm2](../../../../docs/protocols/iterm2.md#iterm2-contract)
- [Backend graphics boundary](../../../../docs/architecture/terminal-backends.md#graphics-backend-boundary)
- [Image ownership](../../../../docs/concepts/images.md#image-ownership-contract)

## Code map

- Protocol-neutral graphics values: `src/SharpVision.Terminal/Graphics/`
- Encoders: `Graphics/Backends/`, `Kitty/`, `Iterm/`, and `Sixel/`
- Selection proof:
  `tests/SharpVision.Terminal.Tests/Graphics/BackendSelectorTests.cs`
- Family tests: `tests/SharpVision.Terminal.Tests/Graphics/`

## Workflow

1. Separate capability evidence, backend authorization, encoding, placement,
   ordinary-cell fallback, and resource cleanup.
2. Test exact bytes, size limits, chunk boundaries, identifiers, cancellation,
   deletion, multiplexer wrapping, and unsupported fallback.
3. Preserve ownership across queued writes; never expose returned pooled memory.
4. Use `rendering-and-text` when changing placement or frame composition.

## Project-specific traps

- A supported graphics protocol must not mutate terminal-family identity.
- Query evidence and user overrides may authorize behavior; guesses may not.
- Cell fallback is renderer behavior, not terminal backend resolution.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Graphics*" \
  --minimum-expected-tests 1 --timeout 60s
```
