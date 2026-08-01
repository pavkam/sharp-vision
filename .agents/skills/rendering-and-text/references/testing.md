# Rendering and Text Testing

## Load this reference when

Changing tests or claiming rendering, Unicode, image, FIGlet, or text-layout
behavior complete.

## Normative documentation

- [Rendering equivalence](../../../../docs/testing/rendering.md#overview)
- [Unicode evidence](../../../../docs/testing/unicode-rendering.md#required-evidence)
- [Randomized testing](../../../../docs/testing/randomized.md#overview)
- [Performance evidence](../../../../docs/testing/performance.md#required-evidence)
- [Control integration](../../../../docs/testing/controls-integration.md#overview)

## Evidence ladder

- Curated semantic cell and grapheme cases.
- Complete A-to-B frame transitions applied to an independent terminal model.
- Randomized incremental-versus-full equivalence with recorded seeds.
- Cross-consumer proof for layout, selection, hit testing, cursor, and
  rendering.
- Exact bytes only where encoder behavior is the contract.
- Allocation and throughput evidence after correctness.

## Focused verification

Choose the narrow namespace from the topic reference. Prevent zero discovery:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Rendering*" \
  --minimum-expected-tests 1 --timeout 60s
```

Every `dotnet test` refinement, including an exact `--filter-class` command,
must retain `--minimum-expected-tests 1`. A narrower command is not allowed to
turn zero discovery into apparent success.

Before completion, run `make format`, `make lint`, `make build`, and
`make test`.
