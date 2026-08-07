# Performance testing

## Overview

A performance claim needs correctness tests for the behavior being measured
before it can gate anything, plus a versioned scenario describing the
measurement. The tracked metrics include elapsed and CPU time, throughput,
allocations, retained and peak memory, output bytes, transport writes, damage
spans, and the full-redraw rate.

## Scenarios

- Parser: plain ASCII, control-heavy, fragmented, large bounded OSC/DCS, and
  malformed recovery.
- Geometry: ASCII, mixed CJK, combining text, emoji ZWJ, wrapping, and clipping.
- Rendering: no change, sparse, dense, style-heavy, Unicode-heavy, resize, and
  full invalidation at representative terminal sizes.
- UI: deep/wide trees, grid/percentage layout, text reflow, lists, menus,
  popups, and nested scrolling.

Every measurement records its warm-up, iteration count, architecture, runtime,
OS, and terminal size. Allocation budgets may gate deterministic tests on pull
requests. Wall-clock timing is currently informational everywhere: no dedicated
or scheduled benchmark environment exists yet, so no workflow gates on elapsed
time or publishes comparative timing results.

An optimization is rejected when it breaks model equivalence, when it grows
memory without bound, or when it improves one synthetic case while materially
regressing the common dense or sparse counterpart without an approved tradeoff.

The display-tree scenario warms representative 80×24 and 200×60 trees, then
measures five 500-iteration layout/render windows, at least one of which must
allocate exactly zero managed bytes. A separate 1,000-child Grid/Stack tree
measures five 1,000-iteration unchanged-layout windows with the same
requirement. Elapsed times are diagnostic output, not flaky wall-clock gates.

## Renderer and protocol gates

The warmed unchanged-frame path performs 10,000 measured calls and must show
zero thread-local managed allocation. Sparse and dense encodes reuse the
renderer-owned finite pooled batch, and exceeding its configured byte limit must
fail before any transport output. A deliberately blocked fake transport proves
the render stays pending without queue growth, while partial writes, flush
failures, cancellation, and synchronized-cleanup failures prove that only a
complete write-and-flush commits front state. These guarantees implement the
ownership rules in the
[rendering pipeline](../architecture/rendering-pipeline.md#commit-and-terminal-state-invalidation).

The renderer and protocol performance suite warms and measures representative
ASCII, mixed, and emoji segmentation; unchanged, sparse, and dense 80×24
encoding; and legacy text, SGR mouse, and Kitty keyboard decoding. Of five
10,000-iteration allocation windows, at least one must sample zero bytes after
warm-up (both test projects disable tiered compilation, so warm-up covers
one-time initialization rather than JIT promotion). The allocation class lives
in a non-parallel test collection so unrelated terminal tests cannot pollute its
thread-local measurements. Test output records elapsed time, .NET runtime, OS,
and process architecture, but elapsed time is intentionally informational on
local and ordinary CI machines.

## UI infrastructure gates

`InfrastructurePerformanceTests` warms and samples unchanged box layout, reused
80×24 semantic control rendering, and stable depth-20 routed events. The minimum
of its five measured windows must allocate zero managed bytes. A separate
1,000-operation dispatcher post/drain run allows at most 256 bytes per post for
the bounded work object and records completion throughput.

Reports include the .NET runtime, OS, process architecture, elapsed time, and
iteration count. Only the deterministic allocation budgets gate local and
ordinary CI runs; wall-clock values remain informational.

## Interactive control gates

`InteractivePerformanceTests` renders a representative ListView, TextInput,
ScrollBar, and intrinsically scrollable Stack tree at 80×24 and 200×60. Of five
measured 200-frame windows, at least one must be allocation-free after warm-up.
Both test projects disable tiered compilation, so the gate consistently measures
fully optimized steady-state code instead of background JIT promotion timing.

TextInput replacement and captured ScrollBar dragging each run 1,000 public
operations under finite per-operation allocation budgets, and 1,000 nested wheel
commands repeatedly consume deltas through both scrollable ancestors under a
finite routed-command budget. Replacing 1,000 ListView items must release every
detached generated control, and 1,000 unchanged layout passes must allocate
exactly zero managed bytes. Timings remain diagnostic; allocation and
retained-memory assertions are mandatory.

## Required evidence

| Concern    | Required observation                                                          |
| ---------- | ----------------------------------------------------------------------------- |
| Allocation | Warmed windows include the specified zero- or bounded-allocation result.      |
| Retention  | Detached controls, pools, frames, routes, and graphics state are released.    |
| Bounds     | Hostile payload and geometry cases stay within configured memory/work limits. |
| Timing     | Runtime, OS, architecture, iterations, and elapsed time are reported.         |

Allocation and retained-memory assertions gate ordinary CI. Wall-clock
thresholds would require a dedicated benchmark environment, which does not exist
yet.

Geometry bounds are the one wall-clock family that does gate ordinary CI,
because its failure mode is a frozen render thread rather than a slow one.
`Canvas` primitives reject geometry disjoint from the clip in constant time,
clip axis-aligned spans before iterating, and fast-forward Bresenham traversal
to the first visible step while preserving its exact error and tie behavior.
Ellipse intermediates use `Int128` because the cubic products overflow `Int64`
at roughly 1.3 million cells per axis, and a wrapped error term can stop the
horizontal bounds from advancing at all. Regression tests assert that extreme
coordinates, maximum radii, and past-overflow bounds all complete inside a
budget that a per-coordinate loop cannot meet.
