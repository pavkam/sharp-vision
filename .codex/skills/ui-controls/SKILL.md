---
name: ui-controls
description: Use when adding or changing SharpVision controls, mutable properties, child ownership, visual states, control events, keyboard or pointer behavior, focus semantics, control rendering, public control APIs, or showcase pages.
---

# UI Controls

## Overview

Build traditional mutable controls with predictable ownership, invalidation,
input, and rendering. A control is complete only with docs, behavioral tests,
and a showcase page.

## Workflow

1. Read the control contract under `docs/controls/` plus
   `docs/concepts/styling.md`, `docs/concepts/focus.md`,
   `docs/concepts/input-routing.md`, and `docs/concepts/layout.md`.
2. Define the control's property validation, state machine, event order,
   keyboard/pointer/focus behavior, layout, and visual states before coding.
3. Write failing public-surface tests for state, invalidation, routed input,
   cell output, disabled behavior, and tiny bounds.
4. Implement an ordinary mutable control over shared tree, dispatcher, layout,
   style, and canvas services. Do not add a private mini-framework.
5. Add the control's interactive showcase variants and event log coverage.
6. Update XML docs, the control spec, and showcase documentation with behavior.

## Invariants

- Controls never emit ANSI, CSI, OSC, or terminal strings; they draw to a
  clipped cell canvas.
- Mutation is dispatcher-affine. Validated setters invalidate only measure,
  arrange, or render as required.
- A child has one parent. Managed collections reject nulls, duplicates, cycles,
  and cross-parent insertion, and clean focus/capture on removal.
- Visual-state precedence for normal, hovered, pressed, focused, checked, and
  disabled remains deterministic for combined states.
- Keyboard and pointer paths produce equivalent semantic actions. Disabled or
  hidden controls do not activate.
- Unicode content measures, clips, selects, and draws through shared cell
  geometry.
- Closing popups, menus, or windows releases capture and restores focus by the
  documented policy.
- Public/internal members have useful XML docs, validation, exceptions, and
  examples where relevant.
- Keep one named type per non-generated file, name the file exactly after the
  type, and split any existing multi-type or nested-type file when touching it.

## Example review

For `CheckBox`, require explicit two- and three-state transitions, Space and
pointer activation, checked/focused/disabled combinations, exact invalidation,
cell rendering, routed events, and an interactive showcase page.

## Verification

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*Control*Tests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Drawing bytes directly or bypassing clipping and shared geometry.
- Allowing arbitrary child lists or cross-thread property mutation.
- Testing appearance without event order, focus, disabled, or resize behavior.
- Shipping a control without its normative doc and showcase page.
