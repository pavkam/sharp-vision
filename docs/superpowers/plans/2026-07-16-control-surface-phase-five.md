# Control Surface Coverage Phase Five Implementation Plan

> **Execution note:** Reconcile the user-owned NavigationView slice from commit
> `d0bc8e8`; do not cherry-pick that broad mixed commit or touch the unrelated
> dirty main checkout.

**Goal:** Preserve and adapt the existing NavigationView design, then prove
header, main, footer, group, separator, selection, navigation, scrolling,
mutation, Unicode, and resize behavior through mounted surfaces.

**Architecture:** `NavigationView` remains a traditional retained
`CompositeControl` with a private Dock root, optional header, scrollable main
Stack, and pinned footer Stack. Typed entries remain `NavigationViewItem`,
`NavigationViewGroup`, and `NavigationViewSeparator`. Reconciliation updates the
older slice only where current ownership, dispatcher, focus, styling, Unicode,
input, and repair contracts require it.

**Evidence:** Existing unit tests from the user slice are restored first. New
mounted tests use raw terminal pointer and keyboard input, dispatcher-safe
public mutation, resize, exact semantic screens, public selection/group/offset
state, and wide-cell assertions. Every discovered mismatch is fixed in the
responsible retained control rather than hidden in expectations.

## Task 1: Restore and reconcile the user-owned baseline

**Files:**

- Create from `d0bc8e8`: NavigationView production types, unit tests, normative
  spec, and showcase pane.
- Modify integration points only: gallery catalog and showcase expectations.

- [x] **Step 1: Port only the eight NavigationView files**

Use the committed user slice as provenance. Do not import its older GroupBox,
Expander, TabControl, layout, focus, menu, showcase-shell, or Snake changes,
which have independent newer implementations on this branch.

- [x] **Step 2: Adapt mechanical current-architecture differences**

Preserve public names and semantic shape while satisfying current one-type/file,
XML documentation, retained ownership, intrinsic chrome, style scope, Unicode,
and dispatcher rules. Record any behavior change in the normative spec.

- [x] **Step 3: Run restored focused tests**

Run `*NavigationViewTests`, ownership, CompositeControl, focus, Pressable,
Stack, and scrolling fixtures. Fix only demonstrated compatibility defects.

## Task 2: Specify mounted NavigationView behavior

**Files:**

- Create: `tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs`
- Modify production files only after a visible red scenario.

- [ ] **Step 1: Prove header, main, footer, and exact cells**

Mount a bordered view with Unicode header/glyphs, main items, groups,
separators, and pinned footer. Assert committed bounds, exact rows, continuation
ownership, initial focus policy, and separator non-interactivity.

- [ ] **Step 2: Prove pointer and keyboard selection**

Drive Tab, pointer click, Up/Down/Home/End, Enter/Space, wrapping policy,
disabled/hidden skipping, main-to-footer traversal, focus, event order, selected
style, and correct bring-into-view owner.

- [ ] **Step 3: Prove group expansion and repair**

Toggle groups by pointer and keyboard. Collapse, remove, disable, hide, and
clear the selected item or its containing group; require deterministic nearest
eligible selection/focus repair without stale tab stops or capture.

- [ ] **Step 4: Prove scrolling, mutation, and resize**

Overflow the main viewport, navigate and wheel it, mutate entries, resize tiny
and wide surfaces, and assert offset clamping, footer pinning, Unicode geometry,
reflow, and complete stale-cell clearing.

## Task 3: Complete public proof and repository verification

**Files:**

- Modify: `docs/controls/menus/navigation-view.md`
- Modify: `docs/testing/controls-integration.md`
- Modify: NavigationView showcase pane and screen tests.
- Modify: umbrella coverage design status if its old uncommitted-work note is
  stale.

- [ ] **Step 1: Add representative showcase screen proof**

Cover basic selection, groups, disabled items, pinned footer, Unicode, overflow,
and mutation with exact representative cells.

- [ ] **Step 2: Audit the navigation scenario card**

Map every behavior to a named mounted or unit responsibility and add any missing
validation, ownership, event, tiny, Unicode, mutation, scrolling, or stale-cell
regression.

- [ ] **Step 3: Run repository quality gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings/errors, all discovered tests pass, documentation and
links pass, and isolated package consumption succeeds.

- [ ] **Step 4: Commit the reconciled phase**

Commit as `feat: reconcile and cover navigation view`.
