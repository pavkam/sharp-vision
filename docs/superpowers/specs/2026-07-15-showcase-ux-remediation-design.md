# Showcase UX Remediation Design

Date: 2026-07-15

## Problem

The expanded showcase contains substantially more content, but its information
hierarchy, visual states, and several specimens do not yet communicate the
library cleanly. The review also exposed product defects that a showcase must
not disguise: Table chrome and cells diverge while scrolling, Table scrolling
can terminate a debug process, and an open ComboBox does not dismiss when input
moves outside its popup.

This pass treats the showcase as executable product documentation. A visual
problem caused by a control contract is fixed in the control, specified, and
tested before the specimen is polished.

## Approach decision

Three approaches were considered:

1. Patch only the visible specimens. This is fast but leaves broken Table,
   popup, focus, and scrollbar behavior in applications.
2. Fix only production controls. This restores correctness but leaves the
   showcase difficult to scan and several examples unable to teach their API.
3. Complete one behavior-plus-showcase pass. Production defects are corrected
   first, then the gallery shell and specimens are rebuilt against the corrected
   public behavior.

The third approach is selected. It keeps one source of truth and prevents the
showcase from carrying local workarounds for product defects.

## Goals

- Replace underline-based standard focus with semantic color treatment on the
  control chrome that actually owns focus.
- Give pages a fixed identity header and a separately scrolling teaching body.
- Establish a visible hierarchy between page, section, example, explanation,
  code, and status text.
- Make default checked, selected, hovered, focused, pressed, and disabled states
  legible without painting unrelated empty cells.
- Make all owned and standalone scrollbars follow the active theme unless a
  caller supplies an explicit local override.
- Correct Table arrangement, scrolling, clipping, and signed coordinate
  behavior.
- Dismiss ComboBox popups on outside pointer presses and outside wheel input
  without stealing focus or committing a new selection.
- Add public arbitrary line and circle/ellipse drawing primitives to the
  semantic terminal Canvas and demonstrate them through the public API.
- Replace abstract or broken specimens with application-shaped examples whose
  purpose is evident before the user interacts.
- Prove the complete pass with public-surface unit, rendering, integration, and
  showcase screen tests.

## Non-goals

- No virtual tree, function component, hook, or showcase-only render path.
- No wrapper `Border`, `Shadow`, or `ScrollView` control.
- No modal behavior added to Window or Popup.
- No selection commit caused by merely hovering a List or ComboBox item.
- No sticky Table header. The Table header, grid, and rows are one scrollable
  content surface and move together.
- No use of underline as the standard focus cue. Underline remains available to
  authored Text markup and explicit caller styles.

## Visual-state design

The standard theme stops assigning `TerminalAttributes.Underline` to
`State.Focused` on the base `Control` style. Base checked state also stops
assigning an opaque selection background to every checked control.

State appearance moves to the semantic owner:

- Button focus changes `BorderColor` to `ThemeColors.Accent`. Pressed buttons
  use a distinct face/border color. Face translation remains conditional on
  `HasShadow`; a flat button never moves.
- ScrollBar focus colors its track and thumb with the accent role. It never
  decorates each repeated track glyph with underline.
- CheckBox and RadioButton checked state colors only the state mark. Unchecked,
  checked, indeterminate, focused, and disabled combinations remain distinct;
  none fills the control's unused arranged area.
- ComboBox uses `ThemeColors.Surface` as its opaque closed-field background and
  accent border treatment while focused. Its intrinsic border renders through
  the shared chrome path.
- ListItem hover fills the complete row with a subtle theme surface or accent
  treatment. Selected state remains stronger and wins when selected and hovered
  combine. Pointer leave restores the non-hovered state and hovering never
  changes selection.

The standard recipe is type-keyed. Applications can replace any state overlay
with `ControlStyle<TControl>`, and explicit local values retain final
precedence.

## Theme-owned scrollbar policy

Scrollbar chrome and fill become style-resolved properties for standalone
`ScrollBar` and scrollbar-owning controls. The standard themes define one
canonical chrome/fill recipe. `Container`, `List`, `ComboBox`, and `TextInput`
propagate the resolved values to their owned bars; they do not silently replace
them with unrelated hard-coded defaults.

An explicit `Chrome`, `Fill`, `ScrollBarChrome`, or `ScrollBarFill` assignment
remains a local override and wins over the theme. The same theme therefore
produces the same rail in a page, dropdown, table, list, and editor unless the
example is explicitly demonstrating an override.

## Documentation shell and hierarchy

`Doc.Page` becomes a two-region layout:

- a fixed, opaque page header containing the accent page name, a quiet
  `Overview` label, and the wrapped summary; and
- a vertically scrollable body containing the teaching sections.

Changing pages creates a fresh body at offset zero. Scrolling can never move the
page name or overview out of view.

`Doc.Section` gains an explicit one-emoji prefix chosen by each pane. Emoji is
used once per major section as punctuation, not on every example. Section
headings use accent color and strong weight with larger vertical separation.
Example headings use normal bold text and are visibly nested beneath a section.
Descriptions use dim text, code labels use the info role, success/warning/error
readouts use their semantic roles, and authored links retain intentional
underline. This establishes five levels without relying on size, which a cell
terminal cannot guarantee.

The sidebar footer becomes two aligned rows separated from navigation: `Theme`
and its picker share one row; `Quit` and the `Ctrl+C` hint share the next. Both
rows use the available width and degrade without overlap in a short terminal.

## Control-correctness design

### ComboBox dismissal and rendering

While open, ComboBox observes pointer input at the attached root in the preview
phase. A primary press outside both the field and popup closes it. Wheel input
whose physical target is outside the field and popup also closes it before the
surrounding scroll container consumes the delta. Inside pointer interaction
stays open until item invocation, the field still toggles, Escape still
dismisses, and dismissal never commits the highlighted item.

The temporary root registration is installed only while open and is removed on
close, detach, disable, and disposal. Outside dismissal preserves the focus that
the outside interaction normally selects; closing does not force focus back to
the field unless focus was inside the popup.

ComboBox calls the shared chrome renderer before drawing its label and arrow so
its opaque surface and optional border behave like other controls.

### Table cell arrangement

Table gives each cell its resolved track slot but lets the cell's ordinary
`HorizontalAlignment` and `VerticalAlignment` resolve the control inside that
slot. A default left-aligned automatic CheckBox therefore keeps its measured
width. The showcase option also uses top alignment so the taller Button in the
sibling cell does not stretch it vertically. Callers that want a cell control to
paint the whole track opt into `Stretch` explicitly.

### Table scrolling and clipping

Table stores the arranged scroll-content rectangle and renders headers, grid
lines, and cells from that same translated origin. Table chrome renders through
the scrolled content canvas, which is already clipped to the viewport, before
owned scrollbars render above it. Horizontal, vertical, and combined offsets
therefore keep column headers, separators, rows, hit testing, and scrollbar
chrome aligned.

The running row and column origin is signed. Extents and gaps remain
non-negative, but adding them to an origin that has moved above or left of zero
is valid and saturates safely. The extent-only accumulator retains its
non-negative invariant. This removes the debug assertion that currently
terminates Table scrolling.

### Canvas arbitrary geometry

The public terminal `Canvas` gains clipped integer rasterization primitives:

- `DrawLine(Point start, Point end, Rune value, CellStyle style = default)`
  draws both endpoints with a printable one-cell Rune using deterministic
  Bresenham traversal.
- `DrawEllipse(Rect bounds, Rune value, CellStyle style = default)` draws a
  one-cell outline inside the half-open bounds using deterministic midpoint
  ellipse traversal.
- `DrawCircle(Point center, int radius, Rune value, CellStyle style = default)`
  is the equal-radius convenience form and rejects a negative radius before
  mutation.

All three validate the Rune through the existing narrow-printable rule, clip to
the Canvas and Frame bounds, perform no per-cell managed allocation, and draw
through `DrawRune` so cell ownership and frame arena checks remain centralized.
Zero-length lines and zero-radius circles draw exactly one cell. Empty ellipse
bounds draw nothing; one-cell axes degrade to the corresponding line or point.
Circle radius is defined in cell coordinates. Because terminal cells are often
taller than they are wide, a caller that wants a physically round appearance
uses `DrawEllipse` with a wider cell rectangle; the library does not guess the
terminal's pixel aspect ratio when metrics are unavailable.

## Specimen redesign

### Button and state controls

- Place composite and block shadows over a patterned `Surface` backdrop so the
  composite specimen visibly preserves parent glyphs while restyling them.
- Keep the flat Button beside them and label that pressed color changes without
  translation.
- Replace the abstract programmatic target with an autosave recipe: a visible
  `Save draft` action and `Simulate autosave` trigger share `PerformClick`, and
  the log reports the Programmatic cause and invocation count.
- Present CheckBox and RadioButton marks without selection-background blocks.
- Repair the RadioButton cross-container example so members actually share the
  same `GroupName`; selecting one visibly clears the other.

### ComboBox

- Show the closed field on an opaque surface and include both unbordered and
  explicitly bordered field variants.
- Make dropdown hover distinct from committed selection.
- Use the canonical themed scrollbar in long dropdowns.
- Add prose beside the interactive specimen explaining outside-click, outside-
  wheel, Enter, and Escape outcomes.

### Canvas and Grid

- Resize the shade/quadrant specimen so every label ends before the right
  border; exact-cell tests preserve all four corners.
- Add a public-API geometry specimen containing a diagonal line, circle, and
  ellipse, with clipping visible at one edge.
- Rebuild Grid's auto/star specimen with enough height for visible row content,
  colored track regions, and labels showing the committed `2*:1*` allocation.

### Popup and Window

- Put Popup and Window specimens over populated application-like stages with
  toolbar, content, and status layers so their promoted/floating nature is
  unmistakable.
- Replace three isolated placement triggers with one placement lab: a central
  anchor, Above/Below/Left/Right controls, one open popup, and a requested-side
  status label. The popup visibly moves around the same anchor.
- Keep Window non-modal and ordinary in the input tree while using Overlay or
  Canvas z-order for every floating demonstration.

### Table

- Keep the interactive CheckBox at its measured top-left size inside the cell.
- Replace the overflowing headerless sample with a deliberate keyboard-shortcut
  reference containing three or four fully visible key/action rows, formatted
  keys, quieter descriptions, and no scrollbar at its documented size.
- Add a dedicated scrollable table specimen only after Table chrome, cells, and
  hit testing remain aligned on both axes.

### Theming and navigation

- Replace the arbitrary indexed-blue type-style preview with semantic colors, no
  shadow, a baseline Button beside the type-styled Button, and a concise readout
  such as `Background: Accent · Border: Heavy`.
- Keep raw `Glyphs` record formatting out of visible prose.
- Use the aligned two-row sidebar footer described above.

## Traceability to review findings

|   # | Finding                                                | Contract response                                       |
| --: | ------------------------------------------------------ | ------------------------------------------------------- |
|   1 | Focused scrollbar underlines every glyph               | Type-specific accent focus; no base focus underline     |
|   2 | Page identity scrolls away                             | Fixed `Doc.Page` header and scrolling body              |
|   3 | Focused Button underlines its frame                    | Accent border focus                                     |
|   4 | CheckBox checked highlight is ambiguous                | Mark-only checked state                                 |
|   5 | Section levels are indistinguishable                   | Page/section/example typography hierarchy               |
|   6 | Shadows are invisible                                  | Patterned contrasting parent stage                      |
|   7 | Programmatic Button example is abstract                | Autosave recipe with cause/count log                    |
|   8 | Sections need restrained emoji                         | One explicit emoji per major section                    |
|   9 | Canvas shade text overwrites border                    | Correct geometry and corner assertions                  |
|  10 | No arbitrary line/circle examples                      | Public line, ellipse, and circle APIs plus specimen     |
|  11 | Flat Button appears to move                            | Stationary exact-bounds proof and pressed color         |
|  12 | CheckBox bracket states look wrong                     | Semantic mark colors without block background           |
|  13 | ComboBox blends into parent and lacks bordered variant | Opaque Surface field and shared chrome                  |
|  14 | Dropdown hover is not visible                          | Full-row hover with selected precedence                 |
|  15 | Dropdown scrollbar differs                             | Theme-owned canonical scrollbar policy                  |
|  16 | ComboBox stays open on outside scroll/click            | Root-preview dismissal while open                       |
|  17 | Grid auto/star specimen is clipped                     | Taller labeled allocation stage                         |
|  18 | Type-keyed theme specimen is visually noisy            | Semantic side-by-side preview and concise readout       |
|  19 | Popup and Window look inline                           | Populated floating stages and z-order proof             |
|  20 | RadioButton paints a long selected runway              | Mark-only selected state                                |
|  21 | Popup placement controls are unclear                   | One central-anchor placement lab                        |
|  22 | Sidebar footer is poorly arranged                      | Two aligned rows with separator                         |
|  23 | Showcase barely uses text formatting                   | Semantic marked-text hierarchy and callouts             |
|  24 | Table CheckBox fills its cell                          | Alignment-aware cell arrangement and top-aligned option |
|  25 | Headerless Table is a scrollbar demo                   | Fully visible formatted shortcut reference              |
|  26 | Table scrolling corrupts output or terminates          | Shared translated chrome/cell space and signed origins  |

The Table-scrolling reports describe two separate observable failures: visual
divergence between chrome and cells, and the signed-origin assertion reached by
scrollbar interaction. They receive separate regression tests even though they
share the same translated content geometry.

## Testing design

Testing follows red-green cycles and asserts public behavior:

1. **Theme rendering:** exact focused Button and ScrollBar cells contain no
   underline; CheckBox and RadioButton unused cells retain the parent
   background; ComboBox fields and ListItem hover use the expected semantic
   colors; local overrides still win.
2. **Scrollbar policy:** standalone, Container-owned, List-owned,
   ComboBox-owned, and TextInput-owned bars resolve the same theme chrome/fill,
   followed by explicit-override cases.
3. **ComboBox integration:** real routed pointer presses and wheel reports close
   an open dropdown outside it, preserve it inside, avoid selection commit, and
   leave focus at the outside target.
4. **Table layout:** left/top-aligned interactive cells keep measured bounds;
   explicit Stretch fills a track. Horizontal, vertical, and combined scrolling
   compare exact headers, grid cells, content cells, scrollbar cells, clipping,
   and hit targets. A debug configuration scroll through the actual owned bar
   completes without assertion or termination.
5. **Canvas geometry:** exact octants, endpoints, degenerates, clipping, invalid
   Rune/radius validation-before-mutation, deterministic repeatability, and
   frame-arena behavior.
6. **Showcase structure:** fixed page-header bounds survive body scrolling;
   section emoji and markup roles are present; the footer remains contained at
   normal and constrained sizes.
7. **Specimen screens:** focused assertions cover the shadow backdrop,
   stationary flat Button, Canvas corners and geometry, Grid allocations,
   ComboBox variants, Popup placement, floating Window/Popup layers, RadioButton
   grouping, intrinsic Table CheckBox, and scrollbar-free headerless table.
8. **Responsive suite:** every page still renders at 30x8, 80x24, and 140x40
   with valid wide-cell ownership and no content painted over scrollbar chrome.

## Documentation impact

- Update `docs/concepts/styling.md` with type-specific standard state recipes
  and theme-owned scrollbar precedence.
- Update `docs/concepts/scrolling.md` with the canonical theme/default/local
  scrollbar policy.
- Update `docs/controls/input/combo-box.md` with field chrome, hover, and
  outside dismissal behavior.
- Update `docs/controls/layout/table.md` with cell alignment and unified
  scrolling behavior.
- Update `docs/architecture/rendering-pipeline.md` with the arbitrary geometry
  Canvas surface and clipping rules.
- Update `docs/architecture/showcase.md` and `docs/testing/showcase.md` with the
  fixed header, text hierarchy, emoji restraint, and corrected specimen proof.
- Update XML documentation on every affected public or internal member.

## Delivery order

1. Correct standard visual states and theme-owned scrollbar policy.
2. Correct ComboBox chrome, hover, and outside dismissal.
3. Correct Table cell alignment, scrolling render space, and signed origins.
4. Add and prove Canvas arbitrary geometry.
5. Rebuild the shared documentation shell and sidebar footer.
6. Rewrite the affected specimens and add focused showcase screens.
7. Update normative documentation and run `make format`, `make lint`,
   `make build`, and `make test` with zero warnings and errors.
