# Performance testing

## Performance testing

Performance claims require correctness tests first and versioned scenarios.
Metrics include elapsed/CPU time, throughput, allocations, retained/peak memory,
output bytes, transport writes, damage spans, and full-redraw rate.

## Scenarios

- Parser: plain ASCII, control-heavy, fragmented, large bounded OSC/DCS, and
  malformed recovery.
- Geometry: ASCII, mixed CJK, combining text, emoji ZWJ, wrapping, and clipping.
- Rendering: no change, sparse, dense, style-heavy, Unicode-heavy, resize, and
  full invalidation at representative terminal sizes.
- UI: deep/wide trees, grid/percentage layout, text reflow, lists, menus,
  popups, and nested scrolling.

Warm-up, iteration count, architecture, runtime, OS, and terminal size are
recorded. Allocation budgets may gate deterministic tests on pull requests.
Timing regressions gate only stable dedicated/scheduled environments; noisy CI
still publishes comparative results.

Optimization is rejected when it breaks model equivalence, increases unbounded
memory, or improves one synthetic case while materially regressing the common
dense/sparse counterpart without an approved tradeoff.

## Current renderer gates

The warmed unchanged-frame path performs 10,000 measured calls and requires zero
thread-local managed allocation. Sparse and dense encodes reuse the
renderer-owned finite pooled batch; exceeding its configured byte limit must
fail before transport output. A deliberately blocked fake transport proves the
render stays pending without queue growth, while partial write, flush,
cancellation, and synchronized cleanup failures prove that only a complete
write-and-flush commits front state. These guarantees implement the ownership
rules in the
[rendering pipeline](../architecture/rendering-pipeline.md#commit-and-invalidation).
