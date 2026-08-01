# Data Binding

## Load this reference when

Changing Binding, property paths, modes, conversion, notifications, observable
collections, item projection, selection synchronization, dispatcher coalescing,
or binding lifetime.

## Normative documentation

- [Data-binding contract](../../../../docs/concepts/data-binding.md#overview)
- [Notification model](../../../../docs/concepts/data-binding.md#notification-model)
- [Paths and ordering](../../../../docs/concepts/data-binding.md#paths-nulls-and-ordering)
- [Dispatcher responsiveness](../../../../docs/concepts/data-binding.md#dispatcher-and-responsiveness)
- [Data-binding proof](../../../../docs/testing/controls-integration.md#data-binding-proof)

## Code map

- Binding implementation: `src/SharpVision/DataBinding/`
- Target integration: controls and collection controls under `src/SharpVision/`
- Tests: `tests/SharpVision.Tests/DataBinding/`
- Unfriended consumer proof:
  `tests/SharpVision.Tests/Compatibility/DataBindingConsumerTests.cs`

## Workflow

1. Define source, target, mode, natural value, conversion, null, error, and
   disposal behavior.
2. Test initial synchronization and each permitted update direction.
3. Cover path replacement, missing members, collection deltas, selection,
   dispatcher bursts, stale queued work, cycles, and disposal.
4. Preserve notification ordering and coalesce background work without losing
   the latest valid value.
5. Retain external consumer proof for public/protected extensibility.

## Project-specific traps

- Component-owned bindings must have an explicit lifetime; detached targets must
  not retain sources.
- Do not turn collection replacement into reset churn when a semantic delta is
  available.
- Never mutate dispatcher-affine targets directly from source notification
  threads.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.DataBinding*" \
  --minimum-expected-tests 1 --timeout 60s
```
