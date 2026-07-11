# Phase 5A Display Controls and Panels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans`
> to implement this plan task-by-task. Execute directly on `main`; the user
> explicitly forbids additional worktrees. Steps use checkbox (`- [ ]`) syntax
> for tracking.

**Goal:** Ship the concrete Stack, Overlay, Dock, Canvas, Grid, Text, and Border
controls as the tested foundation for every remaining Phase 5 control and the
Showcase.

**Architecture:** Extend the Phase 4 mutable `Control`/`Container` box model
without introducing a second layout or rendering system. Panels measure and
arrange owned children through the existing internal transaction API, text uses
the terminal project's Unicode grapheme and cell-width engine, and every control
draws only semantic cells through the clipped terminal canvas. Every named type
lives in its own exactly named file.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision terminal Unicode
and rendering primitives, Markdown, Mermaid

---

## Normative inputs

- `docs/superpowers/specs/2026-07-11-sharpvision-foundation-design.md`
- `docs/controls/control.md`
- `docs/controls/layout/{stack,overlay,dock,canvas,grid}.md`
- `docs/controls/display/{text,border}.md`
- `docs/concepts/{layout,styling,unicode-cell-geometry,input-routing}.md`
- `docs/testing/{controls-integration,correctness-model,unicode-rendering}.md`

RichText, interactive input controls, scrolling, list/menu/window composition,
and Showcase pages remain in Phase 5B/5C/6 plans. Phase 5A must not add partial
versions of those APIs.

## Task 1: Add panel primitives and child rendering policy

**Files:**

- Create: `src/SharpVision/Layout/Orientation.cs`
- Create: `tests/SharpVision.Tests/Support/ProbeContainer.cs`
- Modify: `tests/SharpVision.Tests/Support/ProbeControl.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `src/SharpVision/Controls/Children.cs`
- Modify:
  `tests/SharpVision.Tests/Controls/{PropertyTests,RenderingTests,TreeTests}.cs`
- Modify: `docs/controls/control.md`

- [x] **Step 1: Split the touched test helper file**

  Move `ProbeContainer` from `ProbeControl.cs` into `ProbeContainer.cs` without
  changing behavior. Run the complete UI test assembly before further edits.

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*TreeTests" "*RenderingTests" --minimum-expected-tests 2 --timeout 60s
  ```

  Expected: existing tests pass and each touched helper file contains one type.

- [x] **Step 2: Write failing child-policy tests**

  Add tests proving:

  - `IsHitTestVisible` defaults true, is dispatcher-affine, and prevents hit
    targeting without changing visibility, focus eligibility, or rendering;
  - a container may opt out of clipping descendants while its own drawing stays
    clipped;
  - a capacity-one `Children` collection rejects a second child before changing
    the first and atomically replaces its only child;
  - invalid replacement preserves parent, dispatcher, focus, and capture.

  Use `MethodName_WhenThis_ThatIsExpected` names and semantic frame assertions.

- [x] **Step 3: Run the focused tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*PropertyTests" "*RenderingTests" "*TreeTests" --minimum-expected-tests 3 --timeout 60s
  ```

  Expected: compile failures for the new child policy API.

- [x] **Step 4: Implement the shared policy minimally**

  Add `Orientation.Vertical` and `Orientation.Horizontal` in its own file. Add
  `Control.IsHitTestVisible` (default true) and make `HitTest` reject false. Add
  an internal virtual `ClipsChildren` policy that defaults true. `Render` must
  clip `RenderCore` to `Bounds` but pass either the clipped or ancestor canvas
  to `RenderChildren` according to that policy.

  Extend `Children` with a validated capacity constructor and atomic internal
  `SetOnly(Control?)`. `Container` keeps its existing unlimited default and
  exposes a protected capacity constructor for Border and later Button,
  ScrollView, Popup, and Window. Validate the complete replacement before any
  detach or attach mutation.

- [x] **Step 5: Verify GREEN, format, and commit**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*PropertyTests" "*RenderingTests" "*TreeTests" --minimum-expected-tests 3 --timeout 60s
  make lint
  git add src/SharpVision/Controls src/SharpVision/Layout/Orientation.cs tests/SharpVision.Tests/Controls tests/SharpVision.Tests/Support docs/controls/control.md
  git commit -m "feat: add panel child policies"
  ```

## Task 2: Implement Stack

**Files:**

- Create: `src/SharpVision/Controls/Stack.cs`
- Create: `tests/SharpVision.Tests/Controls/StackTests.cs`
- Modify: `docs/controls/layout/stack.md`
- Modify: `docs/concepts/layout.md`

- [x] **Step 1: Write failing Stack tests**

  Cover both orientations, default vertical orientation, non-negative `Spacing`,
  `Reverse`, collapsed-child spacing removal, fixed/auto/percent/star mixtures,
  margins, alignment, overflow, zero/tiny bounds, resize, exact bounds, render
  order, and ownership. Assert percentage tracks against final content size and
  require all allocated stack-axis extents plus spacing to stay contained.

- [x] **Step 2: Verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*StackTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because `Stack` does not exist.

- [x] **Step 3: Implement Stack**

  `Stack : Container` exposes `Orientation`, `Spacing`, and `Reverse`. Measure
  visible non-collapsed children intrinsically along the stack axis and against
  the available cross axis. Arrange uses `Tracks.Resolve` over each child's
  stack-axis `Length`, margin-inclusive intrinsic request, and limits, after
  reserving saturated spacing. Reverse changes geometry iteration and render
  iteration together without reparenting children.

  Setters validate before mutation: orientation changes measure; spacing changes
  measure; reverse changes arrange/render. Use pooled or stack storage for small
  track vectors and return cleared rented arrays in `finally`.

- [x] **Step 4: Verify GREEN and commit**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*StackTests" --minimum-expected-tests 1 --timeout 60s
  git add src/SharpVision/Controls/Stack.cs tests/SharpVision.Tests/Controls/StackTests.cs docs/controls/layout/stack.md docs/concepts/layout.md
  git commit -m "feat: add stack panel"
  ```

## Task 3: Implement Overlay

**Files:**

- Create: `src/SharpVision/Controls/Overlay.cs`
- Create: `src/SharpVision/Controls/ZOrder.cs`
- Create: `tests/SharpVision.Tests/Controls/OverlayTests.cs`
- Modify: `docs/controls/layout/overlay.md`

- [x] **Step 1: Write failing Overlay tests**

  Cover maximum desired size, shared final slot, child length/alignment,
  percentage sizing, stable `ZIndex` ties, z-order mutation, render and hit-test
  order, `IsHitTestVisible`, clipping on/off, focus order remaining collection
  order, removal damage, and zero/tiny bounds.

- [x] **Step 2: Verify RED**

  Run `*OverlayTests`; expect compile failure for `Overlay` and its attached
  z-order API.

- [x] **Step 3: Implement Overlay**

  `Overlay : Container` exposes `ClipToBounds` defaulting true and static
  `GetZIndex`/`SetZIndex`. Store attached values in a weak table through the
  one-type `ZOrder` holder. Validate controls and dispatcher affinity before
  mutation. Measure every child with the content constraint and return the
  maximum margin-inclusive request. Arrange every child into the same content
  rectangle. Render/hit-test a stable `(ZIndex, child-order)` ordering without
  changing focus traversal.

- [x] **Step 4: Verify GREEN and commit**

  Run the focused test, then `git diff --check`; commit as
  `feat: add overlay panel`.

## Task 4: Implement Dock

**Files:**

- Create: `src/SharpVision/Layout/Side.cs`
- Create: `src/SharpVision/Controls/Dock.cs`
- Create: `src/SharpVision/Controls/DockPlacement.cs`
- Create: `tests/SharpVision.Tests/Controls/DockTests.cs`
- Modify: `docs/controls/layout/dock.md`

- [x] **Step 1: Write failing Dock tests**

  Cover every side, collection order, `LastChildFills` default true, spacing,
  fixed/percent/auto lengths, collapsed children, over-consumption saturation,
  margins, zero/tiny bounds, resize, clipping, navigation order, and exact
  semantic cells.

- [x] **Step 2: Verify RED**

  Run `*DockTests`; expect compile failure for `Dock`, `Side`, and attached side
  methods.

- [x] **Step 3: Implement Dock**

  `Dock : Container` exposes non-negative `Spacing`, `LastChildFills`, and
  static `GetSide`/`SetSide`. Each measure/arrange iteration uses the saturated
  remaining rectangle; percentages resolve against that iteration's final
  remaining axis. The last visible non-collapsed child fills only when enabled.
  Attached side changes invalidate the owning parent only when it is a Dock.

- [x] **Step 4: Verify GREEN and commit**

  Run the focused test and commit as `feat: add dock panel`.

## Task 5: Implement positioned Canvas

**Files:**

- Create: `src/SharpVision/Controls/Canvas.cs`
- Create: `src/SharpVision/Controls/Position.cs`
- Create: `tests/SharpVision.Tests/Controls/CanvasTests.cs`
- Modify: `docs/controls/layout/canvas.md`

- [x] **Step 1: Write failing Canvas tests**

  Cover left/top/right/bottom independently and in pairs, automatic stretch only
  for opposing offsets, explicit-size precedence, intrinsic union, deferred
  percentages in unbounded measure, final percentage resolution, negative final
  origins caused by oversized right/bottom placement, clipping on/off, hit
  testing, z-order, resize, and Unicode-width children.

- [x] **Step 2: Verify RED**

  Run `*CanvasTests`; expect compile failure for the positioned Canvas API.

- [x] **Step 3: Implement Canvas**

  Store four nullable attached `Length` values in weak `Position` records.
  Reject `Auto` and `Star` offsets before mutation; cells and percentage are
  valid. `Canvas : Container` exposes `ClipToBounds` default true. Unbounded
  percentage offsets contribute zero origin during desired-union measurement and
  resolve only during arrange. Opposing offsets stretch an automatic child; an
  explicit child size keeps its size and uses the leading offset.

- [x] **Step 4: Verify GREEN and commit**

  Run the focused test and commit as `feat: add canvas panel`.

## Task 6: Add immutable Grid tracks and placement

**Files:**

- Create: `src/SharpVision/Layout/Track.cs`
- Create: `src/SharpVision/Layout/TrackCollection.cs`
- Create: `src/SharpVision/Controls/GridPlacement.cs`
- Create: `src/SharpVision/Controls/Grid.cs`
- Create: `tests/SharpVision.Tests/Layout/GridPrimitiveTests.cs`
- Modify: `docs/controls/layout/grid.md`

- [x] **Step 1: Write failing Grid primitive tests**

  Prove `Track` factories and constructor validate length/min/max, collections
  are never null and invalidate their owner once per actual mutation, empty
  definitions imply one automatic track, row/column origins are non-negative,
  spans are positive, and invalid attached changes preserve prior metadata.

- [x] **Step 2: Verify RED**

  Run `*GridPrimitiveTests`; expect compile failure for `Track`,
  `TrackCollection`, and placement methods.

- [x] **Step 3: Implement the primitives**

  `Track` is an immutable record struct with `Length`, `Minimum`, and `Maximum`
  plus `Auto`, `Cells`, `Percent`, and `Star` factories. `TrackCollection`
  implements `IList<Track>` and calls one validated owner invalidation callback.
  `GridPlacement` weakly stores row, column, row span, and column span defaults
  of 0, 0, 1, 1. Create the `Grid` ownership/API shell with non-null Rows and
  Columns, spacing validation, and static placement methods; Task 7 adds its
  layout algorithm. Keep every type in its own file.

- [x] **Step 4: Verify GREEN and commit**

  Run the focused test and commit as `feat: add grid definitions`.

## Task 7: Implement Grid measure and arrange

**Files:**

- Modify: `src/SharpVision/Controls/Grid.cs`
- Create: `tests/SharpVision.Tests/Controls/GridTests.cs`
- Create: `tests/SharpVision.Tests/Controls/RandomizedGridTests.cs`
- Modify: `docs/controls/layout/grid.md`
- Modify: `docs/testing/randomized.md`

- [x] **Step 1: Write failing deterministic Grid tests**

  Cover all track kinds and mixes, spacing, min/max, non-spanning intrinsic
  requests, competing spans, collapsed children, origin/span range validation,
  wrapping remeasure, percentage final-size resolution, remainder rounding,
  zero/tiny overflow, resize, ownership, and exact child cells.

- [x] **Step 2: Verify RED**

  Run `*GridTests`; expect geometry assertions to fail because the Task 6 Grid
  shell has no measure/arrange algorithm.

- [x] **Step 3: Implement Grid**

  Measure fixed and intrinsic non-spanning requirements first, then satisfy
  spans deterministically with `Tracks.Satisfy`, excluding spacing from track
  extents. Arrange subtracts saturated spacing, resolves final tracks with
  `Tracks.Resolve`, computes cumulative origins, and arranges children into the
  union of their spanned tracks plus internal spacing. A changed final span
  constraint remeasures wrapped content before committing bounds.

- [x] **Step 4: Add randomized independent invariants**

  With fixed seed `0x51A475A`, generate at least 10,000 grids containing mixed
  tracks, limits, spans, spacing, visibility, and tiny/final sizes. Resolve each
  twice and assert determinism, containment, non-negative geometry, stable
  shared edges, and exact final-axis consumption whenever a star track can
  absorb the remainder. Log seed and case on failure.

- [x] **Step 5: Verify GREEN and commit**

  Run `*GridTests` and `*RandomizedGridTests`; commit as `feat: add grid panel`.

## Task 8: Add grapheme-safe text layout values

**Files:**

- Create: `src/SharpVision/Text/Wrapping.cs`
- Create: `src/SharpVision/Text/Trimming.cs`
- Create: `src/SharpVision/Text/Alignment.cs`
- Create: `src/SharpVision/Text/Line.cs`
- Create: `src/SharpVision/Text/Layout.cs`
- Create: `tests/SharpVision.Tests/Text/LayoutTests.cs`
- Create: `tests/SharpVision.Tests/Text/RandomizedLayoutTests.cs`
- Modify: `docs/controls/display/text.md`

- [x] **Step 1: Write failing pure text-layout tests**

  Cover empty text, CR/LF/CRLF logical lines, no/word/grapheme wrapping,
  no/clip/grapheme-ellipsis/word-ellipsis trimming, start/center/end alignment,
  tabs with an explicit four-cell stop, combining clusters, variation selectors,
  CJK, emoji ZWJ, invalid UTF-16 replacement, zero/one-cell widths, and resize
  reflow. Assert UTF-16 slice boundaries always coincide with grapheme edges.

- [x] **Step 2: Verify RED**

  Run `SharpVision.Tests.Text.LayoutTests`; expect compile failure for the text
  layout types.

- [x] **Step 3: Implement allocation-conscious layout**

  `Layout` segments only through `Terminal.Unicode.Graphemes` and measures each
  cluster with the shared width policy. It produces immutable `Line` values
  containing source offset/length, cell width, leading cells, and ellipsis flag.
  Cache reusable arrays on the owning Text control later; the pure engine
  accepts caller-owned destination spans and reports required capacity without
  exposing pooled memory.

- [x] **Step 4: Add randomized model checks**

  Use seed `0x7E875A` over at least 5,000 mixed Unicode inputs and widths.
  Compare layout source slices with independent grapheme enumeration; assert
  monotonic consumption, no split cluster, line width containment when width is
  positive, and deterministic results.

- [x] **Step 5: Verify GREEN and commit**

  Run both text-layout test classes and commit as
  `feat: add grapheme-safe text layout`.

## Task 9: Implement Text

**Files:**

- Create: `src/SharpVision/Controls/Text.cs`
- Create: `tests/SharpVision.Tests/Controls/TextTests.cs`
- Modify: `docs/controls/display/text.md`
- Modify: `docs/testing/unicode-rendering.md`

- [x] **Step 1: Write failing Text control tests**

  Cover constructor and non-null `Content`, property defaults and validation,
  measure invalidation for content/wrapping/trimming, arrange invalidation for
  alignment, render-only color/attribute overrides, committed `Lines`, cached
  unchanged layout, inherited style, hidden/collapsed behavior, tiny clipping,
  multiline/wrapped Unicode, ellipsis, and exact frame cells/styles.

- [x] **Step 2: Verify RED**

  Run `*TextTests`; expect compile failure because `Controls.Text` is absent.

- [x] **Step 3: Implement Text**

  `Text : Control` stores a non-null string but performs measurement/rendering
  over borrowed spans. Content defaults empty; `Wrapping.None`, `Trimming.None`,
  text `Alignment.Start`, and null color/attribute overrides are the defaults.
  It exposes those modes, optional foreground/background/attributes, and
  read-only committed `Lines`. Measure/layout caches key on content identity,
  width, modes, and ambiguous width policy. Render draws precomputed
  grapheme-safe line slices plus typed ellipsis cells through `Canvas.Draw`; it
  never emits protocol bytes.

- [x] **Step 4: Verify GREEN, allocation behavior, and commit**

  Warm an unchanged 80-column Unicode Text measure/render loop and require a
  zero-allocation sample across five windows. Run `*TextTests` plus the Unicode
  integration tests; commit as `feat: add text control`.

## Task 10: Implement Border

**Files:**

- Create: `src/SharpVision/Controls/Border.cs`
- Create: `src/SharpVision/Controls/Glyphs.cs`
- Create: `tests/SharpVision.Tests/Controls/BorderTests.cs`
- Modify: `docs/controls/display/border.md`

- [x] **Step 1: Write failing Border tests**

  Cover no child, atomic replacement, invalid already-parented/self/ancestor
  replacement, capacity-one collection enforcement, every zero/one thickness
  edge combination, padding, no/tiny bounds, child measure/arrange, default and
  custom glyphs, invalid empty/control/wide glyphs, border/background styles,
  combined visual states, clipping, Unicode content, resize, disposal, and exact
  corner/edge/interior cells.

- [x] **Step 2: Verify RED**

  Run `*BorderTests`; expect compile failure for `Border` and `Glyphs`.

- [x] **Step 3: Implement Border**

  `Border : Container` uses capacity one and exposes atomic `Child`. Its
  `BorderThickness` accepts only zero or one per edge. Immutable `Glyphs`
  contains top-left/top/top-right/right/bottom-right/bottom/bottom-left/left
  Runes and validates every Rune is a printable narrow one-cell grapheme.
  Measure adds active border edges around the child's margin-inclusive request;
  arrange deflates active edges after base padding. Render fills background and
  draws only complete visible corners/edges through semantic cells.

- [x] **Step 4: Verify GREEN and commit**

  Run `*BorderTests`, then the shared rendering tests; commit as
  `feat: add border control`.

## Task 11: Prove panel/display composition end to end

**Files:**

- Create: `tests/SharpVision.Tests/Integration/DisplayPanelTests.cs`
- Create: `tests/SharpVision.Tests/Performance/DisplayPanelPerformanceTests.cs`
- Modify: `docs/testing/{controls-integration,performance}.md`

- [x] **Step 1: Write cross-layer composition tests**

  Compose Grid, Stack, Overlay, Dock, Canvas, Border, and Text under a real
  `Application` with `FakeTerminal`. Drive multiple resizes and style changes,
  then assert committed bounds, wide-cell ownership, final semantic frame,
  incremental output bytes, and absence of stale cells after removal/movement.

- [x] **Step 2: Add performance gates**

  Measure warmed unchanged layout/render for representative 80x24 and 200x60
  trees plus a 1,000-child Grid/Stack tree. Gate deterministic allocations and
  retained memory; report elapsed time without flaky wall-clock thresholds.

- [x] **Step 3: Run the Phase 5A focused suite**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --filter-class "*StackTests" "*OverlayTests" "*DockTests" "*CanvasTests" "*GridTests" "*TextTests" "*BorderTests" "*DisplayPanelTests" "*DisplayPanelPerformanceTests" --minimum-expected-tests 9 --timeout 120s
  ```

  Expected: all deterministic, randomized, cross-layer, and allocation proofs
  pass.

- [x] **Step 4: Commit**

  Commit as `test: prove display and panel composition`.

## Task 12: Publish and verify Phase 5A

**Files:**

- Modify:
  `docs/architecture/{project-structure,memory-ownership,rendering-pipeline}.md`
- Modify: `docs/concepts/{layout,styling,unicode-cell-geometry}.md`
- Modify: `docs/controls/index.md`
- Modify: `docs/superpowers/plans/2026-07-11-phase-5a-display-panels.md`

- [ ] **Step 1: Audit documentation against exact public API**

  Record every default, unit, validation rule, exception, ownership boundary,
  invalidation phase, clipping/z-order rule, Unicode behavior, and example.
  Remove future-tense claims for shipped Phase 5A controls and keep later
  controls explicitly assigned to their later plans.

- [ ] **Step 2: Audit one-type-per-file compliance for changed files**

  ```bash
  rg -n "^(public|internal|private|protected).*\\b(class|struct|record|interface|enum|delegate)\\b" src/SharpVision tests/SharpVision.Tests
  ```

  Expected: every non-generated named type added or touched by this plan is the
  only type in its exactly named file; no nested named types were introduced.

- [ ] **Step 3: Run all quality gates**

  ```bash
  make format
  npm run check:unicode
  make lint
  make build
  make test
  git diff --check
  rg -n "TODO|TBD|NotImplementedException" src tests docs scripts
  ```

  Expected: all six projects build with zero warnings/errors; every test
  assembly passes with non-zero discovery; formatting, analyzers, docs links,
  Unicode generation, and placeholder audits are clean.

- [ ] **Step 4: Commit the verified slice**

  ```bash
  git add docs
  git commit -m "chore: complete display and panel controls"
  ```

## Self-review record

- **Spec coverage:** Tasks cover every Text, Border, Stack, Overlay, Dock,
  Canvas, and Grid requirement, including defaults, ownership, validation,
  Unicode, clipping, z-order, resize, tiny bounds, exact cells, randomized
  layout, integration, and allocation evidence.
- **Dependency order:** Shared child policy precedes panels; simple panels
  precede Grid; pure text layout precedes Text; Text and capacity-one ownership
  precede Border and later interactive content controls.
- **Type consistency:** Canonical new names are `Orientation`, `Side`, `Track`,
  `TrackCollection`, `Stack`, `Overlay`, `Dock`, `Canvas`, `Grid`, `Wrapping`,
  `Trimming`, text `Alignment`, `Line`, `Layout`, `Text`, `Glyphs`, and
  `Border`. Every named type has one file.
- **Scope:** RichText and interactive/scrolling/window behavior remain fully in
  later plans; Phase 5A does not ship misleading partial facades.
