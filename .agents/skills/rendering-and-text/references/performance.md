# Rendering Performance

## Load this reference when

Changing renderer, Unicode, text, image, allocation, throughput, byte-count,
write-count, pooling, or benchmark behavior.

## Normative documentation

- [Performance contract](../../../../docs/testing/performance.md#overview)
- [Renderer gates](../../../../docs/testing/performance.md#renderer-and-protocol-gates)
- [Memory ownership](../../../../docs/architecture/memory-ownership.md#overview)
- [Randomized rendering](../../../../docs/testing/rendering.md#randomized-transitions)

## Workflow

1. Establish semantic equivalence before measuring speed or bytes.
2. Use deterministic workloads, warmup, allocation counters, and versioned
   budgets rather than wall-clock assertions in ordinary tests.
3. Measure scanning, changed-cell density, write count, encoded bytes,
   throughput, and retained memory separately.
4. Cover steady state and adversarial full-invalidation transitions.
5. Keep pooled ownership explicit and prove cancellation and exception cleanup.

## Project-specific traps

- Fewer bytes are not an optimization if cursor or style state becomes unknown.
- Avoid per-cell delegates, boxing, strings, or collection growth in hot loops.
- Performance evidence supplements correctness gates; it never replaces them.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Performance*" \
  --minimum-expected-tests 1 --timeout 60s
```
