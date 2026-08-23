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

## Required fixture work

- Every public concrete control needs its own focused detached-unit fixture and
  its own mounted `*SurfaceTests` fixture proving every route it supports:
  mounted rendering, hover or its explicit non-support, focus, Tab, directional
  keys, semantic press/release, activation, unavailable-state cleanup, transient
  layers, retained composition, and disabled state or its explicit non-support.
- Never add a reflection-based catalog test that scans the assembly and asserts
  a matching test/attribute/fixture exists for every exported type - that only
  proves a test exists, never that it exercises real behavior, and it rots
  silently the moment the catalog and the reflected set drift. Prove the shared
  contract through the control's own fixture instead, reviewed by hand against a
  sibling control with the same shape.

Use xUnit v3, Shouldly, Arrange/Act/Assert, real public behavior, and
deterministic fakes. Every focused command must include
`--minimum-expected-tests 1`.

Do not assert private call graphs or hand-write API-shape assertions —
`SharpVision.Compatibility.Tests` already freezes every production assembly's
public surface, so a shape test duplicates it and covers less. When a test needs
state a control does not expose, add a documented `internal` seam instead of
reflecting into private state.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Controls*" \
  --minimum-expected-tests 1 --timeout 60s
```

Before completion, run `make format`, `make lint`, `make build`, and
`make test`.
