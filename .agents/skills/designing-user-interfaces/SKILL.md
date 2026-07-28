---
name: designing-user-interfaces
description:
  Use when designing, composing, polishing, or reviewing SharpVision application
  interfaces, examples, showcase pages, dialogs, forms, menus, toolbars, status
  regions, responsive layouts, spacing, borders, shadows, or visual hierarchy.
---

# Designing User Interfaces

## Core principle

Design the information hierarchy first, then express it with semantic retained
layouts. A polished terminal interface is compact, aligned, responsive, and
restrained; fixed coordinates and decorative chrome are deliberate exceptions.

## Workflow

1. Identify primary content, navigation, commands, supporting information, and
   transient surfaces. Decide what must remain visible at narrow sizes.
2. Choose the layout by relationship: `Dock` for application regions, `Grid` for
   shared alignment, `Stack` for one-dimensional sequences, `Overlay` for
   layers, and `Canvas` only for intentional positioning or movable Windows.
3. Define the box model explicitly: margin separates siblings, panel spacing
   creates rhythm, border communicates a boundary, and padding separates that
   boundary from content.
4. Prefer intrinsic and responsive sizing. Give labels and actions `Auto`
   tracks, content fields `Star` tracks with useful minimums, and complete
   surfaces percentage widths bounded by cell minimums and maximums.
5. Establish visual hierarchy with semantic theme roles. Use borders to group,
   shadows to communicate elevation, and accent colour for state or priority.
6. Build one retained tree. Attach every visible `Popup` and `Window` to the
   mounted ownership tree, choose modeless visibility or modal presentation from
   the interaction contract, and wire every visible command.
7. Verify the same instance at narrow, normal, and wide terminal sizes. Check
   alignment, clipping, focus order, pointer targets, modal isolation, and final
   semantic cells.

## Non-negotiable design rules

- Do not build forms from fixed-width horizontal `Stack` rows. Use one `Grid` so
  labels, fields, validation, and actions share tracks.
- Do not use `Canvas` as a general responsive layout. It is for diagrams,
  overlays with authored positions, and draggable `Window` placement.
- Do not imitate web CSS with wrapper controls. Border and shadow are intrinsic
  `Control` properties; use a container only when it owns real layout or style.
- Do not assign fixed widths merely to align siblings. Express the shared edge
  in Grid tracks, spans, alignment, and spacing.
- Do not give every surface a border or every button a shadow. More chrome
  reduces hierarchy; dialogs commonly use a Window frame and flat inner actions.
- Do not treat one screenshot size as proof. Responsive composition must survive
  resize, tiny viewports, longer text, and collapsed optional regions.
- Do not make a Window modal merely because it floats above content. Use
  modality only when background interaction must be blocked.
- Keep actions conventional: primary action nearest the task, Cancel/Close at
  the trailing edge, button labels centered, and default/cancel semantics wired.

## Reference routing

Read only the references needed for the task:

| Need                                                                          | Reference                                                           |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Select `Dock`, `Grid`, `Stack`, `Overlay`, or `Canvas`                        | [layout-selection.md](references/layout-selection.md)               |
| Decide Auto, Star, Percent, fixed cells, alignment, margin, or padding        | [spacing-and-sizing.md](references/spacing-and-sizing.md)           |
| Apply borders, glyphs, colour roles, backgrounds, and shadows                 | [chrome-and-depth.md](references/chrome-and-depth.md)               |
| Design dialogs, Windows, Popups, menus, modality, and resize behavior         | [transient-surfaces.md](references/transient-surfaces.md)           |
| Start from an application shell, form, split view, toolbar, or overlay recipe | [composition-recipes.md](references/composition-recipes.md)         |
| Review quality and build mounted resize/render evidence                       | [review-and-verification.md](references/review-and-verification.md) |

Always read the nearest normative contract under `docs/controls/` and
`docs/concepts/`; these references explain design choices but do not replace the
product specification.

## Common mistakes

- Picking a panel because it is familiar instead of because its geometry matches
  the relationship.
- Hard-coding widths before deciding which region should absorb extra space.
- Nesting borders, shadows, and padding until usable content becomes cramped.
- Leaving status text, validation, or actions on unrelated alignment axes.
- Making a transient surface visible without placing it in the mounted tree.
- Polishing the normal size while narrow resize leaves controls unreachable.

## Verification

Use `testing-quality` for test design and run the focused mounted fixtures
before the repository gates:

```bash
make format
make lint
make build
make test
```
