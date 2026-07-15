---
name: layout-input-events
description: Use when changing SharpVision measure or arrange, fixed/percentage/auto/proportional sizing, margin, padding, alignment, scrolling, routed input, focus, pointer capture, dispatcher affinity, resize, idle, timers, or lifecycle event order.
---

# Layout, Input, and Events

## Overview

Keep geometry and callbacks deterministic under resize, mutation, overflow, and
asynchrony. Layout never guesses final space during intrinsic measurement.

## Workflow

1. Read `docs/concepts/layout.md`, `docs/concepts/scrolling.md`,
   `docs/concepts/input-routing.md`, `docs/concepts/focus.md`,
   `docs/concepts/threading.md`, and `docs/concepts/lifecycle-events.md`.
2. State units, box-model edges, percentage base, rounding, event order, and
   invalidation effects before changing APIs.
3. Write failing tests with recording controls plus fake terminal, dispatcher,
   and clock. Assert order and committed geometry, not merely final values.
4. Implement in dependency order: sizing primitives, measure/arrange, panels,
   scroll host, routed input, then dispatcher/runtime events.
5. Reject reentrant layout. Queue invalidation and coalesce resize records on
   the dispatcher.
6. Update concept docs, control contracts, diagrams, and showcase resize/scroll
   examples with behavior.

## Invariants

- Fixed lengths use cells. Percentages resolve against the final parent content
  box after padding and reserved scrollbars.
- During an unbounded measure, percentage is intrinsic/automatic and resolves
  during arrangement; deterministic cumulative rounding assigns remainders.
- Margin is external, padding internal, min/max clamps the border box, and
  deflation saturates at zero.
- Automatic scrollbar visibility is solved before final arrange. Adding one bar
  must re-evaluate the other; offsets clamp after every extent/viewport change.
- Layout helpers may measure or arrange only a direct owned child. Private
  composition and item hosts participate through the same ownership registry as
  panel children; never reintroduce a public `Children` escape hatch merely to
  reach private presentation controls.
- One dispatcher owns tree mutation, focus, capture, layout, render, and user
  callbacks. No callback runs under an internal lock.
- Routed input snapshots ancestry, previews root-to-target, then bubbles
  target-to-root. Capture wins over pointer hit testing; focus receives keys.
- Resize commits the newest size, completes root layout, raises one event with
  committed geometry, then renders.
- `Idle` fires once after input, due timers, layout, and render drain, directly
  before waiting. Work queued by `Idle` is drained without busy spinning.
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

For percentage content in an automatic two-axis scroll view, test intrinsic
measure, final viewport resolution, horizontal-bar-induced vertical overflow,
offset clamping, clipping, hit testing, resize removal, and event/render order.

## Verification

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*Layout*Tests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*Runtime*Tests" --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Resolving percentages from a provisional measure constraint.
- Adding scrollbars after final arrangement without the two-axis feedback pass.
- Running callbacks under locks or mutating the tree from transport threads.
- Raising resize before committed layout or firing idle in a polling loop.
