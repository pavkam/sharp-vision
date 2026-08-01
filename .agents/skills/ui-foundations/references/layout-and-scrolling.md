# Layout, Scrolling, and Invalidation

## Load this reference when

Changing measure, arrange, Length, fixed/percent/auto/star sizing, margin,
padding, alignment, Grid, Dock, Stack, Overlay, scrolling, auto-size, or update
phase invalidation.

## Normative documentation

- [Layout](../../../../docs/concepts/layout.md#overview)
- [Box model](../../../../docs/concepts/box-model.md#overview)
- [Scrolling](../../../../docs/concepts/scrolling.md#overview)
- [Invalidation](../../../../docs/concepts/invalidation.md#overview)
- [Control evidence](../../../../docs/testing/controls-integration.md#required-evidence)

## Code map

- Shared primitives and algorithms: `src/SharpVision/Layout/`
- Panels: `src/SharpVision/Controls/Layout/`
- Scrolling services and controls: `src/SharpVision/Scrolling/` and
  `Controls/Scrolling/`
- Tests: `tests/SharpVision.Tests/Layout/`, `Scrolling/`, and `Controls/Layout/`

## Workflow

1. State units, box edges, percentage base, unbounded behavior, rounding, and
   invalidation before coding.
2. Test fixed, auto, percent, star, min/max, margin, padding, alignment,
   overflow, resize, and tiny bounds as applicable.
3. Measure intrinsic content before final percentage resolution.
4. Solve two-axis scrollbar feedback before final arrangement and clamp offsets
   after every extent or viewport change.
5. Prove invalidation propagation, coalescing, clean-subtree reuse, and retry.

## Project-specific traps

- Absolute positions and z-order belong to `Overlay`; layout `Canvas` is
  retired.
- Border sides reserve cells; shadows affect visual overflow, not desired size.
- Scrolling and grow/shrink are intrinsic `Container` properties; there is no
  `ScrollView`.
- Reentrant layout is rejected; queued invalidation is drained by the update
  cycle.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Layout*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Controls.Layout*" \
  --minimum-expected-tests 1 --timeout 60s
```
