# Intrinsic border and shadow

## Status

Design approved 2026-07-14 (during the intrinsic-capabilities brainstorming; the
locked endpoint was "delete both, fully intrinsic"). The executable
[implementation plan](../plans/2026-07-15-intrinsic-border-shadow.md) is defined
and begins after the component Foundation gate. Implementation is pending. This
is the second of two sibling specs in the _WinForms/Delphi-aligned intrinsic
capabilities_ initiative. The first
([intrinsic container scrolling](2026-07-14-intrinsic-container-scrolling-design.md))
is implemented; this spec folds the `Border` and `Shadow` wrapper controls into
intrinsic `Control` behavior and removes both.

## Problem

`Border` and `Shadow` are dedicated single-child wrapper controls, but `Control`
already owns their entire surface:

- **Properties.** `BorderThickness`, `BorderGlyphs`, `HasShadow`, `ShadowMode`,
  `ShadowOffset`, `ShadowGlyph`, and `ShadowAttributes` are all style properties
  on `Control` (`Control.StyleProperties.cs`).
- **Rendering.** Every control's default `OnRender` calls `RenderChrome` →
  `ControlChrome.Render`, which already draws the shadow, the body fill, and a
  per-side border (`DrawPartialBorder`, gated on `BorderThickness != default`)
  for the current visual state.
- **Visual overflow.** `Control.VisualBounds` already expands for the shadow
  (`ControlChrome.ExpandVisualBounds(Bounds, HasShadow, ShadowOffset)`).
- **Content inset.** `Control.ContentBounds` is already
  `Padding.Deflate(BorderThickness.Deflate(Bounds))`.

So **`Shadow` duplicates intrinsic drawing and overflow behavior**, while its
wrapper node can still carry distinct bounds, margins, styling, ownership, and
routed ancestry. Moving `HasShadow` directly onto a child is equivalent only
when that child renders standard chrome and those node distinctions were not in
use; otherwise an ordinary chrome-rendering container preserves the node.
**`Border` fills one additional gap: layout reservation.** The base layout
pipeline reserves `Padding` but **not** `BorderThickness`:

- `Control.Arrange` calls `ArrangeOverride(Padding.Deflate(bounds))` — padding
  only.
- `CreateContentConstraint` deflates `Padding` only.

Today each control compensates individually: `Button` has a one-cell padding
class default; content-drawing controls (`Text`, `FigletText`, `ScrollBar`)
render into `ContentBounds`; `TextInput` is a `Container` with private scrollbar
parts whose editor geometry is also derived from `ContentBounds`; `Window`
deflates a hardcoded `Thickness(1)`; and `Border` deflates `BorderThickness` for
its child. A general-purpose container (`Stack`, `Grid`, `Dock`) reserves
nothing — which is the only reason a `Border` wrapper is needed around one.
There is no shipped `RichText` type.

This is the opposite of the desktop-UI idiom, where a border is a _property_
(`BorderStyle` in WinForms, `BorderStyle`/`BevelKind` in Delphi VCL), never a
wrapper control.

## Goals

- Make border a layout-reserved intrinsic property of any `Control`, so
  `BorderThickness` both draws (already true) and reserves space (the gap).
- Delete `Border` and `Shadow`; migrate every usage to the intrinsic properties.
- Reserve the border **exactly once** — reconcile the controls that reserve it
  themselves today so nothing double-insets.
- Preserve established border/shadow geometry, clipping, overflow, and terminal
  cells for the intrinsic property contract and migrated product/showcase
  surfaces; retire wrapper-only aliases and ownership semantics explicitly.
- Preserve distinct styling/layout nodes with ordinary containers rather than
  pretending every wrapper can collapse into an arbitrary custom-rendering
  child.

## Non-goals

- No change to `ControlChrome` rendering, `RenderChrome`, or `VisualBounds`.
- No new border/shadow properties — every property already exists.
- No redesign of `Window`'s bespoke title-bar frame (it hardcodes its own 1-cell
  frame and does not use `BorderThickness`; see §4).
- No change to the shadow model beyond deleting the `Shadow` wrapper.

## Decisions (locked)

| #   | Decision                | Choice                                                                                                                                                                                                          |
| --- | ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `Shadow`                | Deleted — intrinsic properties own shadow drawing/overflow; an ordinary chrome-rendering container preserves a distinct node when required                                                                      |
| 2   | `Border`                | Deleted — border becomes a layout-reserved intrinsic property                                                                                                                                                   |
| 3   | Where the gap is closed | The base layout pipeline reserves `BorderThickness` (with `Padding`) in the content constraint and before `ArrangeOverride`                                                                                     |
| 4   | Reserve exactly once    | Reconcile controls that reserve border themselves today (`Button` primarily); leaf/`Window` paths verified unchanged                                                                                            |
| 5   | Idiom                   | Border/shadow as `Control` properties, matching WinForms `BorderStyle` / VCL `BorderStyle`/`BevelKind`                                                                                                          |
| 6   | Showcase pages          | The `Border` and `Shadow` pages are **removed** (they document controls that no longer exist); border/shadow are demonstrated incidentally as properties on other panes, matching the removed `ScrollView` page |

## Design

### 1. Reserve `BorderThickness` in the base layout pipeline

Two changes in `Control`, both deflating border together with the padding that
is already deflated, in the same border-then-padding order `ContentBounds`
already uses:

```csharp
// Control.Arrange — was: ArrangeOverride(Padding.Deflate(bounds));
ArrangeOverride(Padding.Deflate(BorderThickness.Deflate(bounds)));

// CreateContentConstraint — subtract BorderThickness alongside Padding on each axis
// (i.e. the content constraint reserves margin + border + padding, not just margin + padding).
```

Measure combines border and padding with saturating arithmetic before passing
the inset to its axis helpers. Arrange deflates border and padding sequentially.
`Container.OnMeasuredDesired` must include the same inset when `AutoSize`
replaces the base desired size; otherwise auto-sized containers would still omit
their border.

Effect:

- **Containers** (`Stack`, `Grid`, `Canvas`, `Dock`, `View`, …) arrange their
  children into the border+padding-deflated box, so any control with
  `BorderThickness` set now insets its children under the border automatically —
  exactly what `Border` did. A container with the default zero border is
  unchanged (deflating zero is a no-op).
- **Content-drawing controls** render into `ContentBounds`, which is computed
  from `Bounds` (the full border box) and already deflates border+padding — so
  their drawing is unaffected by the `ArrangeOverride`-bounds change.
- **`TextInput`** remains a container rather than a leaf. Its private editor and
  scrollbar geometry must be tested directly so the common content rectangle is
  neither skipped nor deflated twice.

`ContentBounds` and `VisualBounds` are unchanged.

### 2. Rendering is already intrinsic

No rendering change is needed. The default `OnRender` → `RenderChrome` →
`ControlChrome.Render` draws the per-side border and the shadow for any control
whose `BorderThickness`/`HasShadow` are set. Containers that do **not** override
`OnRender` (`Stack`, `Grid`, `Canvas`, `Dock`, `View`) therefore draw their own
border for free. A container that **does** override `OnRender` and wants a
visible border must call `RenderChrome` (most do not need one); this is
documented, not a silent gap.

### 3. `Shadow` → `HasShadow`

Delete `Shadow`. When its child uses standard chrome, shares the wrapper bounds,
and does not rely on wrapper margins/style/ancestry, migrate
`new Shadow { Child = x, Mode = m, Offset = o, Glyph = g }` to setting
`x.HasShadow = true` plus the corresponding intrinsic properties. Preserve the
wrapper's non-base defaults explicitly: `ShadowOffset = new Point(2, 1)` and
`ShadowAttributes = Attributes.Dim`.

When any of those equivalence conditions is false, replace the wrapper with an
ordinary chrome-rendering container such as `Dock` and set the intrinsic shadow
properties there. A custom `OnRender` that does not call `RenderChrome` neither
draws a border nor a shadow automatically.

### 4. Reserve border exactly once (reconciliation)

The base change reserves border for **every** control, so any control that
already reserves it itself must stop doing so. The audit:

- **`Button`** — has both `BorderThickness = Thickness(1)` and
  `Padding = Thickness(1)` class defaults. The padding currently supplies the
  one-cell content inset because the base does not reserve the border. Remove
  only the padding class default when base reservation lands. Before that base
  change, the immediate `OnPressedChanged` path recomputes `ContentBounds` with
  border plus padding after released arrangement used padding alone, then shifts
  it. That latent double inset can collapse small content. Keeping
  `FaceContentBounds` and the immediate arrangement while moving reservation to
  the base corrects the path: released content is inset once, and immediate and
  post-layout pressed content are the same one-offset translation.
- **Content-drawing controls** (`Text`, `FigletText`, `ScrollBar`) — draw into
  `ContentBounds` derived from `Bounds`; unaffected. `RichText` is not a shipped
  type.
- **`TextInput`** — is a `Container` with private rails, not a leaf. Verify its
  rendered editor, caret, and scrollbar positions remain exactly once inside the
  border.
- **`Window`** — draws a bespoke title+frame and reserves it by deflating a
  hardcoded `Thickness(1)` in its own measure/arrange. It does **not** set the
  `BorderThickness` property (it stays zero), so the base deflates nothing for
  it; `Window` keeps its bespoke frame unchanged. (Whether `Window` should later
  express its frame through `BorderThickness` is out of scope.)
- **Every other container** — verify none references `BorderThickness` in its
  own `ArrangeOverride`; with a default zero border there is nothing to reserve.

The implementation plan verifies "reserved exactly once" per control with a
focused test on committed content position/`Bounds`.

### 5. Delete the controls and migrate usages

- Delete `src/SharpVision/Controls/Border.cs` and `Shadow.cs`, their showcase
  panes (`BorderPane`, `ShadowPane`), and their unit tests (`BorderTests`,
  `ShadowTests`) once the behavior is covered on the intrinsic path.
- `new Border { Child = x, Glyphs = g }` → set `x.BorderThickness` (e.g.
  `new Thickness(1)`) and `x.BorderGlyphs = g` (per-side thickness and glyph
  families already exist). When the bordered subject must stay a distinct node,
  set the border on a single-child container (`Dock`/`Grid`/`Stack`) that wraps
  it.
- `Border` exposed a `Glyphs` alias for `BorderGlyphs` and drew a per-side
  partial border; both are already provided intrinsically by `BorderGlyphs` and
  `ControlChrome.DrawPartialBorder`.

## Error handling

- All border/shadow property setters already validate (theme-value validators in
  `Control.StyleProperties.cs`); unchanged.
- The layout change adds no new exceptions. Sequential arrange deflation and
  saturated measure inset addition handle a valid near-`int.MaxValue` padding
  plus border without overflow, and oversized insets produce non-negative
  geometry.

## Testing

- **Reserve-once:** for a container with `BorderThickness` set, children arrange
  inside the border (content `Bounds` inset by border+padding); with zero
  border, layout is byte-for-byte unchanged (regression).
- **Specialized base paths:** AutoSize desired size and AutoScroll viewport/bar
  geometry reserve the border inside the committed border box.
- **Styling and extension:** theme-resolved border changes remeasure content,
  and an unfriended external `Container` receives the same inset through the
  protected extension kernel.
- **Border draws + reserves together:** a `Stack`/`Grid` with `BorderThickness`
  set renders the per-side border (via `Frame`/`FrameOracle`) and insets content
  — the case that previously required a `Border` wrapper.
- **Specialized controls:** content-drawing controls remain at their existing
  `ContentBounds`; `TextInput` editor, caret, and private rails are verified by
  position rather than an inaccurate leaf label.
- **Button reserve-once:** released content keeps its one-cell inset after the
  padding default is replaced by base border reservation. Pressed content is
  corrected from the old double-inset immediate path and must have immediate and
  post-layout parity at exactly one `ShadowOffset` translation.
- **Shadow intrinsic:** setting `HasShadow` on any control produces the shadow
  cells and expanded `VisualBounds` that `Shadow` produced when that control
  renders standard chrome; distinct-node cases use an ordinary container (port
  `ShadowTests` without copying wrapper-only body-fill behavior).
- **Border contract:** port `BorderTests`' per-side/glyph/partial-border cases
  to assertions on an intrinsic-bordered control.
- **Migration:** representative screens that used `Border`/`Shadow` render
  identically after migration.

Follow the repo test rules (watch new tests fail first; assert final frames and
committed geometry).

## Documentation to update in the same change

- `docs/controls/*` — remove the `Border` and `Shadow` control specs; document
  border/shadow as intrinsic `Control` properties.
- `docs/concepts/layout.md` — note that `BorderThickness` reserves layout (with
  `Padding`) and insets children.
- `docs/concepts/styling.md` / chrome docs — border/shadow are set on any
  control.
- `AGENTS.md` — note there is no `Border`/`Shadow` control; border/shadow are
  intrinsic `Control` properties (mirroring the scrolling note).
- Showcase inventory — the `Border` and `Shadow` pages are removed (border and
  shadow are properties every control has, not controls in their own right, so
  they get no dedicated page — the same treatment the `ScrollView` page got).
  Border/shadow remain visible incidentally on other panes that set them.

## Risks

- **Heavy showcase usage.** `Border` is used across many showcase panes as a
  framing helper (`CanvasPane`, `DockPane`, `GridPane`, `MenuPane`,
  `OverlayPane`, `RadioButtonPane`, `StackPane`, `ThemingPane`, `WindowPane`,
  `Doc.cs`, `Gallery.cs`, plus `BorderPane`/`ShadowPane`). This is a large
  migration and — like the ScrollView removal — collides with the concurrent
  showcase-rewrite effort; sequence it when the showcase is quiescent or
  coordinate closely.
- **Inventory change.** Removing the `Border`/`Shadow` pages changes the
  showcase control inventory that several showcase tests assert (`GalleryTests`,
  `GalleryRenderingTests`, `TmuxSmokeTests`, …) — the same edits the
  `ScrollView` removal made. Update those inventory assertions in the same
  change.
- **Double-inset regressions.** The base reservation affects any control with a
  non-zero `BorderThickness`; `Button` is the known case, but the plan must
  audit every current `OnRender` override and every control that sets a border
  to confirm the border is reserved exactly once.
- **Non-chrome `OnRender` containers.** A container that overrides `OnRender`
  without calling `RenderChrome` will reserve a border but not draw one if
  `BorderThickness` is set on it. Document that setting a border on such a
  control requires it to render chrome. `ShowcasePanel` is the current concrete
  audit case.

## Proposed phasing (for the implementation plan)

1. Atomically reserve `BorderThickness` in the base measure/arrange pipeline,
   include `Container.AutoSize`, remove Button's padding class default, and make
   the temporary Border wrapper delegate reservation to the base. The slice must
   never commit with Button or Border double-inset; verify zero-border,
   released, pressed, AutoSize, and AutoScroll geometry before commit.
2. Port intrinsic Shadow tests and non-page usages while `Shadow` still
   compiles; then delete the wrapper/page/spec atomically.
3. Port intrinsic Border tests and migrate integration/showcase call sites in
   compile-green slices while `Border` still compiles; then delete the
   wrapper/page/spec atomically.
4. Reconcile remaining docs and run the full quality gate.
