# Showcase

## Load this reference when

Adding or changing a showcase pane, DocPage composition, interactive example,
navigation entry, event log, responsive behavior, or visual capture.

## Normative documentation

- [Showcase contract](../../../../docs/architecture/showcase.md#overview)
- [Responsive behavior](../../../../docs/architecture/showcase.md#responsive-behavior)
- [Verification contract](../../../../docs/architecture/showcase.md#verification)
- [Mounted surfaces](../../../../docs/testing/controls-integration.md#mounted-component-surfaces)

## Code map

- Application and gallery: `examples/Showcase/`
- Component panes: `examples/Showcase/Panes/`
- Documentation composition: `DocPage`, `DocSection`, `DocExample`, `DocRow`,
  and `DocColumn` under the showcase project
- Automated gallery evidence: `tests/SharpVision.Tests/Showcase/`
- Real capture helper: `scripts/capture-showcase.sh`
- Per-control document images: `npm run capture:controls` regenerates
  `docs/images/controls/` from `scripts/control-image-manifest.mjs`; run it
  after changing a pane whose image appears in `docs/controls/`

## Workflow

1. Show representative defaults, states, validation, interaction, and event
   output through the real public API.
2. Keep the example live and mounted; do not draw a fake screenshot of behavior.
3. Verify narrow, normal, and wide sizes plus long content and focus traversal.
4. Use `scripts/capture-showcase.sh` for deterministic launch, navigation,
   pointer injection, capture, and rendering rather than fixed sleeps.

## Project-specific traps

- The showcase compiles as production example code and has no dedicated test
  project.
- Generic gallery rendering does not prove a pane's critical interaction. Add a
  targeted mounted regression when the example makes a behavioral promise.
- Preserve current semantic colors; literal colors are for exact color semantics
  only.
- A fixed `Length.Cells` specimen width above roughly 46-47 usable columns
  silently clamps and disappears instead of overflowing: `ControlBase`'s
  `ResolveArrangeAxis` clamps an over-wide fixed request down to the available
  arrange slot, and `DocPage` hides its horizontal scrollbar
  (`HorizontalBarVisibility`), so there is no affordance revealing the
  clamped-away content. Keep a pane's fixed specimen widths inside the current
  reading column.
