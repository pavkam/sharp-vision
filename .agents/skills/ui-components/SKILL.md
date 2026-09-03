---
name: ui-components
description:
  Use when adding or changing SharpVision concrete controls, control state
  machines, collections, navigation, menus, popups, windows, dialogs, TextInput
  or transient numeric editing, application-surface composition, showcase pages,
  mounted component surfaces, or component interaction and rendering.
---

# UI Components

## Overview

Build retained mutable components whose public contract, state, appearance,
interaction, documentation, tests, and showcase example agree. Component code
composes shared foundations; it does not invent local frameworks.

## Workflow

1. Route the task to the matching component and verification references.
2. Read the exact control, dialog, concept, and testing contracts plus the
   current declaration, nearest tests, and showcase page.
3. Define validation, defaults, state transitions, event order, focus/input,
   layout, appearance, ownership, and disposal.
4. Add a focused failing behavioral test and mounted surface evidence before
   implementation.
5. Implement the smallest retained component over shared UI foundations.
6. Update XML docs, normative contract, behavior catalog, and interactive
   showcase together, following the
   [control-page template](../../../docs/documentation-guide.md#control-page-template)
   exactly; then run focused and repository gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                  | Read                                                                      | Normative starting point                                                                     |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Concrete control, properties, events, state, rendering, retained composition | [controls.md](references/controls.md)                                     | [Control index](../../../docs/controls/index.md#control-catalog)                             |
| List, Tree, Tab, selection, semantic items, navigation                       | [collections-and-navigation.md](references/collections-and-navigation.md) | [ItemsControl](../../../docs/controls/items-control.md#overview)                             |
| Menu, Popup, Flyout, Tooltip, Window, Dialog, modal presentation             | [floating-surfaces.md](references/floating-surfaces.md)                   | [Floating surfaces](../../../docs/concepts/floating-surfaces.md#overview)                    |
| TextInput, NumberInput, CurrencyInput, editing, selection, cursor, undo      | [text-editing.md](references/text-editing.md)                             | [TextInput](../../../docs/controls/input/text-input.md#overview)                             |
| Application composition, forms, responsive regions, hierarchy, chrome        | [design.md](references/design.md)                                         | [Showcase responsive behavior](../../../docs/architecture/showcase.md#responsive-behavior)   |
| Showcase page, DocPage, examples, navigation, visual capture                 | [showcase.md](references/showcase.md)                                     | [Showcase contract](../../../docs/architecture/showcase.md#overview)                         |
| Component tests, surfaces, behavior evidence, showcase proof                 | [testing.md](references/testing.md)                                       | [Mounted surfaces](../../../docs/testing/controls-integration.md#mounted-component-surfaces) |

<!-- markdownlint-enable MD013 -->

## Boundaries

- Use `ui-foundations` when changing tree ownership, layout algorithms, routing,
  focus/modality policy, themes, styles, or binding infrastructure.
- Use `rendering-and-text` when changing grapheme geometry, cells, Canvas,
  frames, or semantic rendering primitives.
- Use `runtime-and-hosting` for dispatcher execution and application lifecycle.
- A component may consume those contracts without loading their skills.

## Invariants

- Controls render cells through `OnRenderContent`; they never emit terminal
  bytes.
- Validate every public mutation before changing observable state.
- Keyboard and pointer paths produce the same semantic action.
- Disabled, hidden, removed, or disposed components release transient
  interaction state and do not activate.
- Every public concrete control has explicit mounted behavior classification.
- Every shipped component has aligned XML docs, normative docs, tests, and a
  showcase page.

## Common mistakes

- Shipping a helper-only test without mounted cells and interaction proof.
- Rebuilding retained children during measure or render.
- Implementing focus restoration in Window when the shared modality policy is
  faulty.
- Treating a pretty screenshot at one size as responsive evidence.
