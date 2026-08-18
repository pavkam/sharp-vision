# UI Foundation Testing

## Load this reference when

Changing tests or claiming control-tree, layout, scrolling, input, focus,
modality, styling, theme, or binding behavior complete.

## Normative documentation

- [Control and integration testing](../../../../docs/testing/controls-integration.md#overview)
- [End-to-end path](../../../../docs/testing/controls-integration.md#end-to-end-path)
- [Randomized testing](../../../../docs/testing/randomized.md#overview)
- [Performance evidence](../../../../docs/testing/performance.md#ui-infrastructure-gates)
- [Continuous integration](../../../../docs/testing/continuous-integration.md#overview)
- [Shape and reflection](../../../../docs/testing/correctness-model.md#shape-and-reflection)

## Evidence ladder

- Pure primitive and algorithm tests.
- Engine tests with recording controls and deterministic fakes.
- Mounted component surfaces for final cells, routed interaction, focus, and
  cleanup.
- Randomized geometry, track, scroll, and binding invariants with recorded
  seeds.
- Unfriended consumer proof for public extensibility.
- End-to-end input through dispatcher, layout, rendering, and terminal output
  where the contract crosses those boundaries.

Every `dotnet test` command must use supported filter grammar and
`--minimum-expected-tests 1`, including exact-class refinements.

Assert observable output and state, not private call graphs or member shape.
`SharpVision.Compatibility.Tests` already freezes all three public surfaces; a
hand-written shape assertion duplicates it and covers less. When a test needs
state a control does not expose, add a documented `internal` seam rather than
reflecting into private state — see
[Shape and reflection](../../../../docs/testing/correctness-model.md#shape-and-reflection).

## Completion verification

Run the focused command from each changed topic, then:

```bash
make format
make lint
make build
make test
```
