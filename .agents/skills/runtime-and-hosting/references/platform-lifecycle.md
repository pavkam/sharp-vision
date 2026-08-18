# Platform Lifecycle

## Load this reference when

Changing Session, transport, ConsoleConnection, Unix or Windows console modes,
raw mode, VT modes, alternate screen, host leases, restoration, cancellation, or
cleanup exception behavior.

## Normative documentation

- [Terminal Session](../../../../docs/architecture/runtime-event-loop.md#terminal-session-implementation)
- [Shutdown](../../../../docs/architecture/runtime-event-loop.md#shutdown)
- [Failure and cleanup](../../../../docs/architecture/terminal-integration.md#failure-fallback-and-cleanup)
- [Exception preservation](../../../../docs/architecture/error-handling.md#exception-preservation)
- [Memory ownership](../../../../docs/architecture/memory-ownership.md#overview)
- [Pseudoterminals](../../../../docs/testing/pseudoterminals.md#overview)

## Code map

- Session and connection: `src/SharpVision.Terminal/Runtime/`
- Transport abstractions: `src/SharpVision.Terminal/Abstractions/`
- Tests: `tests/SharpVision.Terminal.Tests/Runtime/` and `Transport/`
- Application host-lease tests:
  `tests/SharpVision.Tests/Runtime/ApplicationTests.cs`

## Ownership order

Trace the concrete acquisition stack before editing cleanup. Renderer/session VT
state must be restored while the platform lease still permits terminal writes;
raw or console mode restoration follows dependent VT cleanup. Dispose owned
resources once, in reverse acquisition order.

## Workflow

1. Test failure after each acquisition boundary and every cleanup boundary.
2. Cover cancellation during blocked read/write, concurrent stop/dispose,
   partial initialization, resize source failure, and cleanup aggregation.
3. Assert original exception identity plus observable restoration order.
4. Use platform fakes for deterministic order and pseudoterminals for real Unix
   behavior.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Runtime*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Transport*" \
  --minimum-expected-tests 1 --timeout 60s
```
