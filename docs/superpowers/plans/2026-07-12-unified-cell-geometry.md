# Unified Unicode and Cell Geometry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Unicode measurement, stored cell ownership, emitted text, pointer
coordinates, layout, editing, hit testing, and future graphics share one
explicit geometry contract.

**Architecture:** Preserve source text as grapheme-aligned UTF-16, but classify
each cluster into a width and safe terminal presentation before it reaches a
frame. Replace truncated cell-pixel dimensions with an exact rational grid and
make unavailable cell coordinates explicit. Publish one immutable cell policy
through `Application`, propagate it with control attachment, and migrate every
width consumer to that policy. Generalize frame ownership to rectangular spans
only after ordinary one-row text and repair invariants are green.

**Tech Stack:** .NET 10, C# 14, Unicode 17 generated data, UAX 29 revision 47,
UAX 11 revision 44, `Rune`, spans, `TimeProvider`-free deterministic geometry,
xUnit v3, Shouldly, independent test oracles, PTY/tmux smoke tests.

---

## Confirmed defects and delivery boundary

This plan completes vertical slice 2 from the approved terminal expansion
design. The current implementation already segments Unicode 17 extended grapheme
clusters and measures common combining, emoji, variation-selector, keycap, flag,
tag, ZWJ, invalid UTF-16, CJK, and ambiguous-width cases.

The slice fixes these confirmed gaps:

1. `Width.GetCluster` assigns an orphan combining/ZWJ/selector cluster one cell,
   but `Canvas` stores its raw scalars. The renderer can therefore emit a
   zero-width scalar after cursor positioning and let the terminal mutate the
   preceding cell while SharpVision claims ownership of the next cell.
2. `Dimensions.CellMetrics` divides total pixels by total cells and discards the
   remainder. Pixel mouse decoding then divides by that truncated value, so an
   uneven 101-pixel/10-cell grid maps its right edge as if eleven columns exist.
3. Pixel input without metrics fabricates `Point(0, 0)` and relies on a boolean
   to say it is not inferred. Hit testing can still treat that fabricated value
   as a real cell.
4. `Application` gives `Frame` the negotiated ambiguous-width policy, while
   `Text`, `RichText`, `TextInput`, `Table`, `ComboBox`, `Window`, `MenuItem`,
   `FigletText`, button glyph validation, scrollbar glyph validation, and other
   controls independently default to narrow width.
5. Frame ownership is a horizontal width byte. Graphics placeholders and scaled
   text need rectangular ownership so repair, clipping, selection, and damage
   cannot split a multi-row placement.

This slice does not implement Kitty explicit-width output, Kitty graphics,
Sixel, or iTerm2 images. It builds the geometry and ownership primitives those
backends require. It does not tailor width from locale or terminal names.

## Normative decisions

### Cluster source and presentation

- Editing and selection retain the caller's original grapheme-aligned UTF-16.
- A printable cluster with a real base stores its original valid UTF-8.
- Invalid UTF-16 stores U+FFFD, as today.
- A cluster containing only Extend, ZWJ, SpacingMark, Prepend, variation
  selectors, emoji modifiers, or tag components presents as one U+FFFD cell.
- The orphan presentation counts as one logical replacement in draw metrics.
- Raw orphan components are never emitted, even at column zero.
- Controls may copy the original source text; terminal frame access exposes the
  safe presentation because that is what owns the screen cell.

### Exact grid mapping

For a validated in-window pixel coordinate:

```text
column = floor(pixelX * cellColumns / pixelWidth)
row    = floor(pixelY * cellRows / pixelHeight)
```

The multiplication uses checked 64-bit arithmetic. A coordinate outside the
known pixel rectangle is not mapped or clamped into the terminal. Every pixel
inside the rectangle maps monotonically into exactly one valid cell, including
uneven final columns and rows.

Cell mouse input always has cells. Pixel mouse input always preserves pixels;
its cells are nullable and are present only when exact grid mapping succeeds.
Pointer leave has neither coordinate. A captured pixel-aware control may still
receive pixels without cells; ordinary hit testing requires cells.

### Width policy ownership

`Application.Capabilities` remains the immutable protocol profile and source of
the effective ambiguous-width setting. Each attached control receives the same
immutable cell policy reference. Adding a child to an attached tree inherits the
current reference before measure. A geometry-affecting capability update
replaces the reference for the whole tree on the dispatcher and invalidates root
measure once.

Public control-specific width overrides, where retained, are nullable. Null
means inherit the application policy. An explicit value is stable across
application profile changes and is documented as a local override.

### Rectangular ownership

A frame owner has a lead coordinate and positive `Size` in cells. Ordinary text
uses height one and width one or two. Every continuation references the lead.
Repair, clear, style, clip, copy, semantic comparison, damage, and selection
operate on the complete rectangle. A placement that cannot fit its complete
rectangle under the requested edge policy is skipped or replaced before any
mutation.

## File map

### Create

- `src/SharpVision.Terminal/Unicode/Cluster.cs` — internal width and safe
  presentation classification.
- `src/SharpVision.Terminal/Unicode/Presentation.cs` — orphan presentation
  policy enum.
- `src/SharpVision.Terminal/Unicode/Policy.cs` — immutable Unicode cell policy.
- `tests/SharpVision.Terminal.Tests/Unicode/ClusterTests.cs`
- `tests/SharpVision.Terminal.Tests/Geometry/MetricsTests.cs`
- `tests/SharpVision.Tests/Runtime/CellPolicyTests.cs`
- `tests/SharpVision.Tests/Integration/UnicodeGeometryTests.cs`

### Modify in the terminal layer

- `src/SharpVision.Terminal/Unicode/Width.cs`
- `src/SharpVision.Terminal/Capabilities/Capabilities.cs`
- `src/SharpVision.Terminal/Geometry/Metrics.cs`
- `src/SharpVision.Terminal/Runtime/Dimensions.cs`
- `src/SharpVision.Terminal/Input/Options.cs`
- `src/SharpVision.Terminal/Input/Pointer.cs`
- `src/SharpVision.Terminal/Input/Decoder.cs`
- `src/SharpVision.Terminal/Rendering/Cell.cs`
- `src/SharpVision.Terminal/Rendering/CellInfo.cs`
- `src/SharpVision.Terminal/Rendering/Frame.cs`
- `src/SharpVision.Terminal/Rendering/Canvas.cs`
- renderer and encoder ownership consumers under
  `src/SharpVision.Terminal/Rendering/`

### Modify in the control layer

- `src/SharpVision/Runtime/Application.cs`
- `src/SharpVision/Controls/Control.cs`
- `src/SharpVision/Controls/Children.cs`
- `src/SharpVision/Input/PointerEventArgs.cs`
- `src/SharpVision/Input/CaptureManager.cs`
- `src/SharpVision/Text/Layout.cs`
- text and glyph consumers in `src/SharpVision/Controls/`, especially `Text`,
  `RichText`, `TextInput`, `Table`, `ComboBox`, `Window`, `MenuItem`, `Button`,
  `ScrollBar`, `Shadow`, `FigletText`, and shared glyph types.

### Modify docs and showcase

- `docs/concepts/unicode-cell-geometry.md`
- `docs/architecture/capabilities.md`
- `docs/architecture/rendering-pipeline.md`
- `docs/concepts/input-routing.md`
- `docs/testing/unicode-rendering.md`
- `docs/testing/randomized.md`
- `docs/protocols/mouse.md`
- `docs/protocols/coverage-matrix.md`
- the Unicode/Text/RichText/TextInput showcase examples and screen tests.

## Task 1: Specify one geometry contract

- [x] **Step 1: Document source versus presentation**

State that source graphemes remain available to editors, while frame payloads
are terminal-safe presentations. List every base-less family and require U+FFFD
instead of raw zero-width output. Define draw replacement accounting.

- [x] **Step 2: Document exact rational coordinates**

Add the formula, input domain, overflow strategy, unavailable mapping behavior,
nullable cell coordinates, leave semantics, captured pixel-aware routing, and
no-clamping rule to the mouse and input-routing specs.

- [x] **Step 3: Document policy propagation and rectangular ownership**

Describe attach/inherit/update order, nullable local overrides, one root measure
invalidation, per-frame policy capture, owner rectangles, and complete repair.

- [x] **Step 4: Add test obligations and validate docs**

Require curated and randomized orphan presentation, exhaustive uneven-grid
coordinates, nullable-pointer routing, cross-control ambiguous width, complete
rectangle clipping/repair, independent oracles, allocation windows, and
end-to-end output bytes. Run Markdown formatting, lint, links, and docs tests.

## Task 2: Classify safe cluster presentation

- [x] **Step 1: Write failing cluster-classification tests**

Cover standalone combining marks, spacing marks without a base, prepend-only,
ZWJ-only, VS15/VS16-only, emoji-modifier-only, tag-only, mixed orphan
components, invalid UTF-16, valid decomposed text, keycaps, flags, and ZWJ
emoji. Assert width, source length, and whether safe replacement is required.

- [x] **Step 2: Add `Presentation`, `Policy`, and `Cluster`**

Use explicit constructors and validation. `Policy` carries the pinned Unicode
version, `Ambiguous`, and orphan `Presentation`. The default is narrow plus
replacement. `Cluster` is a small readonly value with `CellWidth` and a
replacement flag; it never owns source memory.

- [x] **Step 3: Refactor `Width` around one analysis pass**

Keep public `Measure` allocation-free. Add one internal analysis method used by
measurement and canvas. Do not segment twice and do not normalize or allocate.
Controls remain contextual, invalid data remains replacement-width one, and
valid base-bearing clusters retain existing Unicode 17 behavior.

- [x] **Step 4: Prove allocation and canonical behavior**

Retain zero-allocation warmed measurement. Prove composed/decomposed equality,
both ambiguous policies, all existing emoji cases, and generated-data
boundaries. Run all Unicode tests before changing canvas.

## Task 3: Prevent orphan overlap in frames and terminal bytes

- [x] **Step 1: Write the failing semantic-frame test**

Draw `"a\u0301"` where the mark is a separate grapheme, and separately draw a
standalone mark after an existing neighboring cell. Assert the orphan-owned cell
stores U+FFFD and the preceding cell remains unchanged. Repeat for ZWJ,
selector, modifier, tag, and clipped origins.

- [x] **Step 2: Write the failing exact-byte renderer test**

Render a frame containing a neighboring printable cell and an orphan cluster.
Assert emitted UTF-8 contains `EF BF BD`, never the orphan scalar bytes, and the
virtual terminal ends with two independent cells. Compare full and incremental
renders.

- [x] **Step 3: Apply classification in `Canvas` preflight and write**

Preflight and mutation select the same borrowed original cluster or static
replacement span. Count replacement bytes before mutation, increment
`DrawResult.Replaced`, preserve logical one-cell advance, and keep arena failure
transactional.

- [x] **Step 4: Add randomized non-overlap proof**

Generate base-less component clusters around narrow/wide neighbors, clipping,
clear, overwrite, and frame diffs. The independent test model must classify
orphan source without calling production `Width` or `Cluster` code.

## Task 4: Replace truncated metrics with an exact grid

- [x] **Step 1: Write failing uneven-grid mapping tests**

Construct 10×3 cells over 101×31 pixels. Assert every pixel maps by the rational
formula, mappings are monotonic and bounded, first/last pixels map to first/last
cells, and each cell receives at least one pixel when pixels are not smaller
than cells. Add maximum safe integer cases and out-of-domain rejection.

- [x] **Step 2: Extend `Metrics` without losing uniform compatibility**

The existing `(cellPixelWidth, cellPixelHeight)` constructor represents an exact
uniform 1×1 ratio for tests and callers. Add a `(Size cells, Size pixels)`
constructor and expose exact totals. Add `TryMap(Point pixels, out Point cells)`
using checked `long`. Validate positive totals before assigning state.

- [x] **Step 3: Make `Dimensions` preserve totals**

Construct exact metrics from complete positive cell and pixel sizes; do not
divide. Suspended, missing, zero-pixel, or pixels-smaller-than-cells cases have
documented mapping availability. Preserve original `Cells` and `Pixels` values.

- [x] **Step 4: Integrate the decoder and runtime resize path**

`ProtocolRouter.SetCellMetrics` and `Decoder` receive exact metrics. Pixel input
preserves the original zero-based pixel coordinate and calls `TryMap` once.
Every representative pixel report still passes all read-fragment boundaries.

- [x] **Step 5: Add exhaustive randomized mapping**

For seeded positive totals within bounded test sizes, enumerate every pixel and
compare to an independent 64-bit formula. Prove no overflow, gaps, fabricated
cells, or coordinate mutation.

## Task 5: Make unavailable cells explicit through routing

- [x] **Step 1: Write failing pointer validation tests**

Test cell-only, pixel-plus-mapped-cell, pixel-only, and leave values. Reject
negative present coordinates, inferred cells without both coordinates, and a
non-inferred pixel/cell pair when the contract requires provenance.

- [x] **Step 2: Change `Pointer.Cells` and `PointerEventArgs.LocalCells` to
      nullable**

Update constructor XML and validation before assignment. Cell protocols always
populate cells. Pixel protocols populate cells only through exact metrics. Leave
carries null/null. Preserve `IsCellPositionInferred` only when both are present.

- [x] **Step 3: Update capture and hit testing**

Ordinary physical hit testing requires cells. Capture may deliver a pixel-only
move/release to a control; its local cells remain null. Press/focus acquisition
without cells is rejected. Hover clears when no physical cell exists.

- [x] **Step 4: Harden controls**

Pressable, TextInput, ScrollBar, popup/menu behavior, selection, and any custom
pointer math pattern-match cells before use. ScrollBar may use pixel deltas
during an existing capture, but never substitutes `(0,0)`.

- [x] **Step 5: Add end-to-end routing tests**

Drive pixel input before metrics, after uneven metrics, outside the pixel
rectangle, during capture, after resize, and after focus loss. Assert exact
target, local coordinate, selection/thumb state, and no accidental top-left
interaction.

## Task 6: Propagate one Unicode policy through controls

- [x] **Step 1: Write the failing first-layout cross-control test**

Create one application with `Ambiguous.Wide` and place ambiguous text in Text,
RichText, TextInput, Table headers/cells, ComboBox, MenuItem, Window title,
Button, and FigletText. Assert each measured extent, arranged bound, cursor,
selection, and emitted frame agrees with `Frame.AmbiguousWidth`.

- [x] **Step 2: Add policy attachment to `Control`**

Attach the root with dispatcher plus immutable policy. `SetDispatcher`
propagates that reference through descendants. `Children.Attach` copies the
owner's current policy before the child can measure. Detach returns to the
documented default without mutating public local overrides.

- [x] **Step 3: Apply profile updates once**

`Application.ApplyCapabilities` derives the new policy, updates the complete
tree on the dispatcher, raises `CapabilitiesChanged`, and invalidates root
measure exactly once when geometry changes. A render in progress retains its
captured frame policy; the next layout/frame uses the new reference.

- [x] **Step 4: Migrate shared text layout and display controls**

Pass the effective policy into every `Width.Measure` and `Text.Layout.Format`
call. Make Text's local ambiguous-width override nullable, defaulting to
inheritance. Add focused tests per migrated control and remove hard-coded
`Ambiguous.Narrow` values.

- [x] **Step 5: Migrate interactive controls and glyph validators**

TextInput caret/index/selection, RichText runs, menus, tables, combo boxes,
window titles, button marks, scrollbar marks, shadows, and custom glyph
validation use one effective policy. Glyph APIs that intentionally require
portable narrow runes remain one cell under both policies and say so explicitly.

- [x] **Step 6: Prove mid-frame and mutation behavior**

Change the profile while a render flush is paused. Assert one measure/layout
cycle, immutable old/new policy references, no mid-frame swap, and next-frame
cursor/text ownership matching the new policy.

## Task 7: Generalize frame ownership to rectangles

> **Deferred:** Horizontal one-row ownership, orphan replacement, and clipping
> invariants are verified for this slice. Multi-row rectangular metadata remains
> planned for the semantic graphics slice.

- [ ] **Step 1: Specify and test public metadata**

Replace lead width metadata with positive owner `Size`; preserve convenience
width for one-row text if source compatibility is required. Continuations expose
lead coordinates and zero occupied size. Validate impossible combinations.

- [ ] **Step 2: Store width and height on leads**

Update `Cell`, `CellInfo`, frame write/copy/clone/semantic equality, and memory
preflight. Continuations across rows reference the same absolute lead. Checked
row-major arithmetic must reject overflow before mutation.

- [ ] **Step 3: Repair, style, clear, and clip complete rectangles**

Touching any owner cell repairs the full rectangle. Styling succeeds only when
the complete rectangle is inside the effective clip. Clear first repairs every
touched owner, then blanks the requested region. No dangling continuation may
survive outside the region.

- [ ] **Step 4: Update damage and encoding**

Damage expands to complete owner rectangles before row-span emission. Ordinary
text still encodes exactly once from its lead. Placeholder/image backends later
consume the rectangle metadata without teaching control code escape sequences.

- [ ] **Step 5: Add seeded frame equivalence**

Randomly place 1×1, 2×1, 1×2, and bounded multi-cell owners; overwrite, style,
clear, clip, resize, clone, and diff. Compare incremental output with a separate
full-render virtual screen and print the seed plus operation on failure.

## Task 8: Showcase and end-to-end proof

- [x] **Step 1: Add a Unicode geometry specimen**

Show composed/decomposed text, orphan marks rendered as replacement without
changing editable source, text/emoji selectors, modifiers, flags, ZWJ families,
ambiguous narrow/wide examples, and clipped wide clusters. Explain the active
policy in user-facing prose.

- [x] **Step 2: Add an uneven-pixel pointer specimen**

Display live pixel and optional mapped-cell coordinates plus grid totals. The
example must visibly distinguish unavailable cells instead of showing `(0,0)`.

- [x] **Step 3: Add showcase screen and interaction tests**

Verify the specimen at compact and wide sizes, policy change reflow, pixel-only
input, exact uneven mapping, text editing source preservation, and no orphan
continuations at clips.

- [x] **Step 4: Add PTY/tmux smoke**

Under an installed tmux, launch the Release showcase, navigate to the specimen,
enter or paste CJK/decomposed/orphan text, resize, and wait on visible state.
Prove bounded startup and clean restoration. Record that tmux rendering cannot
establish every outer terminal's glyph-width behavior.

## Task 9: Synchronize coverage and verify

- [x] **Step 1: Update only proven docs and coverage**

Claim safe orphan presentation, exact grid mapping, nullable cell routing,
shared control policy, and rectangular ownership only after their typed APIs and
tests land. Keep Kitty explicit-width and graphics protocols unsupported.

- [x] **Step 2: Run focused Release suites**

Run Unicode, canvas/frame/renderer, mouse/resize/router/session, control text,
selection, pointer routing, scrolling, application policy, showcase, and PTY
classes with finite Microsoft Testing Platform timeouts.

- [x] **Step 3: Run allocation and randomized gates**

Run Unicode allocation tests, exact-grid properties, frame randomized ownership,
diff equivalence, and cross-control geometry tests. Record seeds and measured
allocations.

- [x] **Step 4: Run repository gates**

Run `make format`, `make lint`, `make build`, and `make test`. Require zero
warnings/errors, all docs/link checks, and the configured test minimum.

- [x] **Step 5: Audit ownership and commits**

Use `git diff --check`, inspect every public XML contract, ensure every named
type has one file, stage only slice-owned paths/hunks, and keep commits aligned
to the verified tasks above.

## Commit sequence

1. `docs(geometry): specify unified cell ownership`
2. `feat(unicode): classify safe cluster presentation`
3. `fix(rendering): replace orphan zero-width clusters`
4. `feat(geometry): map exact pixel and cell grids`
5. `feat(input): expose unavailable pointer cells`
6. `feat(controls): inherit application cell policy`
7. `feat(rendering): support rectangular cell owners`
8. `feat(showcase): demonstrate Unicode cell geometry`
9. `docs(geometry): record verified geometry coverage`

## Slice acceptance checklist

- One immutable policy governs layout, controls, frames, cursor, selection, and
  rendering unless a documented local override is explicit.
- No orphan mark, selector, modifier, tag, prepend, or joiner byte is emitted as
  an independently owned cell.
- Every valid pixel of an uneven grid maps monotonically and exactly; missing or
  out-of-domain metrics never fabricate cell zero.
- Wide and rectangular owners survive clipping, clearing, overwriting, styling,
  selection, cloning, damage, and incremental rendering without partial state.
- Source text and safe terminal presentation are both observable at their
  documented ownership boundaries.
- Curated, exhaustive, randomized, allocation, integration, PTY, tmux, docs,
  build, and full test gates all pass.
