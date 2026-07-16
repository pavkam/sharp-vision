# Control Surface Coverage Phase Four Implementation Plan

> **Execution note:** Follow the repository TDD workflow and complete each task
> as a verified commit.

**Goal:** Add intended-behavior mounted coverage for `List`, then implement and
prove the catalogued `TabControl` and `TabItem` with typed retained ownership,
accessible selection, repair, scrolling headers, documentation, and showcase
parity.

**Architecture:** `List` keeps its existing fully realized private scrolling
`Stack`; surface tests drive only real keyboard, pointer, wheel, resize, and
public item replacement paths. `TabControl` derives from `ItemsControl`, owns
one private vertical presentation host, and exposes a typed `TabItems`
collection. Every `TabItem` remains the semantic owned item and uses retained
framework parts for its pressable header and caller-replaceable content.
Selection changes update header state and content participation without
rebuilding pages.

**Evidence:** Exact semantic screens are paired with public selection, active
index, focus, ownership, event, offset, and wide-cell assertions. Pure
collection validation and index-repair algorithms stay in unit tests;
cross-layer input, stale-cell clearing, overflow, and resize live on mounted
surfaces.

---

## Task 1: Prove List through mounted user paths

**Files:**

- Create: `tests/SharpVision.Tests/Controls/ListSurfaceTests.cs`
- Modify only when a red test demonstrates a responsible defect:
  `src/SharpVision/Controls/List.cs` or shared control infrastructure.

- [x] **Step 1: Specify exact initial, pointer, and keyboard selection**

Mount Unicode rows with selected-state styles. Prove initial empty selection,
Tab focus entry, Down/Up/Home/End movement, Space selection, Enter invocation,
primary-click parity, exact selected cells, event cause/order, and wide-cell
continuation ownership.

- [x] **Step 2: Specify modifiers and disabled skipping**

Prove Multiple-mode Control toggle and Shift range selection, disabled/hidden
template skipping, focus movement, and no-op input without direct router or
focus calls. Add only the missing encoded key notation needed by these
scenarios.

- [x] **Step 3: Specify scrolling, resize, and replacement repair**

Prove Page keys and wheel/bring-into-view offsets, resize clamping, item removal
selection/active repair, template/content mutation, variable-height clipping,
and complete stale-row clearing.

- [x] **Step 4: Verify List slice and commit**

Run `*ListTests`, `*ListSurfaceTests`, focus, routing, scrolling, and List
showcase fixtures. Commit as `test: cover list component surfaces`.

## Task 2: Clarify TabControl retained behavior with RED unit tests

**Files:**

- Modify: `docs/controls/collections/tab-control.md`
- Create: `src/SharpVision/Controls/TabControl.cs`
- Create: `src/SharpVision/Controls/TabItem.cs`
- Create: `src/SharpVision/Controls/TabItems.cs`
- Create: `tests/SharpVision.Tests/Controls/TabControlTests.cs`
- Create: `tests/SharpVision.Tests/Controls/TabItemTests.cs`

- [x] **Step 1: Specify typed collection ownership and validation**

Prove Add/Insert/replace/Remove/Clear, null/duplicate/attached/disposed/cycle
rejection before mutation, one parent per tab, caller content ownership, and
detachment without accidental disposal.

- [x] **Step 2: Specify deterministic selection and repair**

The first eligible tab auto-selects. `SelectedIndex` validates `-1` or a
contained eligible tab. Removing/replacing/disabling/hiding the selected tab
chooses the nearest eligible successor, then predecessor, or `-1`. Insertion
before the selected identity preserves identity. `SelectionChanged` fires once
after committed header/content state and includes no speculative transitions.

- [x] **Step 3: Specify retained composition and geometry**

Each item owns one retained pressable header framework part and its ordinary
caller content. The private header strip occupies one row, a separator occupies
one row when height permits, and only selected content participates below it.
Headers use terminal-cell measurement and never rebuild on selection or resize.

- [x] **Step 4: Watch focused tests fail for missing types**

Run `*TabControlTests` and `*TabItemTests`; record the expected compilation
failure before implementation.

## Task 3: Implement TabControl and mounted behavior

**Files:**

- Modify the Tab files from Task 2.
- Create: `tests/SharpVision.Tests/Controls/TabControlSurfaceTests.cs`

- [x] **Step 1: Implement the smallest retained typed control**

Use `ItemsControl` semantic helpers and a private retained presentation host.
Keep public pages as the owned item controls, route header activation back to
their owner, and make selected content layout/render/hit-test/navigation
participation atomic. Validate public arguments before observable mutation.

- [x] **Step 2: Prove pointer and keyboard parity**

Drive pointer clicks plus Left/Right/Home/End navigation with disabled skipping
and wrapping. Assert selected identity, focus, event order, combined visual
state, exact header/divider/separator/content cells, and Unicode continuation
ownership.

- [x] **Step 3: Prove removal, replacement, overflow, and resize**

Remove and replace the selected page, replace selected content, resize tiny and
wide surfaces, and navigate an overflowing header strip. Assert deterministic
repair, header offset/reveal, clipped complete graphemes, and clearing of the
previous page and obsolete headers.

- [x] **Step 4: Run focused control and infrastructure checks**

Run Tab unit/surface tests plus `ItemsControlTests`, `ContentControlTests`,
focus, capture, input-routing, layout, rendering, and Unicode geometry fixtures.

## Task 4: Add Tab showcase and normative proof

**Files:**

- Create: `src/SharpVision.Showcase/Panes/TabControlPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/TabControlPaneTests.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: gallery and showcase section expectations.
- Modify: `docs/controls/collections/tab-control.md`
- Modify: `docs/testing/controls-integration.md`

- [x] **Step 1: Add representative showcase specimens**

Show basic selection, disabled tabs, Unicode headers, content replacement,
overflow, and tiny/resized geometry using ordinary public API.

- [x] **Step 2: Add representative showcase screen proof**

Assert catalog registration, exact selected/unselected headers, separator,
selected content, wide continuation, and disabled availability.

- [x] **Step 3: Link named evidence and audit the collections card**

Update the normative contract and mounted-testing guide with named surface and
unit responsibilities. Add any missing selection, focus, mutation, tiny,
Unicode, overflow, resize, or stale-cell regression found by the audit.

- [x] **Step 4: Run repository quality gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings/errors, all discovered tests pass, documentation and
links pass, and isolated package consumption succeeds.

- [x] **Step 5: Commit phase four**

Commit as `feat: add tab control and complete collection surfaces`.
