# Terminal Input

## Load this reference when

Changing keyboard, mouse, paste, focus, UTF-8 decoding, escape disambiguation,
incremental parsing, or typed terminal input events.

## Normative documentation

- [Keyboard protocols](../../../../docs/protocols/kitty-keyboard.md#overview)
- [Mouse protocols](../../../../docs/protocols/mouse.md#overview)
- [Paste and focus](../../../../docs/protocols/paste-focus.md#overview)
- [Protocol testing](../../../../docs/testing/terminal-protocols.md#overview)
- [Pseudoterminal evidence](../../../../docs/testing/pseudoterminals.md#overview)

## Code map

- Decoders and input values: `src/SharpVision.Terminal/Input/`
- Input protocol helpers: `src/SharpVision.Terminal/Protocols/`
- Focus, paste, keyboard, mouse tests: `tests/SharpVision.Terminal.Tests/Input/`
- End-to-end transport proof: `tests/SharpVision.Terminal.Tests/Transport/`

## Workflow

1. Define the typed event, coordinate units, modifier state, and ambiguity rule.
2. Test the sequence as one read and across every meaningful split point.
3. Cover invalid UTF-8, truncated escapes, timeout boundaries, and parser
   recovery.
4. Preserve cell and pixel coordinates when both are supplied.
5. Prove adjacent input continues after malformed or unknown sequences.

## Project-specific traps

- Never reduce Unicode input to `char`.
- Do not confuse terminal decoding with UI routed-input behavior.
- Escape timeout policy must be deterministic under fake time.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Input*" \
  --minimum-expected-tests 1 --timeout 60s
```
