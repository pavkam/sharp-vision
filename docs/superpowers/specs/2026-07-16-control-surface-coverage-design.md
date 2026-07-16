# Control Surface Coverage Design

## Purpose

Extend the mounted `ComponentSurface` proof from `Button` to every other
catalogued SharpVision control except the deliberately deferred `ComboBox`,
menu, popup, and window families. The suites specify the intended public
behavior first, drive real terminal input, and retain every discovered defect as
a regression after fixing the responsible production code.

This is not a migration of every existing unit test. Unit tests remain the best
oracle for pure algorithms, validation, ownership, invalidation, and exhaustive
state machines. A surface test is required where input, focus, layout, styling,
rendering, or resize must agree across layers.

## Scope

### Included controls

- Display: `Text`, `FigletText`, `Separator`, and `ProgressBar`.
- Input: `CheckBox`, `RadioButton`, and `TextInput`.
- Layout: `Stack`, `Grid`, `Dock`, `Overlay`, `Canvas`, `Table`, `ScrollBar`,
  `GroupBox`, and `Expander`.
- Collections: `List`, `TabControl`, and `TabItem`.
- Navigation: `NavigationView`, `NavigationViewItem`, `NavigationViewGroup`, and
  `NavigationViewSeparator`.

`Button` already owns the reference surface suite and is not duplicated.
`Separator`, `ProgressBar`, `GroupBox`, `Expander`, `TabControl`, and `TabItem`
are included because the control catalog claims them even though production
types do not yet exist. They will be implemented test-first from their normative
contracts, with XML documentation and showcase coverage.

### Excluded controls and roles

- `ComboBox`, `Menu`, `MenuItem`, `MenuSeparator`, `MenuBuilder`, `Popup`, and
  `Window` are deferred because their transient-layer and dismissal behavior
  needs a dedicated multi-surface design.
- Abstract authoring roles, private presentation hosts, data collections, and
  internal item wrappers are tested through their concrete public owner.
- `Screen` and application hosting types are not mountable controls.

The uncommitted `NavigationView` work already present in the user worktree is
user-owned. Its phase comes last and must reconcile with, rather than overwrite,
that work.

## Evidence model

Each surface scenario follows Arrange, Act, Assert and combines only the oracles
that matter for the behavior:

1. mount a normally constructed control in a fixed cell surface;
2. perform a user action through encoded terminal bytes or a dispatcher-safe
   public property mutation;
3. assert public value, selection, focus, capture, offset, or event state;
4. assert exact whole-surface text when spatial appearance is significant; and
5. inspect representative semantic cells for style, wide-cell continuation,
   cursor, chrome, or overlap ownership.

Snapshots are reviewable geometry evidence, never the sole oracle. Tests must
not call `Router.Route`, `FocusManager.Focus`, protected visual-state setters,
or private layout/render methods. A discovered mismatch is resolved against the
normative control and shared concept contracts. If the contract is ambiguous,
the design is clarified before changing either the expectation or production
behavior.

Every production correction follows a visible red-green cycle: add the focused
surface regression, run it and record the expected failure, make the smallest
responsible implementation change, rerun the focused fixture, then run the
nearest existing unit and showcase fixtures.

## Scenario cards

### Display controls

`Text` proves styled markup and transparent background composition, Unicode
combining and wide graphemes, wrap/clip/ellipsis alignment, and resize reflow on
one mounted instance. Assertions cover exact rows, style spans, continuation
cells, and removal of stale cells after mutation.

`FigletText` uses a deterministic embedded font and proves exact generated art,
inherited and explicit style, clipping at small bounds, source mutation, and
resize exposure. Catalog-wide parsing and smushing remain unit-test concerns.

`Separator` proves non-focusable and non-hit-testable horizontal and vertical
lines, inherited style, orientation mutation, and zero/tiny bounds. Its glyph
and desired-size behavior must be made explicit in the control contract before
implementation.

`ProgressBar` proves determinate empty, partial, and full values; clamping after
range mutation; horizontal and vertical fill direction; inherited style; and
zero/tiny/resize behavior. Indeterminate mode remains static until animation is
specified, but it must render a deterministic distinct indication rather than
masquerading as an arbitrary determinate value.

### Toggle controls

`CheckBox` proves unchecked, hovered, pressed, focused, checked, indeterminate,
and disabled combinations. Space and primary-click activation must produce the
same transition and event cause. Exact mark, label, combined style, tiny-bound
clipping, and pointer-capture release are asserted.

`RadioButton` mounts a real group and proves no initial selection, Space and
pointer selection, exclusivity, arrow focus-and-selection with wrapping,
disabled-member skipping, Unicode labels, combined styles, and exact mark cells.
The suite asserts group state and event order as well as appearance.

### Text editing

`TextInput` proves focus and semantic terminal cursor, decoded Unicode typing,
grapheme-safe Left/Right and Backspace/Delete, Home/End, selection, atomic
bracketed paste, submit versus multiline Enter, read-only and disabled behavior,
placeholder/password rendering, pointer placement/drag, wheel scrolling, and
resize-driven offset repair. Exact cells never expose password source text.

### Layout controls

`Stack` proves vertical and horizontal placement, spacing, fixed/automatic/
proportional children, resize reflow, overflow clipping, intrinsic scrolling,
and pointer hit testing after movement.

`Grid` proves fixed/automatic/proportional tracks, spanning, deterministic
remainder assignment, padding, resize reflow, overflow clipping, and hit testing
against committed cells.

`Dock` proves each edge plus fill, stable remaining-space order, resize, tiny
bounds, and exact hit targets.

`Overlay` proves common-slot arrangement, later-child visual and hit-test
precedence, hidden-child reveal, clipping, and resize.

`Canvas` proves cell and percentage coordinates, z-order, negative/overflow
clipping, resize repositioning, and hit testing using final arranged bounds.

Layout suites use simple `Text` or `Button` leaves as probes but assert the
container's contract. They do not duplicate the probe control's own state
matrix.

`GroupBox` proves header interruption of the intrinsic border, content insets,
Unicode/wide headers, style inheritance, content replacement, and tiny-bound
clipping. It is a `ContentControl`, not a general child panel.

`Expander` proves expanded and collapsed layout, pointer and Space activation,
focus, indicator and header cells, complete removal of collapsed content from
layout/hit testing, content replacement, and resize. It uses retained private
composition rather than rebuilding children during layout.

### Scrolling and collections

`ScrollBar` proves value-to-thumb geometry for horizontal and vertical rails,
minimum thumb size, arrow keys, wheel, track paging, thumb drag with pointer
capture, endpoint bubbling, range/viewport mutation, disabled behavior, and
zero/tiny bounds.

`List` proves initial and changed selection, focus, Up/Down/Home/End navigation,
primary-click parity, disabled-item skipping, viewport scrolling, Unicode and
wide rows, item removal with selection repair, resize, and cleared stale cells.

`Table` proves headers, row/column placement, automatic/fixed/proportional
columns, wrapping, Unicode width, vertical and horizontal scrolling, row
mutation/removal, resize, clipping, and stale-cell clearing. Selection is tested
only if it is part of the public contract.

`TabControl` and `TabItem` prove initial selection, pointer and keyboard tab
selection, selected content replacement, focus behavior, disabled-tab skipping,
selected-item removal repair, Unicode headers, overflow navigation, resize, and
complete clearing of old content.

### Navigation controls

`NavigationView` proves header, main, footer, group, and separator composition;
initial and changed selection; pointer and arrow navigation; group expansion;
disabled-item skipping; viewport scrolling; item removal repair; Unicode cells;
and resize. These expectations are reconciled with the normative NavigationView
contract after the existing uncommitted implementation stabilizes.

## Harness extensions

The harness grows only in response to a red scenario:

- `ComponentKeyboard` gains encoded text, Space/Enter press and release, arrows,
  Home/End/Page keys, Backspace/Delete, modifiers, and bracketed paste.
- `ComponentPointer` gains explicit target-relative coordinates, wheel reports,
  and captured drag sequences.
- `ComponentSurface` gains dispatcher-safe `UpdateAsync`, terminal
  `ResizeAsync`, descendant state assertions, event checkpoints, and semantic
  cursor access.
- Settling continues to require input consumption plus dispatcher/frame
  completion; no wall-clock sleeps or retry-to-green behavior is allowed.

All actions preserve the real path through terminal decoding, routing, focus or
capture, layout, rendering, terminal encoding, and the independent virtual
screen. Helpers validate before queuing work and include the current surface in
timeouts and assertion failures.

## Delivery phases

1. Display controls and toggles establish mutation, text input, descendant
   state, and tiny-bound notation.
2. Text editing and scrolling add the richer keyboard, paste, wheel, drag,
   cursor, and resize drivers.
3. Layout controls prove committed geometry, overlap, clipping, and hit tests.
4. Lists, tables, and tabs prove selection, removal repair, and viewport state.
5. NavigationView reconciles and covers the existing user-owned work.
6. A catalog audit links each included control contract to its surface fixture,
   checks showcase coverage, and runs all repository gates.

Each phase is independently planned, committed, and verified. A phase may fix
core behavior used by later controls, but it may not weaken an intended contract
merely to preserve existing output.

## Completion criteria

The goal is complete only when every included concrete control has at least one
meaningful mounted surface fixture; every scenario card is either represented by
surface evidence or explicitly retained as a named unit-test responsibility; all
discovered product defects are fixed with regression tests; the five catalogued
missing control families ship with docs, XML documentation, and showcase
examples; the testing contract and control specs link to their proof; and
`make format`, `make lint`, `make build`, and `make test` pass with no warnings
or missing-test failures.
