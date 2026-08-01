---
name: rendering-and-text
description:
  Use when changing SharpVision cells, Canvas, frames, damage, cursor or SGR
  emission, Unicode graphemes, width, wrapping, text layout, image placement,
  FIGlet parsing or rendering, frame scheduling, render performance, or final
  terminal-screen equivalence.
---

# Rendering and Text

## Overview

Keep semantic content, cell ownership, frame transitions, and emitted terminal
state equivalent. Optimize bytes and allocations only after a full-render oracle
proves the same final screen.

## Workflow

1. Route the task to the smallest matching references.
2. Read their exact normative sections, source owners, and nearest tests.
3. Add a focused failing semantic, transition, conformance, or ownership test.
4. Implement through shared grapheme, cell, frame, image, and renderer
   abstractions rather than local approximations.
5. Prove cross-consumer consistency and update the owning docs, tests, and
   showcase surface when observable behavior changes.
6. Run focused verification, then repository gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                      | Read                                        | Normative starting point                                                          |
| -------------------------------------------------------------------------------- | ------------------------------------------- | --------------------------------------------------------------------------------- |
| Canvas, Cell, Frame, damage, cursor, SGR, synchronized output, terminal writes   | [rendering.md](references/rendering.md)     | [Rendering pipeline](../../../docs/architecture/rendering-pipeline.md#overview)   |
| Graphemes, Rune decoding, width, wide cells, wrapping, clipping, cursor geometry | [unicode.md](references/unicode.md)         | [Unicode cell geometry](../../../docs/concepts/unicode-cell-geometry.md#overview) |
| Image ownership, placement, composition, ordinary-cell fallback                  | [images.md](references/images.md)           | [Images](../../../docs/concepts/images.md#overview)                               |
| FIGfont parser, catalog, smushing, embedded fonts, provenance                    | [figlet.md](references/figlet.md)           | [FigletText](../../../docs/controls/display/figlet-text.md#overview)              |
| Allocation, throughput, write counts, performance budgets                        | [performance.md](references/performance.md) | [Performance testing](../../../docs/testing/performance.md#overview)              |
| Any rendering or text verification                                               | [testing.md](references/testing.md)         | [Rendering evidence](../../../docs/testing/rendering.md#overview)                 |

<!-- markdownlint-enable MD013 -->

## Boundaries

- Use `terminal-systems` for protocol grammar, capability evidence, graphics
  backend authorization, and exact graphics encoding.
- Use `ui-foundations` for layout-engine measurement and invalidation policy.
- Use `ui-components` for TextInput editing state and concrete control behavior.
- Use `runtime-and-hosting` for renderer/session shutdown ordering and transport
  lifetime.

## Invariants

- Segment extended grapheme clusters before measuring cells.
- Never split, draw, clear, or expose half of a wide cluster.
- Cell equality includes every renderer-visible semantic field.
- Incremental output must produce the same modeled terminal state as a clean
  full render.
- Front, back, image, and pooled storage have explicit ownership boundaries.
- Resize, capability changes, alternate-screen transitions, and interrupted
  writes cause documented invalidation.
- Hot scanning, width, and emission paths allocate no object per cell or Rune.

## Common mistakes

- Fixing drawing without updating selection, hit testing, cursor movement, or
  repair.
- Treating East Asian Width as the complete terminal-width algorithm.
- Testing snapshots without multi-frame terminal-model equivalence.
- Optimizing cursor motion before proving semantic correctness.
