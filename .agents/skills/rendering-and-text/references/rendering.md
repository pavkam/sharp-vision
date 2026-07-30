# Rendering Pipeline

## Load this reference when

Changing Canvas, Cell, Frame, damage tracking, run planning, cursor movement,
SGR, synchronized output, terminal writes, resize invalidation, or frame commit.

## Normative documentation

- [Rendering pipeline](../../../../docs/architecture/rendering-pipeline.md#rendering-pipeline-contract)
- [Cell and frame rules](../../../../docs/architecture/rendering-pipeline.md#cell-and-frame-rules)
- [Commit and invalidation](../../../../docs/architecture/rendering-pipeline.md#commit-and-terminal-state-invalidation)
- [Memory ownership](../../../../docs/architecture/memory-ownership.md#memory-ownership-contract)
- [Rendering evidence](../../../../docs/testing/rendering.md#rendering-equivalence-contract)

## Code map

- Cells, Canvas, Frame, damage, encoder, renderer:
  `src/SharpVision.Terminal/Rendering/`
- Geometry and image placement values: `src/SharpVision.Terminal/Geometry/` and
  `Graphics/`
- Transition and oracle tests: `tests/SharpVision.Terminal.Tests/Rendering/`
- Independent terminal model:
  `tests/SharpVision.Terminal.Tests/Support/VirtualScreen.cs`

## Workflow

1. Express the change as complete frame A to complete frame B.
2. Assert semantic damage, emitted bytes where relevant, and the final virtual
   screen.
3. Expand changes across both frames' complete grapheme ownership ranges.
4. Keep damage detection, run planning, encoding, transport writing, and commit
   independently testable.
5. Compare incremental output with a clean full render before tuning heuristics.

## Project-specific traps

- `Canvas` is a frame drawing value, not a UI layout panel.
- The encoder emits lead cells and skips continuations; Frame repair owns stale
  continuation cleanup.
- Out-of-band output and interrupted writes can invalidate remembered terminal
  state even when frame cells did not change.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Rendering*" \
  --minimum-expected-tests 1 --timeout 60s
```
