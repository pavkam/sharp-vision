# Interface Composition

## Load this reference when

Designing or polishing an application surface, form, shell, toolbar, split view,
floating interaction, responsive hierarchy, spacing, chrome, or semantic color.

## Normative documentation

- [Layout panels](../../../../docs/concepts/layout.md#panels)
- [Box model](../../../../docs/concepts/box-model.md#box-model-contract)
- [Styling](../../../../docs/concepts/styling.md#styling-contract)
- [Intrinsic chrome](../../../../docs/concepts/intrinsic-chrome.md#intrinsic-chrome-contract)
- [Floating surfaces](../../../../docs/concepts/floating-surfaces.md#floating-surface-contract)
- [Showcase responsive behavior](../../../../docs/architecture/showcase.md#responsive-behavior)

## Composition guide

- Use Dock for outer application regions.
- Use one Grid when rows share column edges, especially forms.
- Use Stack for one-dimensional sequences with no cross-row alignment.
- Use Overlay for layers, absolute offsets, z-order, and movable Windows.
- Use margin between siblings, panel spacing for rhythm, border for a boundary,
  and padding inside that boundary.
- Prefer Auto for intrinsic labels/actions and Star for flexible content,
  bounded by useful minimums and maximums.

## Workflow

1. Identify primary content, navigation, commands, support information, and
   transient surfaces.
2. Choose panels from relationships rather than one screenshot.
3. Use semantic theme roles and one deliberate boundary or elevation signal.
4. Wire every visible command and conventional default/cancel behavior.
5. Verify the same retained instance at narrow, normal, and wide sizes with long
   text, focus order, pointer targets, and modal isolation.

## Project-specific traps

- Layout `Canvas` is retired. Terminal Canvas draws cells; Overlay positions UI.
- Do not align form rows with copied fixed widths or nested horizontal Stacks.
- Do not border every region or shadow every action.
- A Window frame or elevation does not imply modality.
