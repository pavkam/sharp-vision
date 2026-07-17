# Menu Interaction and Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make menus compact, aligned, hover-responsive, keyboard complete, and
visually connected to correctly placed submenus.

**Architecture:** Keep generic popup lifecycle unchanged. Put selection and
sibling-switch policy in `Menu`, retained popup presentation and focus
restoration in `MenuItem`, cross-axis rule geometry in `MenuSeparator`, and the
non-focusable menu-face hover palette in `ControlAppearanceDefaults`.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, SharpVision mounted
component surfaces.

---

## Tasks

### Task 1: Prove compact shared-width menu geometry

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/MenuTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuItemShortcutTests.cs`
- Modify: `src/SharpVision/Controls/Menu.cs`
- Modify: `src/SharpVision/Controls/MenuItem.cs`
- Modify: `src/SharpVision/Controls/MenuSeparator.cs`

- [ ] Add failing tests for zero default spacing, stretched item bounds, aligned
      ASCII and wide-Unicode shortcut cells, and a separator spanning the full
      menu width.
- [ ] Run the two focused test classes and confirm failures describe the current
      one-cell gaps, content-sized rows, UTF-16 shortcut math, and three-cell
      rule.
- [ ] Change the default spacing to zero, make menu faces stretch on the cross
      axis, and use Unicode cell width for shortcut geometry.
- [ ] Rerun the focused tests and keep every exact-cell assertion green.

### Task 2: Prove menu-owned hover and keyboard behavior

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/MenuTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs`
- Modify: `src/SharpVision/Controls/Menu.cs`
- Modify: `src/SharpVision/Styling/ControlAppearanceDefaults.cs`

- [ ] Add failing unit and mounted-surface tests for pointer selection, visible
      hover fill, unavailable-item exclusion, Tab/Shift+Tab wrapping, and
      Enter/Space activation of the selected private face.
- [ ] Run the focused tests and confirm failure occurs because pointer motion is
      not menu policy, non-focusable item hover is suppressed, and Tab escapes.
- [ ] Add menu pointer-selection and key handling while preserving one focus
      stop, and add the explicit `MenuItem` hover appearance.
- [ ] Rerun both classes and verify public state plus final surface cells.

### Task 3: Prove submenu placement, switching, and focus restoration

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/MenuTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs`
- Modify: `src/SharpVision/Controls/Menu.cs`
- Modify: `src/SharpVision/Controls/MenuItem.cs`

- [ ] Add failing tests proving horizontal submenus open below, vertical nested
      submenus open right, an armed menu switches on hover and keyboard
      selection, a command target closes the prior submenu, and Escape restores
      owner focus.
- [ ] Run focused tests and confirm the current click-only, always-below,
      standalone popup behavior fails the assertions.
- [ ] Expose only internal submenu operations from `MenuItem`; let `Menu`
      coordinate siblings, and configure the retained popup's menu-specific
      frame, surface, placement, and closing focus policy.
- [ ] Rerun the focused unit and mounted tests.

### Task 4: Reconcile specifications and showcase evidence

**Files:**

- Modify: `docs/controls/menus/menu.md`
- Modify: `docs/controls/menus/menu-item.md`
- Modify: `docs/controls/windows/popup.md`
- Modify: `docs/testing/controls-integration.md`
- Modify: `src/SharpVision.Showcase/Panes/MenuPane.cs`
- Modify or create: `tests/SharpVision.Showcase.Tests/MenuPaneTests.cs`

- [ ] Update normative defaults, layout, hover, navigation, submenu-chain, and
      responsibility-boundary prose without attributing menu policy to `Popup`.
- [ ] Add a nested submenu showcase specimen and representative screen proof.
- [ ] Run the menu and showcase focused tests.

### Task 5: Verify the complete repository

**Files:**

- Verify all intentional files above while preserving unrelated working-tree
  changes.

- [ ] Run `make format`.
- [ ] Run `make lint`.
- [ ] Run `make build`.
- [ ] Run `make test` and require zero warnings/errors plus the configured test
      minimum.
- [ ] Review the final diff for accidental edits, placeholders, stale docs, and
      changes to generic popup behavior.
