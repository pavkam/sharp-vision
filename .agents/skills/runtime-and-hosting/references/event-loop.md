# Runtime Event Loop

## Load this reference when

Changing read/dispatch order, timers, update phases, render scheduling, idle,
resize, out-of-band writes, stop requests, shutdown, or run/dispose races.

## Normative documentation

- [Runtime event loop](../../../../docs/architecture/runtime-event-loop.md#overview)
- [Iteration order](../../../../docs/architecture/runtime-event-loop.md#iteration-order)
- [Resize ordering](../../../../docs/architecture/runtime-event-loop.md#resize-ordering)
- [Out-of-band writes](../../../../docs/architecture/runtime-event-loop.md#out-of-band-protocol-writes)
- [Shutdown](../../../../docs/architecture/runtime-event-loop.md#shutdown)
- [Lifecycle events](../../../../docs/concepts/lifecycle-events.md#overview)

## Code map

- UI loop and ownership: `src/SharpVision/Application.cs`
- Terminal Session loop: `src/SharpVision.Terminal/Runtime/Session.cs`
- Tests: `tests/SharpVision.Tests/Runtime/` and terminal
  `Runtime/SessionTests.cs`

## Workflow

1. Write the expected ordered trace before changing code.
2. Test input, due timers, invalidation, layout, render, lifecycle callbacks,
   idle, waiting, resize bursts, and stop/dispose interleavings.
3. Assert committed geometry and final terminal state, not only callback counts.
4. Preserve the primary exception while aggregating independent cleanup
   failures.

## Project-specific traps

- Stop request and caller wait are separate states.
- Out-of-band protocol writes must reconcile renderer terminal state.
- Resize events observe completed layout for the newest coalesced size.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Runtime*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-class "*SessionTests" \
  --minimum-expected-tests 1 --timeout 60s
```
