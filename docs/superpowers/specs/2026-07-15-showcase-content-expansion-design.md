# Showcase Content Expansion Design

Date: 2026-07-15

## Problem

The showcase has the right architecture but too little teaching depth. Its 19
pages contain 62 `Doc.Example` blocks, an average of 3.3 examples per page.
`FigletText` has one example; `List`, `Popup`, `RadioButton`, `Table`, and
`Window` have two. Even the fuller pages usually demonstrate a happy path and
one visual variant rather than answering the questions an application author
encounters while composing a real interface.

The live examples are stronger than the prose around them: every page is
responsive and executable, but users must still leave the showcase to discover
important interaction, sizing, ownership, state, and edge behavior from the
normative specifications.

Canvas has a second clarity problem. Its page currently mixes two related but
different surfaces:

- `SharpVision.Controls.Canvas` is a child-positioning container; and
- `SharpVision.Terminal.Rendering.Canvas`, exposed to controls as
  `TerminalCanvas`, is the semantic cell drawing surface used by `OnRender`.

Both deserve more examples, but the page must name the distinction explicitly so
users do not mistake a rendering primitive for a layout API.

## Baseline audit

| Page        | Existing examples | Main missing user questions                                                                               |
| ----------- | ----------------: | --------------------------------------------------------------------------------------------------------- |
| Button      |                 4 | Commands, programmatic activation, real default/cancel routing, pressed-state behavior                    |
| Canvas      |                 5 | Opposing-edge stretch, intrinsic sizing, negative placement, pointer transparency, richer drawing recipes |
| CheckBox    |                 4 | Event order, programmatic toggling, custom marks, form composition                                        |
| ComboBox    |                 3 | Empty selection, popup fallback placement, rail options, committed versus dismissed selection             |
| Dock        |                 3 | Spacing, percentage-of-remainder sizing, collapse, over-consumption, application-shell composition        |
| FigletText  |                 1 | Font comparison, fitting/smushing, direction, clipping/scrolling, styling                                 |
| Grid        |                 3 | Percentage and min/max tracks, implicit tracks, wrapping remeasure, responsive forms                      |
| List        |                 2 | Selection modes, templates, invocation versus selection, scrolling, dynamic item snapshots                |
| Menu        |                 3 | Popup composition, menu-level events, complete item kinds, compact spacing                                |
| Overlay     |                 3 | Pointer transparency, stable equal z-order, dynamic z changes, focus-order independence                   |
| Popup       |                 2 | Edge flipping/clamping, close events, focus restoration, surface styling, resize                          |
| RadioButton |                 2 | Unnamed groups, empty groups, event order, arrow traversal, regrouping                                    |
| ScrollBar   |                 3 | Range/viewport geometry, typed causes, keyboard/wheel parity, tiny rails, custom glyphs                   |
| Stack       |                 3 | Collapse versus hide, cross-axis alignment, margins, overflow, constrained shrinking                      |
| Table       |                 2 | Interactive cells, all column modes, dynamic rows, header/grid styling, Unicode and tiny widths           |
| Text        |                 7 | Escaping/recovery, complete overflow comparison, semantic colors, ambiguous width, line metrics           |
| TextInput   |                 5 | Submission, selection, clipboard, undo/redo, event cancellation, grapheme-safe movement                   |
| Window      |                 2 | Shadow modes, title clipping, styling, responsive composition, concrete Enter/Escape fallback             |
| Theming     |                 5 | State-style matrix, chrome recipes, catalog metadata, custom-control theming workflow                     |

## Goals

- Give every page several clearly named sections that progress from first use to
  realistic composition and boundary behavior.
- Keep every important claim adjacent to a live specimen that proves it.
- Add compact C# excerpts when they help a user reproduce the specimen. Omit an
  excerpt when it would merely repeat a property already obvious in the live
  label.
- Prefer application-shaped examples—settings forms, action bars, result lists,
  dashboards, menus, dialogs, and status surfaces—over abstract colored boxes.
- Keep examples responsive at 30x8, 80x24, and 140x40 cells and usable through
  the real keyboard and pointer pipeline.
- Expand Canvas substantially, covering both layout Canvas and semantic custom
  drawing without conflating their APIs.
- Update the showcase architecture, testing contract, and focused screen and
  interaction tests with the implementation.

## Non-goals

- No new production controls or terminal protocols.
- No virtual tree, function-component, hook, or showcase-only rendering path.
- No exhaustive property inspector. The normative control specifications remain
  the complete API contracts.
- No example that relies on reflection, private members, fake controls, or raw
  escape-sequence emission.
- No dedicated `Border`, `Shadow`, or `ScrollView` pages; those are intrinsic
  properties of existing controls and containers.

## Page structure

Each page uses four or five section groups selected from this progression:

1. **Start here** — the smallest useful live control and a short reproduction
   excerpt.
2. **Configure it** — the properties that materially change behavior or
   geometry.
3. **Interact with it** — keyboard, pointer, focus, selection, events, and
   disabled behavior with visible state or event output.
4. **Compose it** — one application-shaped recipe using other public controls.
5. **Know the boundaries** — resizing, clipping, Unicode, tiny bounds,
   scrolling, safe fallback, or ownership behavior relevant to users.

The labels are not mandatory boilerplate. A display control may use
“Formatting,” “Layout,” and “Unicode” while a layout panel may use “Sizing,”
“Composition,” and “Constrained space.” What matters is a visible information
hierarchy and a progression a user can follow.

`Doc.Section` groups related examples under a wrapped section heading and a
one-paragraph orientation. `Doc.Example` remains the leaf unit and gains an
optional compact source excerpt beneath its description. Source excerpts use
escaped marked `Text`, a framed surface, and wrapping suitable for the narrow
documentation column. They are illustrative extracts from the live specimen, not
a second source of behavior.

## Component extension catalog

### Button

- **Start here:** retain the click/Enter/Space/pointer activation specimen and
  visible `ActivationCause` log.
- **Commands:** add a command/parameter specimen with a CheckBox-controlled
  `CanExecute` state so disabled command behavior is observable.
- **Window roles:** move default/cancel from inert marker buttons into a small
  live `Window` where Enter invokes Apply and Escape invokes Cancel.
- **Chrome and states:** retain composite, block-glyph, and flat shadows; add
  glyph-family and padding variants plus a note explaining face translation
  while pressed.
- **Programmatic use:** add a “Run now” control that calls `PerformClick()` and
  records the Programmatic cause.

### Canvas

The page begins with a plain-language callout distinguishing layout Canvas from
`TerminalCanvas` custom drawing.

- **Positioning:** fixed left/top, percentage placement with a resize-sensitive
  readout, and right/bottom anchoring.
- **Constraints:** show automatic-size stretch from opposing edges beside an
  explicitly sized child where left/top takes precedence.
- **Intrinsic and constrained size:** show the finite union of fixed children, a
  negative/off-screen child, and safe clipping at tiny bounds.
- **Layering and input:** retain insertion-order overlap; add a visible
  `IsHitTestVisible = false` top layer over a clickable lower layer with a hit
  log.
- **Drawing fundamentals:** split the current crowded primitive specimen into a
  line/box style matrix, shade/quadrant palette, and styled fill/clear sample.
- **Unicode drawing:** add a custom control that draws combining text, CJK, and
  emoji near a clip edge and visibly preserves complete cell ownership.
- **Useful custom drawing:** add a responsive mini chart or sparkline-like
  dashboard that uses `Draw`, lines, boxes, fills, and styles from `OnRender`.
  Its data is deterministic and its labels explain that controls draw cells,
  never ANSI bytes.
- **Pointer-aware drawing:** add a small coordinate pad that updates a marker
  and exact cell/pixel readout through routed pointer events, demonstrating how
  custom drawing and ordinary input compose.

### CheckBox

- **Two-state choice:** retain the live toggle and cause log.
- **Three-state policy:** retain the three-state cycle and add a programmatic
  switch back to two-state that visibly normalizes indeterminate to false.
- **Marks:** retain built-in families and add one validated custom `Marks`
  specimen.
- **Events:** show state-specific event followed by `StateChanged` in a compact
  event log.
- **Form recipe:** compose several checkboxes in a settings card, including a
  disabled checked option whose retained state remains visible.

### ComboBox

- **Start here:** retain the density picker and committed selection label.
- **Commit versus dismiss:** show Enter committing and Escape retaining the
  previous selection with a two-line event log.
- **Long choices:** retain the capped drop-down and expose thin/full rail
  options on a sufficiently long catalog.
- **No selection:** demonstrate `SelectedIndex = -1`, a placeholder label, and a
  button that clears the current selection programmatically.
- **Constrained placement:** place a ComboBox near the lower edge of a bounded
  stage so Popup fallback placement is visible after resize.
- **Unavailable state:** retain the disabled field with a concise explanation.

### Dock

- **Application shell:** turn the four-sides sample into a recognizable header,
  sidebar, status bar, inspector, and main content shell.
- **Order and spacing:** retain repeated-side ordering and make the configured
  gaps visible.
- **Sizing from the remainder:** add percentage children whose width resolves
  against the rectangle remaining at their insertion step.
- **Collapse and fill:** add a button or checkbox that collapses a sidebar and
  shows the fill child reclaiming its space.
- **Constrained space:** demonstrate saturated over-consumption at a tiny width
  without negative child rectangles.

### FigletText

- **Live editor:** retain source text and audited font picker.
- **Font comparison:** render the same short word in three materially different
  audited fonts so selection starts from visible intent rather than 400 names.
- **Layout options:** compare full-width, fitted, and smushed output using
  `FigletOptions` and label the active direction/layout.
- **Style:** show semantic foreground/background/attribute overrides while
  retaining theme inheritance on a sibling.
- **Large output:** place a wide font in an `AutoScroll` container and explain
  that `FigletText` does not scale or wrap.
- **Fallback:** include unsupported/source Unicode in a compact sample and
  explain font fallback without claiming that every font supplies every glyph.

### Grid

- **Track fundamentals:** keep fixed and auto/star examples, reorganized under
  one sizing section.
- **Percentage and limits:** add percentage tracks plus visible `Minimum` and
  `Maximum` constraints.
- **Spans:** retain row/column/both spans with clearer non-overlap labels.
- **Implicit grid:** demonstrate empty row/column definitions behaving as one
  automatic cell.
- **Responsive form:** compose labels, inputs, validation text, and an action
  row; narrow resizing must visibly remeasure wrapped text.
- **Constrained space:** show deterministic spacing saturation and non-negative
  tracks at a tiny width.

### List

- **Single selection:** retain the current selected-item and invocation log but
  distinguish active, selected, and invoked state in the wording.
- **Selection modes:** add None, Single, and Multiple specimens; Multiple shows
  Control toggle and Shift range behavior in a selected-items readout.
- **Templates:** add a custom template with Unicode, status text, and
  variable-height content built from ordinary controls.
- **Long data:** add a scrollable list with Home/End/Page keys, thin rails, and
  a visible offset/active-index readout.
- **Snapshot replacement:** add a button that replaces `Items` and shows
  deterministic selection normalization.
- **Unavailable items:** retain disabled context and include an unavailable
  templated row that keyboard navigation skips.

### Menu

- **Command menu:** retain command, separator, check, and radio kinds with an
  invocation log.
- **Menu bar:** retain horizontal orientation and show Left/Right focus
  traversal.
- **Popup composition:** place a vertical Menu inside an anchored Popup to show
  the conventional flyout composition.
- **Selection and invocation:** expose `SelectedIndex`, invoked item, and cause
  separately so navigation is not confused with activation.
- **Spacing and unavailable items:** compare compact and relaxed spacing while
  preserving disabled-item skipping.

### Overlay

- **Layering:** retain negative/default/high z-order with clearer paint-order
  labels.
- **Stable ties:** add two equal-z children and a control that reverses their
  collection order, proving stable tie behavior.
- **Pointer transparency:** place a decorative top layer over an interactive
  lower Button and log that the lower control still receives input.
- **Alignment and sizing:** retain the five alignments and add one percentage-
  sized centered card.
- **Clipping:** retain the side-by-side clip comparison.
- **Composition:** add a lightweight notification/banner layer over ordinary
  content without changing focus traversal order.

### Popup

- **Anchored menu:** retain the action-menu specimen and focus restoration.
- **Placement:** retain all four preferred sides.
- **Fallback and clamp:** put anchors at each stage edge and expose the final
  `SurfaceBounds` so flipping and clamping are visible.
- **Lifecycle:** display Opening state, `Closing`, and `Closed` notifications;
  Escape and a close button use the same observable path.
- **Surface style:** compare inherited and explicit background/border colors.
- **Resize:** keep one popup open while its host stage resizes and report the
  recomputed surface bounds.

### RadioButton

- **Named group:** retain exclusive selection, disabled skipping, and a live
  selection log.
- **Arrow traversal:** arrange the same group in a visible order and explain
  wrapping Left/Up versus Right/Down behavior.
- **Unnamed scope:** show two sibling-scoped unnamed groups in different parent
  containers.
- **No initial selection:** begin with an empty group and show the first user
  selection.
- **Programmatic regrouping:** move one checked option to another `GroupName`
  and show both groups’ committed state.
- **Events:** expose Unchecked, Checked, then SelectionChanged ordering.

### ScrollBar

- **Range anatomy:** label minimum, maximum, value, viewport, and computed thumb
  size beside a horizontal rail.
- **Input parity:** retain full horizontal and vertical rails and log keyboard,
  wheel, button, track, and drag `Cause` values.
- **Chrome:** retain full and thin rails; add custom validated glyphs.
- **Live range:** controls adjust viewport and maximum so thumb geometry updates
  visibly without replacing the ScrollBar.
- **Tiny rails:** show one-, two-, and three-cell fallback rendering.
- **Nested behavior:** put a rail at an endpoint inside an overflowing container
  and explain that an unchanged wheel event bubbles to the parent.

### Stack

- **Orientation:** show equivalent vertical and horizontal stacks.
- **Mixed sizing:** retain fixed, percentage, automatic, and proportional tracks
  with a visible allocation legend.
- **Spacing and margins:** distinguish inter-child spacing from child margins.
- **Reverse:** retain visual/navigation reversal and add a focus-order readout.
- **Visibility:** compare hidden and collapsed children so spacing and extent
  behavior are obvious.
- **Constrained space:** show later proportional tracks shrinking safely to zero
  when minimums cannot fit.
- **Recipe:** add a responsive action bar with a proportional spacer between
  primary and secondary actions.

### Table

- **Column sizing:** expand the mixed table to label fixed, auto, percentage,
  and fill columns explicitly.
- **Header and grid chrome:** compare a structured data table with the compact
  headerless key/value variant.
- **Interactive cells:** add a row containing a Button, CheckBox, or TextInput
  to prove ordinary focus and input routing inside cells.
- **Dynamic rows:** add and remove a detached row through ordinary collection
  ownership and show the row count.
- **Responsive text:** include marked links, CJK, emoji, and wrapping detail
  cells at narrow widths.
- **Boundary states:** show a header-only table and a tiny clipped table without
  phantom row spacing.

### Text

- **Safe content:** add `Text.Escape` with user-provided angle brackets and a
  malformed-markup recovery specimen.
- **Markup:** retain attribute, underline, color, semantic-role, and OSC 8 link
  coverage, grouped into smaller readable examples.
- **Overflow:** show Visible, Wrap, WrapAnywhere, Clip, and Ellipsis side by
  side against the same Unicode content.
- **Alignment and lines:** retain centered text, add end alignment and a visible
  `Lines` metrics readout after resize.
- **Unicode:** retain composed/decomposed, combining, CJK, emoji, and flags; add
  explicit narrow/wide ambiguous-width comparison.
- **Tabs and logical lines:** show four-cell tab stops and CR/LF/CRLF behavior.
- **Live mutation:** retain the append-markup action and make the resulting
  remeasure visible.

### TextInput

- **Editing and submission:** retain free-form editing and add a Submitted event
  log for the single-line editor.
- **Selection:** add selection/caret readouts, grapheme-safe arrow movement, and
  a “Select all” action over combining/emoji content.
- **Clipboard and history:** add Copy, Cut, Paste-through-terminal guidance,
  Undo, and Redo buttons with availability status.
- **Policies:** retain read-only, password, maximum length, return, and tab
  examples, grouped by what mutation each policy permits.
- **Events:** show cancellable `TextChanging` beside ordered `TextChanged` and
  `SelectionChanged` output.
- **Multiline:** retain nested wheel routing and expose horizontal/vertical
  offsets plus canonical scrollbar options.
- **Unicode boundary:** add a focused example deleting one complete ZWJ or
  combining grapheme rather than one UTF-16 unit.

### Window

- **Frame and title:** retain rounded, paired, and ASCII glyphs with all title
  placements.
- **Shadows:** add composite, block-glyph, and shadow-disabled windows.
- **Default and cancel:** retain the project-settings surface but make the
  action log and Enter/Escape fallback explicit.
- **Surface style:** compare inherited theme chrome with explicit border,
  background, and attributes.
- **Composition:** show a Window in Canvas and another in Overlay, emphasizing
  that it introduces no modality or private input model.
- **Boundaries:** demonstrate long-title clipping and a tiny window without
  overwriting corners or drawing outside its box.

### Theming

- **Application theme:** retain the sidebar picker and live semantic-role
  swatches.
- **Catalog:** show the selected theme’s display name, slug, dark/light scheme,
  and source metadata from `ThemeCatalog`.
- **Type and local styles:** retain type-keyed and per-instance override
  examples with a clearer precedence readout.
- **Visual states:** add a compact matrix of normal, hovered, focused, pressed,
  checked, and disabled appearances using ordinary interactive controls.
- **Shared chrome:** demonstrate themeable border and shadow properties on
  ordinary controls rather than wrapper types.
- **Third-party controls:** retain `ShowcasePanel.LabelPlacement` and pair it
  with a short recipe for registering and resolving a custom `StyleProperty`.

## Testing design

The expanded content is proof, not decoration. Tests therefore cover four
layers:

1. **Catalog/content contract:** every page has its expected section and example
   headings, wrapped prose, at least one live instance of its subject, and no
   duplicate owned control.
2. **Virtual screens:** every page renders at 30x8, 80x24, and 140x40 with valid
   wide-cell continuation ownership. Focused assertions cover each newly added
   Canvas drawing specimen and each page’s defining application recipe.
3. **Interaction:** representative examples drive keyboard, pointer, focus,
   selection, editing, popup, drag, and resize through `Application` and the
   fake terminal rather than invoking private helpers.
4. **Live smoke:** tmux continues to cover navigation and a small cross-section
   of interactions. It does not duplicate every example and remains supplemental
   to semantic cell and event assertions.

New examples must first fail a focused showcase test for their expected public
behavior. Screenshots are optional review artifacts; they are never the only
oracle.

## Documentation impact

- Update `docs/architecture/showcase.md` with the progressive page model,
  optional source excerpts, and the explicit layout-Canvas versus
  `TerminalCanvas` distinction.
- Update `docs/testing/showcase.md` with the per-page content catalog and proof
  expectations.
- Link Canvas drawing guidance to
  `docs/architecture/rendering-pipeline.md#rendering-pipeline-contract` and
  Unicode specimens to
  `docs/concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract`.
- Do not duplicate control contracts inside the showcase architecture page; each
  example links conceptually to the normative control document that owns its
  behavior.

## Delivery order

1. Extend `Doc` and add content-contract tests.
2. Expand pages in coherent groups: command/toggle controls, selection/editing,
   layout, layering/windows, data/display, and theming.
3. Expand Canvas last among content pages so its drawing specimens can reuse the
   final documentation patterns while receiving dedicated screen tests.
4. Update normative showcase docs and run all quality gates.
