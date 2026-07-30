# Terminal-System Testing

## Load this reference when

Changing or reviewing terminal-system tests, selecting focused evidence, or
claiming a protocol, discovery, capability, or backend change complete.

## Normative documentation

- [Correctness model](../../../../docs/testing/correctness-model.md#correctness-model-contract)
- [Terminal protocol evidence](../../../../docs/testing/terminal-protocols.md#required-evidence)
- [Randomized testing](../../../../docs/testing/randomized.md#randomized-testing-contract)
- [Pseudoterminal testing](../../../../docs/testing/pseudoterminals.md#pseudoterminal-testing-contract)
- [Continuous integration](../../../../docs/testing/continuous-integration.md#continuous-integration-contract)

## Evidence ladder

- Exact bytes for every encoder variant and terminator.
- Every split point for representative incremental input.
- Malformed, oversized, unknown, interrupted, and adjacent-input recovery.
- Deterministic conflicting-evidence and timeout tests for discovery.
- Typed routing through the real writer, parser, or backend boundary.
- Pseudoterminal or platform proof for console behavior where supported.

Use xUnit v3, Shouldly, Arrange/Act/Assert, deterministic fakes, recorded random
seeds, and observable output. Do not assert private call graphs.

## Focused verification

Choose the narrow namespace from the topic reference. Every focused command must
use supported prefix/suffix filter grammar and prevent zero discovery:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Discovery*" \
  --minimum-expected-tests 1 --timeout 60s
```

Before completion, run:

```bash
make format
make lint
make build
make test
```
