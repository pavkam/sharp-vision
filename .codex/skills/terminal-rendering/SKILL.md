---
name: terminal-rendering
description: Use when changing SharpVision cells, canvases, frame buffers, damage tracking, frame scheduling, cursor or SGR emission, synchronized output, terminal writes, resize invalidation, or render performance.
---

# Terminal Rendering

## Overview

Prove incremental output by applying it to a terminal model and comparing the
result with a full reference render. Fewer bytes are useful only after final
screen equivalence is established.

## Workflow

1. Read `docs/architecture/rendering-pipeline.md`,
   `docs/architecture/memory-ownership.md`,
   `docs/concepts/unicode-cell-geometry.md`, and
   `docs/testing/rendering.md`.
2. Write a failing transition test using two complete frames. Include the
   expected final virtual screen and relevant emitted bytes.
3. Separate damage detection, run planning, protocol encoding, and transport
   writing so each invariant can be tested independently.
4. Expand damage to complete grapheme ownership ranges, merge spans, and emit
   deterministic runs with explicit cursor and style state.
5. Compare incremental output with a full-render oracle across randomized frame
   pairs before tuning heuristics.
6. Record allocation, byte-count, write-count, and throughput evidence; update
   rendering and testing specs with behavior.

## Invariants

- Cell equality includes grapheme identity, width/continuation ownership,
  colors, attributes, hyperlinks, and renderer-visible metadata.
- No operation creates an orphan continuation cell or emits half a wide glyph.
- Resize, capability changes, alternate-screen transitions, interrupted writes,
  and out-of-band output force documented full invalidation.
- The encoder leaves cursor position, cursor visibility, styles, hyperlinks,
  and synchronized-output mode in a known state.
- Slow transports apply bounded backpressure; cancellation still attempts mode
  restoration without hiding the original failure.
- Front, back, and pooled storage have explicit ownership. Returned buffers are
  never observable.
- Steady-state scanning and emission allocate no object per cell.
- Controls draw cells only; they never choose ANSI/CSI/OSC sequences.
- Keep one named type per file, including generated files, name the file exactly
  after the type, and never declare nested named types.
- Make immutable value types readonly. Leave a struct mutable only when its role
  intrinsically advances or accumulates state, and keep that mutability narrow.
- Prefer readonly structs for small immutable wrappers with valid defaults and
  cheap copies; preserve classes for identity, ownership, polymorphism, or
  shared mutable state.
- Never use primary or positional constructors. Define constructors explicitly,
  validate before assignment, and document every rejected argument.
- Use named regions at genuine responsibility boundaries in substantial source
  files; avoid trivial nesting and split unrelated responsibilities instead.

## Example review

For a wide emoji replaced by a narrow letter, require damage to include the
lead and continuation cells, clear stale width, apply the emitted diff to the
virtual terminal, and compare it with a clean full render at the same size.

## Verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*Rendering*Tests" --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Comparing raw struct bytes or relying on boxed equality.
- Testing pretty snapshots without multi-frame state transitions.
- Optimizing cursor motion before proving terminal-model equivalence.
- Forgetting full invalidation after resize or interrupted output.
