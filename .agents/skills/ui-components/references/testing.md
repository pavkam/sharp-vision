# Component Testing

## Load this reference when

Adding or changing component tests, mounted surfaces, behavior classification,
showcase verification, rendering assertions, or interaction evidence.

## Normative documentation

- [Control testing](../../../../docs/testing/controls-integration.md#overview)
- [Mounted surfaces](../../../../docs/testing/controls-integration.md#mounted-component-surfaces)
- [State machines](../../../../docs/testing/controls-integration.md#controls-with-state-machines)
- [Required evidence](../../../../docs/testing/controls-integration.md#required-evidence)
- [Showcase verification](../../../../docs/architecture/showcase.md#verification)
- [Shape and reflection](../../../../docs/testing/correctness-model.md#shape-and-reflection)

## Evidence levels

1. Use Engine and probe controls for isolated layout/state behavior.
2. Use `ComponentSurface` for mounted rendering, pointer, keyboard, focus,
   capture, resize, cleanup, and final semantic cells.
3. Run and capture the real showcase for visible application composition.

## Required catalog work

- Add every public concrete Control to `_requirements` in
  `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`.
- Assign exactly one fixture and explicit required/excluded behavior pairs.
- Mark mounted evidence methods with `ComponentBehaviorEvidence` for the exact
  behaviors they prove.
- The registry and attributed evidence must match exactly; an attribute alone is
  insufficient.

Use xUnit v3, Shouldly, Arrange/Act/Assert, real public behavior, and
deterministic fakes. Every focused command must include
`--minimum-expected-tests 1`.

Do not assert private call graphs or hand-write API-shape assertions —
`SharpVision.Compatibility.Tests` already freezes all three public surfaces, so
a shape test duplicates it and covers less. When a test needs state a control
does not expose, add a documented `internal` seam instead of reflecting into
private state.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ComponentSurfaceCoverageTests" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Controls*" \
  --minimum-expected-tests 1 --timeout 60s
```

Before completion, run `make format`, `make lint`, `make build`, and
`make test`.
