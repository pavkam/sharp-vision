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

Phase 5A warms representative 80×24 and 200×60 display trees, then measures five
500-iteration layout/render windows. At least one window must allocate exactly
zero managed bytes. A separate 1,000-child Grid/Stack tree measures five
1,000-iteration unchanged-layout windows with the same requirement. Elapsed
times are diagnostic output rather than flaky wall-clock gates.

## Current Phase 3 gates

The warmed unchanged-frame path performs 10,000 measured calls and requires zero
thread-local managed allocation. Sparse and dense encodes reuse the
renderer-owned finite pooled batch; exceeding its configured byte limit must
fail before transport output. A deliberately blocked fake transport proves the
render stays pending without queue growth, while partial write, flush,
cancellation, and synchronized cleanup failures prove that only a complete
write-and-flush commits front state. These guarantees implement the ownership
rules in the
[rendering pipeline](../architecture/rendering-pipeline.md#commit-and-invalidation).

`PhaseThreePerformanceTests` warms and measures representative ASCII, mixed, and
emoji segmentation; unchanged, sparse, and dense 80×24 encoding; and legacy
text, SGR mouse, and Kitty keyboard decoding. Five 10,000-iteration allocation
windows must include a zero-byte sample after tiered compilation has crossed its
warm-up. Test output records elapsed time, .NET runtime, OS, and process
architecture, but elapsed time is intentionally informational on local and
ordinary CI machines.

## Current Phase 4 gates

`InfrastructurePerformanceTests` warms and samples unchanged box layout, reused
80×24 semantic control rendering, and stable depth-20 routed events. The minimum
of five measured windows must allocate zero managed bytes. A separate
1,000-operation dispatcher post/drain run allows at most 256 bytes per post for
the bounded work object and records completion throughput.

Reports include .NET runtime, OS, process architecture, elapsed time, and
iteration count. Only deterministic allocation budgets gate local/ordinary CI;
wall-clock values remain informational.

## Current Phase 5B gates

`InteractivePerformanceTests` renders a representative List, TextInput,
ScrollBar, and `AutoScroll`-enabled Stack tree at 80×24 and 200×60. Five
measured 200-frame windows must include a zero-allocation window after warm-up.
The test process disables tiered compilation so the gate consistently measures
fully optimized steady-state code instead of background JIT promotion timing.

TextInput replacement and captured ScrollBar dragging each run 1,000 public
operations under finite per-operation allocation budgets, and 1,000 nested wheel
commands repeatedly consume deltas through both scrollable ancestors under a
finite routed-command budget. Replacing 1,000 List items must release every
detached generated control, and 1,000 unchanged layout passes must allocate
exactly zero managed bytes. Timings remain diagnostic; allocation and
retained-memory assertions are mandatory.
