# Terminal Services

## Load this reference when

Changing Application.Terminal, bell, title, clipboard, graphics, capability
gating, runtime protocol routing, out-of-band writes, or terminal service
availability.

## Normative documentation

- [Terminal integration](../../../../docs/architecture/terminal-integration.md#terminal-integration-contract)
- [Public terminal API](../../../../docs/architecture/terminal-integration.md#public-api)
- [Protocol routing](../../../../docs/architecture/terminal-integration.md#protocol-routing)
- [Runtime routing](../../../../docs/protocols/runtime-routing.md#runtime-routing-contract)
- [Ordering and ownership](../../../../docs/protocols/runtime-routing.md#ordering-and-ownership)

## Code map

- Public terminal services: `src/SharpVision/Runtime/`
- Session and routing: `src/SharpVision.Terminal/Runtime/`
- Clipboard and protocol implementations: `src/SharpVision.Terminal/Clipboard/`
  and protocol family folders
- Tests: UI `Runtime/TerminalServicesTests.cs`, `ProtocolRoutingTests.cs`, and
  terminal runtime/clipboard tests

## Workflow

1. Separate public service availability, capability authorization, typed
   command, protocol encoding, Session ordering, and renderer-state
   reconciliation.
2. Test unavailable and unsupported environments, concurrent requests,
   cancellation, cleanup, redaction, and disposal.
3. Load `terminal-systems` when grammar or capability policy changes.
4. Load `rendering-and-text` when the write invalidates remembered frame state.

## Project-specific traps

- Controls use `Bell.Ring`, `SetTitle`, and `Clipboard`; they never emit escape
  bytes.
- Clipboard contents and untrusted replies do not enter diagnostics.
- Strict diagnostics cannot change valid output or safe fallback.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*TerminalServicesTests" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ProtocolRoutingTests" \
  --minimum-expected-tests 1 --timeout 60s
```
