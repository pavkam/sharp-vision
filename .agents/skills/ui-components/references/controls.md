# Control Authoring

## Load this reference when

Adding or changing a concrete control, public property, state machine, command,
event, visual state, child role, layout override, or rendered content.

## Normative documentation

- [Control catalog](../../../../docs/controls/index.md#control-catalog)
- [Custom components](../../../../docs/concepts/custom-components.md#overview)
- [Intrinsic chrome](../../../../docs/concepts/intrinsic-chrome.md#overview)
- [Theming controls](../../../../docs/concepts/theming-new-controls.md#overview)
- [Control state-machine evidence](../../../../docs/testing/controls-integration.md#controls-with-state-machines)
- [Control-page contract](../../../../docs/documentation-guide.md#control-page-contract)
- [Control-page template](../../../../docs/documentation-guide.md#control-page-template)

Read the exact control contract from the control map. A new or changed public
control needs its own contract following the
[control-page template](../../../../docs/documentation-guide.md#control-page-template)
exactly, with a local Inheritance diagram and the canonical
`Member | Type | Default | Description` API table.

## Code map

- Base and ownership roles: `src/SharpVision/Controls/`
- Families: `Controls/Display/`, `Input/`, `Layout/`, `Scrolling/`
- Tests mirror the family under `tests/SharpVision.Tests/Controls/`
- Showcase panes: `examples/Showcase/Panes/`

## Workflow

1. Choose `ControlBase`, `Container`, `ContentControl`, `CompositeControlBase`,
   `ItemsControl`, or a narrower documented authoring base from the public
   ownership contract. Timed passive indicators reuse `AnimatedIndicatorBase`
   rather than owning another attachment timer and play/pause state machine.
2. Define validation, default, state precedence, input parity, focus, layout,
   rendering, invalidation, cleanup, and disposal.
3. Add unit and mounted surface failures, including disabled and tiny bounds.
4. Use nullable local `Style` plus resolved `ActualStyle` when a specialized
   control owns a complete presentation.
5. Add the control to the behavior registry, docs index, and showcase.

## Project-specific traps

- Call `InitializeContent` once in a concrete composite constructor.
- Override `MeasureOverride`, `ArrangeOverride`, and `OnRenderContent`; retired
  `View`, `Build()`, and chrome helpers do not exist.
- Raw Border and Shadow authoring is protected unless the control deliberately
  republishes complete chrome.
- An `AnimatedIndicatorBase` implementation draws only through `OnRenderFrame`;
  the base owns timer resumption, empty-content handling, and `ContentBounds`.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Controls*" \
  --minimum-expected-tests 1 --timeout 60s
```
