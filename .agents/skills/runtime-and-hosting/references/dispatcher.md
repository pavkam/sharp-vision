# Dispatcher

## Load this reference when

Changing dispatcher affinity, queued work, invoke/post, timers, idle, wakeup,
reentrancy, callback ordering, cancellation, or exception propagation.

## Normative documentation

- [Threading](../../../../docs/concepts/threading.md#threading-contract)
- [Dispatcher timers](../../../../docs/concepts/threading.md#dispatcher-timers)
- [Locks and reentrancy](../../../../docs/concepts/threading.md#locks-and-reentrancy)
- [Lifecycle ordering](../../../../docs/concepts/lifecycle-events.md#ordering)
- [Runtime iteration](../../../../docs/architecture/runtime-event-loop.md#iteration-order)

## Code map

- Dispatcher and timers: `src/SharpVision/Threading/`
- Application integration: `src/SharpVision/Runtime/Application.cs`
- Tests: `tests/SharpVision.Tests/Threading/` and `Runtime/OrderingTests.cs`

## Workflow

1. Define owning thread, queue order, completion, cancellation, and reentrancy.
2. Test immediate and queued calls, cross-thread posting, timer due order,
   callback exceptions, disposal, and work queued from callbacks.
3. Use fake time and explicit wakeups; do not rely on sleeps.
4. Keep internal state protected without invoking user code under locks.

## Project-specific traps

- Dispatcher affinity begins after attachment and covers all observable tree
  mutation.
- Idle is an event-loop state, not a polling timer.
- A callback may queue more work; drain it deterministically without recursive
  layout or render.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Threading*" \
  --minimum-expected-tests 1 --timeout 60s
```
