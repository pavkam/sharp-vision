# Control Tree and Ownership

## Load this reference when

Changing parentage, owned controls, Container children, ContentControl,
CompositeControlBase, ItemsControl, private presentation hosts, removal, or
disposal.

## Normative documentation

- [Custom components](../../../../docs/concepts/custom-components.md#overview)
- [Retained private composition](../../../../docs/concepts/custom-components.md#retained-private-composition)
- [Semantic item presentation](../../../../docs/concepts/custom-components.md#semantic-item-presentation)
- [Control integration](../../../../docs/testing/controls-integration.md#overview)

## Code map

- Base tree and ownership registry: `src/SharpVision/Controls/`
- Layout containers: `src/SharpVision/Controls/Layout/`
- Semantic collections: `src/SharpVision/Controls/Collections/`
- Ownership tests: `tests/SharpVision.Tests/Controls/`

## Workflow

1. Choose the public ownership role before adding children.
2. Validate null, duplicate, cycle, disposed, and cross-parent cases before
   observable mutation.
3. Test parent changes, focus/capture cleanup, dispatcher adoption, disposal,
   and exception aggregation.
4. Keep private hosts inside the same ownership registry as public children.
5. When one semantic item spans several owned hosts, stage every final slot
   snapshot in one compound ownership transaction. Commit framework mappings
   before lifecycle publication, preserve deterministic participant order, and
   test callback failure plus cross-host reentrancy.

## Project-specific traps

- Only a true panel derives from `Container` and exposes `Children`.
- A composite constructor creates one permanent root and calls
  `InitializeContent` exactly once; `View` and measure-time `Build()` are
  retired.
- Permanent capacity-one slots are enforced by the ownership registry for both
  `CompositeControlBase` and `ItemsControl`. An incomplete owner cannot enter a
  tree or attach, and direct root disposal never reopens initialization.
- `ContentControl` is caller-replaceable content; `ItemsControl` exposes
  semantic items rather than its presentation host.
- Sequential mutations of parallel private hosts expose half an item to
  lifecycle callbacks. Do not repair that with callback-time rollback; use the
  compound transaction and leave its complete committed state in place.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*CompositeControlBaseTests" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*OwnedControlRegistryTests" \
  --minimum-expected-tests 1 --timeout 60s
```
