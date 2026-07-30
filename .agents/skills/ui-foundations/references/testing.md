# UI Foundation Testing

## Load this reference when

Changing tests or claiming control-tree, layout, scrolling, input, focus,
modality, styling, theme, or binding behavior complete.

## Normative documentation

- [Control and integration testing](../../../../docs/testing/controls-integration.md#control-and-integration-testing-contract)
- [End-to-end path](../../../../docs/testing/controls-integration.md#end-to-end-path)
- [Randomized testing](../../../../docs/testing/randomized.md#randomized-testing-contract)
- [Performance evidence](../../../../docs/testing/performance.md#ui-infrastructure-gates)
- [Continuous integration](../../../../docs/testing/continuous-integration.md#continuous-integration-contract)

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

## Completion verification

Run the focused command from each changed topic, then:

```bash
make format
make lint
make build
make test
```
