---
name: ui-foundations
description:
  Use when changing SharpVision control-tree ownership, retained composition,
  layout, sizing, scrolling, invalidation, styling, themes, data binding, routed
  input, focus, pointer capture, modality infrastructure, or shared UI state
  propagation.
---

# UI Foundations

## Overview

Keep the retained mutable UI tree deterministic across ownership, layout,
appearance, binding, input, and update phases. Shared foundation behavior has
one owner and one normative contract.

## Workflow

1. Route the task to the smallest matching references.
2. Read their normative sections, code owners, and nearest tests before changing
   public behavior.
3. State ownership, units, validation, dispatcher affinity, invalidation impact,
   ordering, and lifetime.
4. Add a focused failing public-behavior test and any required mounted or
   randomized evidence.
5. Implement through shared tree, layout, appearance, binding, and routing
   services; do not create a component-local substitute.
6. Reconcile docs and run focused verification before repository gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                       | Read                                                                  | Normative starting point                                                                                  |
| --------------------------------------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Parent/child ownership, Container, ContentControl, CompositeControl, ItemsControl | [control-tree.md](references/control-tree.md)                         | [Custom components](../../../docs/concepts/custom-components.md#custom-components-contract)               |
| Measure, arrange, Length, Grid, Dock, Stack, Overlay, scrolling, invalidation     | [layout-and-scrolling.md](references/layout-and-scrolling.md)         | [Layout](../../../docs/concepts/layout.md#layout-contract)                                                |
| Routed events, focus, capture, modality planes, access keys                       | [input-focus-and-modality.md](references/input-focus-and-modality.md) | [Input routing](../../../docs/concepts/input-routing.md#input-routing-contract)                           |
| Theme, ThemeRole, Style, ActualStyle, visual states, Border, Shadow               | [styling-and-themes.md](references/styling-and-themes.md)             | [Styling](../../../docs/concepts/styling.md#styling-contract)                                             |
| Binding, property paths, modes, notification, observable collections, selection   | [data-binding.md](references/data-binding.md)                         | [Data binding](../../../docs/concepts/data-binding.md#data-binding-contract)                              |
| Any UI-foundation verification                                                    | [testing.md](references/testing.md)                                   | [Control testing](../../../docs/testing/controls-integration.md#control-and-integration-testing-contract) |

<!-- markdownlint-enable MD013 -->

## Boundaries

- Use `ui-components` for concrete control state machines, Window/Popup/Dialog
  behavior, application composition, and showcase pages.
- Use `rendering-and-text` for grapheme geometry, cells, Canvas, and frame
  output.
- Use `runtime-and-hosting` for dispatcher execution, timers, event-loop
  ordering, hosting, and terminal lifetime.
- Consuming a foundation API does not require editing its implementation.

## Invariants

- One dispatcher owns tree mutation, focus, capture, layout, render, and
  callbacks.
- One child has at most one parent; reject nulls, duplicates, cycles, and
  cross-parent insertion before mutation.
- Invalidate only the required measure, arrange, or render phase.
- Layout uses terminal cells, saturating box-model arithmetic, and deterministic
  rounding.
- Routed input snapshots ancestry; capture and modality constrain targeting
  before preview and bubble routing.
- Theme and binding updates commit coherent observable state on the dispatcher.
- No callback runs while an internal lock is held.

## Common mistakes

- Using terminal `Canvas` as a layout panel; absolute UI positioning belongs to
  `Overlay` offsets.
- Constructing retained children during measure or render.
- Treating component-level focus restoration as a replacement for shared
  modality policy.
- Mutating appearance or binding targets from background notification threads.
