# Control Surface Phase Three Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add intended-behavior mounted surface suites for Stack, Grid, Dock,
Overlay, Canvas, and Table, then implement and cover the catalogued GroupBox and
Expander controls with matching documentation and showcase proof.

**Architecture:** Existing layout controls remain retained mutable objects and
are mounted beneath the real `Application`. Surface fixtures assert committed
public bounds, exact semantic cells, clipping, stale-cell removal, resize
reflow, and real pointer hit targets. GroupBox derives from `ContentControl` and
owns no wrapper panel. Expander derives from `ContentControl`, owns one retained
private header toggle, and includes caller content in measure, arrangement,
rendering, and hit testing only while expanded.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, `ComponentSurface`, retained
controls, semantic terminal cells, intrinsic border glyphs, routed input, and
the SharpVision showcase/gallery contract.

---

## File structure

- Create `tests/SharpVision.Tests/Controls/StackSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/GridSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/DockSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/OverlaySurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs`.
- Create `tests/SharpVision.Tests/Controls/TableSurfaceTests.cs`.
- Create `src/SharpVision/Controls/GroupBox.cs` and its unit/surface tests.
- Create `src/SharpVision/Controls/Expander.cs` and its unit/surface tests.
- Create matching GroupBox and Expander showcase panes and screen tests.
- Update each affected control specification and the mounted-surface testing
  contract.

### Task 1: Cover Stack and Grid through mounted geometry

**Files:**

- Create: `tests/SharpVision.Tests/Controls/StackSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/GridSurfaceTests.cs`
- Modify: `docs/controls/layout/stack.md`
- Modify: `docs/controls/layout/grid.md`

- [x] **Step 1: Write Stack intended-behavior scenarios**

Mount vertical and horizontal stacks with text and button leaves. Prove spacing,
fixed/automatic/proportional allocation, exact child bounds and cells, Unicode
width, pointer hit targets, overflow clipping, intrinsic `AutoScroll`, and
resize reflow with stale-cell removal.

- [x] **Step 2: Verify Stack RED and make only demonstrated corrections**

Run `*StackSurfaceTests`. Keep every regression that exposes allocation,
scrolling, clipping, hit-testing, or invalidation behavior. Do not change a sane
expected layout merely to preserve existing unit output.

- [x] **Step 3: Write Grid intended-behavior scenarios**

Mount fixed, automatic, and proportional rows/columns with a spanning child.
Prove deterministic remainder assignment, padding, exact bounds/cells, resize,
overflow clipping, and real pointer routing to the final arranged cells.

- [x] **Step 4: Verify Grid RED and make only demonstrated corrections**

Run `*GridSurfaceTests`, `*GridTests`, `*GridPrimitiveTests`, and
`*RandomizedGridTests`. Preserve fixed-seed geometry invariants.

- [x] **Step 5: Link proofs and commit**

Update both control test obligations and commit as
`test: cover stack and grid on mounted surfaces`.

### Task 2: Cover Dock, Overlay, and Canvas ordering

**Files:**

- Create: `tests/SharpVision.Tests/Controls/DockSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/OverlaySurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs`
- Modify: `docs/controls/layout/dock.md`
- Modify: `docs/controls/layout/overlay.md`
- Modify: `docs/controls/layout/canvas.md`

- [ ] **Step 1: Write Dock edge/fill scenarios**

Prove stable top/bottom/left/right consumption, fill behavior, exact remaining
space, tiny bounds, resize, clipping, and pointer activation at each committed
edge.

- [ ] **Step 2: Write Overlay precedence scenarios**

Prove common-slot arrangement, later-child visual and hit-test precedence,
hidden-child reveal after public mutation, clipping, resize, and removal of old
top-layer cells.

- [ ] **Step 3: Write Canvas coordinate scenarios**

Prove cell and percentage positions, negative/overflow clipping, z-order, resize
repositioning, Unicode cells, and pointer routing using final arranged bounds.

- [ ] **Step 4: Run focused suites and correct only observable defects**

Run the three new surface fixtures with `*DockTests`, `*OverlayTests`, and
`*CanvasTests`. Retain one mounted regression for every production correction.

- [ ] **Step 5: Link proofs and commit**

Update the three specifications and commit as
`test: cover ordered layout controls on mounted surfaces`.

### Task 3: Cover Table data, geometry, and scrolling

**Files:**

- Create: `tests/SharpVision.Tests/Controls/TableSurfaceTests.cs`
- Modify: `docs/controls/layout/table.md`

- [ ] **Step 1: Write exact table appearance and mutation scenarios**

Mount headers and rows containing ASCII, combining, and wide cells across
automatic, fixed, and proportional columns. Assert exact cells and committed
column/row geometry.

- [ ] **Step 2: Add scrolling, resize, and stale-cell scenarios**

Drive real wheel reports through overflowing vertical and horizontal axes,
remove and replace rows through public collections, resize the same surface, and
prove clipped or removed cells do not survive.

- [ ] **Step 3: Verify RED and correct only demonstrated table defects**

Run `*TableSurfaceTests`, `*TableTests`, and the relevant scrolling fixtures.
Keep semantic row/column state as the primary oracle and screen cells as the
cross-layer proof.

- [ ] **Step 4: Link proof and commit**

Update the Table test obligations and commit as
`test: cover tables on mounted surfaces`.

### Task 4: Implement and prove GroupBox

**Files:**

- Create: `src/SharpVision/Controls/GroupBox.cs`
- Create: `tests/SharpVision.Tests/Controls/GroupBoxTests.cs`
- Create: `tests/SharpVision.Tests/Controls/GroupBoxSurfaceTests.cs`
- Create: `src/SharpVision.Showcase/Panes/GroupBoxPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/GroupBoxPaneTests.cs`
- Modify: `docs/controls/layout/group-box.md`
- Modify: gallery/catalog registration and showcase section expectations.

- [ ] **Step 1: Write public contract and mounted RED tests**

Specify non-null `Header`, one `Glyphs` family, caller-replaceable `Content`,
one-cell frame reservation, header interruption, content style inheritance,
wide-header measurement, replacement, and zero/tiny clipping. Watch tests fail
because `GroupBox` does not exist.

- [ ] **Step 2: Implement the smallest retained ContentControl**

Derive directly from `ContentControl`. Measure header cells plus frame insets,
arrange content inside the frame, render intrinsic terminal-safe border lines
with a clipped header interruption, validate public assignments before state
changes, and preserve Unicode cell ownership.

- [ ] **Step 3: Verify unit and mounted behavior GREEN**

Run `*GroupBoxTests`, `*GroupBoxSurfaceTests`, content ownership fixtures, and
ambiguous-width control tests.

- [ ] **Step 4: Add showcase and normative proof**

Add one gallery pane demonstrating empty, Unicode-header, styled, nested, and
tiny GroupBoxes. Add a representative showcase screen test and update the
catalog and control spec.

- [ ] **Step 5: Commit GroupBox slice**

Commit as `feat: add group box control`.

### Task 5: Implement and prove Expander

**Files:**

- Create: `src/SharpVision/Controls/Expander.cs`
- Create: `tests/SharpVision.Tests/Controls/ExpanderTests.cs`
- Create: `tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs`
- Create: `src/SharpVision.Showcase/Panes/ExpanderPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/ExpanderPaneTests.cs`
- Modify: `docs/controls/layout/expander.md`
- Modify: gallery/catalog registration and showcase section expectations.

- [ ] **Step 1: Write expanded/collapsed and activation RED tests**

Specify the retained header row, exact expanded/collapsed glyph and text cells,
content exclusion from collapsed measure/hit testing, pointer and Space/Enter
parity, focus, event order, disabled refusal, content replacement, resize, and
zero/tiny behavior. Watch tests fail because `Expander` does not exist.

- [ ] **Step 2: Implement retained composition without rebuilding**

Derive from `ContentControl`, create the private retained header toggle in the
constructor, and keep caller content ownership stable. `IsExpanded` updates
measure/render/hit-test visibility atomically and raises `ExpandedChanged` only
after a changed commit. Do not introduce `Build()`, virtual trees, or hooks.

- [ ] **Step 3: Verify unit and mounted behavior GREEN**

Run `*ExpanderTests`, `*ExpanderSurfaceTests`, content ownership, focus,
capture, and routing fixtures.

- [ ] **Step 4: Add showcase and normative proof**

Add one gallery pane with expanded, collapsed, nested, disabled, Unicode, and
content-replacement examples plus a representative screen test. Update catalog
registration and the control specification.

- [ ] **Step 5: Commit Expander slice**

Commit as `feat: add expander control`.

### Task 6: Close phase three

**Files:**

- Modify: `docs/testing/controls-integration.md`
- Modify: this plan.

- [ ] **Step 1: Audit every layout scenario against named evidence**

Map the umbrella design to named mounted or retained unit responsibilities. Add
any missing geometry, clipping, hit-test, resize, Unicode, disabled, tiny,
mutation, or stale-cell proof.

- [ ] **Step 2: Run repository quality gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings/errors, docs and links pass, every discovered test
passes, and isolated package consumption succeeds.

- [ ] **Step 3: Commit phase documentation**

Commit as `docs: complete third control surface coverage phase`.
