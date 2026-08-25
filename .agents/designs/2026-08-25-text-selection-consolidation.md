# Text selection hierarchy consolidation

## Goal

Make `ControlBase` the only owner of semantic text-selection state, commands,
gesture arbitration, keyboard navigation, autoscroll, lifecycle cleanup,
rendering, and common notification. Every control inherits the same opt-in
mechanic; no concrete component keeps a parallel selection controller.

## Ownership

`ControlBase` owns the directional range, semantic fingerprint, desired visual
column, pointer gesture, capture, edge scrolling, caret reveal, final selection
adornment, and `TextSelectionChanged`. Its public text-selection properties and
commands are inherited implementations rather than component override seams.

Concrete controls may provide only policy or source data:

- an authoritative `TextSelectionMap` or selectable-text snapshot;
- selected-face colors;
- whether copied text may be disclosed;
- whether a pointer cell belongs to selectable content;
- a post-commit compatibility notification;
- a non-selection action completed by an unconsumed click, such as activating a
  document link or toggling a code-fold gutter.

These hooks cannot store a second range or replace the common controller.

## Specialized controls

`Document` retains Markdown layout, embedded source projection, link identity,
and scrolling layout. It deletes `DocumentSelectionGesture`, its private range,
keyboard selection navigation, selection autoscroll, and selection paint loop.
Its legacy `Selection`, `SetSelection`, `SelectAll`, `ClearSelection`,
`CopySelection`, and `SelectionChanged` surface delegates to the inherited
mechanic.

`CodeView` retains normalized code, syntax spans, folding, viewport projection,
and code-specific selected colors. It deletes private range state, desired
column state, pointer selection, selection timer, keyboard range movement, and
selection overlay painting. Fold-gutter presses stay component behavior and are
excluded before the common gesture arms.

`TextInput` retains text mutation, undo/redo, cursor rendering, word-wrap
projection, password policy, and editor-only cut/paste/replace commands. Its
edit transaction commits the proposed range into base state before observers
run, preserving the existing `TextChanged` then `SelectionChanged` order. It
deletes private range storage, pointer selection, range paint logic, and
selection state-machine navigation. Its cache-backed navigation hook preserves
editor performance without owning range state. Password mode provides semantic
validation without exposing glyphs or copied source text.

## Shared behavior

The common gesture supports potential click, thresholded drag, capture,
double-click word selection, triple-click visual-line selection, cross-child
selection, and bounded nested autoscroll. Click completion is offered to a
component only when the same eligible non-selection target survives press and
release.

The common keyboard path owns Ctrl+A, grapheme and word movement, visual
Home/End, vertical sticky-column movement, and Page Up/Page Down. Movement
without Shift collapses or moves the caret; Shift extends from the directional
anchor. After a committed caret movement, the common controller reveals the
caret through the innermost selectable viewport and eligible ancestor
containers.

The final subtree adornment is always painted by `ControlBase`; specialized
controls choose colors but never repaint selected glyphs themselves.

## Compatibility and validation

Legacy convenience APIs remain and forward to the inherited API. Removing the
protected controller-replacement seams is an intentional tightening of the
pre-release API: consumers can customize projection and policy, but cannot fork
selection mechanics.

Tests prove the same pointer, keyboard, rendering, event, mutation, password,
link, fold, and autoscroll behavior through ordinary controls and all three
specialized controls. Completion also requires the full repository gates and a
real Showcase run under tmux with captured terminal output.

## Implementation plan

- [x] Add RED tests for shared double/triple click, plain and modified keyboard
      movement, common caret reveal, specialized event compatibility, password
      secrecy, link/fold click coexistence, and final selected-cell styling.
- [x] Extend the common map/controller with word and line selection, plain
      movement, caret reveal, click-policy hooks, selected-face hooks, and one
      post-commit hook.
- [x] Convert `TextInput` to base-owned range state, remove its private pointer
      and selection-painting paths, and retain cache-backed navigation only as a
      common-controller policy hook.
- [x] Convert `CodeView` to base-owned mechanics and retain only projection,
      folding, style, and compatibility aliases.
- [x] Convert `Document` to base-owned mechanics and delete its gesture, range,
      navigation, autoscroll, and paint duplication.
- [x] Tighten the public API snapshots and normative documentation.
- [x] Run focused suites, `make format`, `make lint`, `make build`, `make test`,
      then exercise and capture the Showcase in tmux.
