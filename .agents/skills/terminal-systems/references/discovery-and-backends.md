# Discovery and Backends

## Load this reference when

Changing terminal capabilities, identity, active queries, environment overrides,
terminfo, termcap, multiplexer evidence, backend resolution, or safe fallback.

## Normative documentation

- [Capabilities](../../../../docs/architecture/capabilities.md#overview)
- [Discovery pipeline](../../../../docs/architecture/discovery-pipeline.md#overview)
- [Terminal backends](../../../../docs/architecture/terminal-backends.md#overview)
- [Terminal integration](../../../../docs/architecture/terminal-integration.md#overview)
- [Terminfo](../../../../docs/protocols/terminfo.md#overview)
- [Termcap](../../../../docs/protocols/termcap.md#overview)

## Code map

- Published capabilities: `src/SharpVision.Terminal/Capabilities/`
- Evidence pipeline and active queries: `src/SharpVision.Terminal/Discovery/`
- Terminal-family selection: `src/SharpVision.Terminal/Backends/`
- Description databases: `src/SharpVision.Terminal/Terminfo/`
- Tests: matching `Capabilities/`, `Discovery/`, and `Backends/` test folders

## Ownership model

- `DiscoveryPipeline` combines immutable evidence with explicit provenance.
- `TerminalBackendResolver` chooses terminal-family behavior.
- Graphics capability does not establish Kitty terminal identity.
- Graphics backend selection is a separate authorization and fallback decision.

## Workflow

1. Identify the evidence source and its precedence before changing resolution.
2. Test conflicts among overrides, environment, descriptions, active queries,
   platform evidence, and safe defaults.
3. Bound active-query concurrency, correlation, timeout, and late replies.
4. Keep publication immutable and fallback conservative.
5. Prove multiplexer identity and inner-terminal capability independently.

If the requested evidence classification contradicts the current protocol
contract or tests, identify the exact reply token or status, decide whether it
publishes supported, unsupported, or absent evidence, and reconcile the protocol
page, typed model, and tests together. Do not disguise that contract decision as
a local fallback tweak.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Discovery*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Backends*" \
  --minimum-expected-tests 1 --timeout 60s
```
