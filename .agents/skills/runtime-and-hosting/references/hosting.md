# Hosting

## Load this reference when

Changing ConsoleApplication, ConsoleApplicationBuilder, ConsoleRunOptions,
ConsoleHost, terminal-description preflight, TreatControlCAsInput, startup, or
portable host selection.

## Normative documentation

- [Hosting](../../../../docs/concepts/hosting.md#overview)
- [Entry points](../../../../docs/concepts/hosting.md#entry-points)
- [ConsoleRunOptions](../../../../docs/concepts/hosting.md#consolerunoptions)
- [Portable console host](../../../../docs/concepts/hosting.md#portable-console-host)
- [Terminal startup](../../../../docs/architecture/terminal-integration.md#startup-sequence)

## Code map

- Public application host: `src/SharpVision/Runtime/ConsoleApplication.cs`
- Builder and options: `src/SharpVision/Runtime/`
- Portable terminal host: `src/SharpVision.Terminal/Runtime/ConsoleHost.cs`
- Tests: UI `tests/SharpVision.Tests/Runtime/ConsoleApplication*`; terminal
  `tests/SharpVision.Terminal.Tests/Runtime/ConsoleHost*`

## Workflow

1. Start from the public builder and trace configuration to host acquisition,
   discovery, Session, Application, run, and disposal.
2. Test argument validation before acquisition, startup failures at every owned
   boundary, Ctrl+C modes, cancellation, exit status, and cleanup.
3. Keep portable behavior equivalent across Unix and Windows while testing
   platform mechanisms separately.

## Project-specific traps

- Interactive applications use `ConsoleApplication.CreateBuilder` and
  `RunAsync`; they do not hand-wire `ConsoleHost`, transport, and terminal
  options.
- A host that enables `TreatControlCAsInput` owns its exit path.
- Preflight description evidence must be immutable when published to discovery.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ConsoleApplicationTests" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-class "*ConsoleHostTests" \
  --minimum-expected-tests 1 --timeout 60s
```
