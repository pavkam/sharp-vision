# Intrinsic border and shadow

## Status

Design approved 2026-07-14 (during the intrinsic-capabilities brainstorming; the
locked endpoint was "delete both, fully intrinsic"). Ready for an implementation
plan. This is the second of two sibling specs in the *WinForms/Delphi-aligned
intrinsic capabilities* initiative. The first
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

So **`Shadow` is pure redundancy** — `new Shadow { Child = x }` is exactly
`x.HasShadow = true`. And **`Border` fills a single real gap: layout
reservation.** The base layout pipeline reserves `Padding` but **not**
`BorderThickness`:

- `Control.Arrange` calls `ArrangeOverride(Padding.Deflate(bounds))` — padding
  only.
- `CreateContentConstraint` deflates `Padding` only.

Today each control compensates individually: `Button` insets via
`FaceContentBounds`/`ContentBounds`, leaf controls (`Text`, `RichText`,
`FigletText`, `ScrollBar`, `TextInput`) render into `ContentBounds`, `Window`
deflates a hardcoded `Thickness(1)`, and `Border` deflates `BorderThickness` for
its child. A general-purpose container (`Stack`, `Grid`, `Dock`) reserves
nothing — which is the only reason a `Border` wrapper is needed around one.

This is the opposite of the desktop-UI idiom, where a border is a *property*
(`BorderStyle` in WinForms, `BorderStyle`/`BevelKind` in Delphi VCL), never a
wrapper control.

## Goals

- Make border a layout-reserved intrinsic property of any `Control`, so
  `BorderThickness` both draws (already true) and reserves space (the gap).
- Delete `Border` and `Shadow`; migrate every usage to the intrinsic properties.
- Reserve the border **exactly once** — reconcile the controls that reserve it
  themselves today so nothing double-insets.
- Preserve every existing chrome-rendering behavior, invariant, and test.

## Non-goals

- No change to `ControlChrome` rendering, `RenderChrome`, or `VisualBounds`.
- No new border/shadow properties — every property already exists.
- No redesign of `Window`'s bespoke title-bar frame (it hardcodes its own
  1-cell frame and does not use `BorderThickness`; see §4).
- No change to the shadow model beyond deleting the `Shadow` wrapper.

## Decisions (locked)

| #   | Decision                     | Choice                                                                                                    |
| --- | ---------------------------- | --------------------------------------------------------------------------------------------------------- |
| 1   | `Shadow`                     | Deleted — `HasShadow` + the shadow style properties already do everything                                 |
| 2   | `Border`                     | Deleted — border becomes a layout-reserved intrinsic property                                             |
| 3   | Where the gap is closed      | The base layout pipeline reserves `BorderThickness` (with `Padding`) in the content constraint and before `ArrangeOverride` |
| 4   | Reserve exactly once         | Reconcile controls that reserve border themselves today (`Button` primarily); leaf/`Window` paths verified unchanged        |
| 5   | Idiom                        | Border/shadow as `Control` properties, matching WinForms `BorderStyle` / VCL `BorderStyle`/`BevelKind`    |
| 6   | Showcase pages               | The `Border` and `Shadow` pages are **removed** (they document controls that no longer exist); border/shadow are demonstrated incidentally as properties on other panes, matching the removed `ScrollView` page |

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

Effect:

- **Containers** (`Stack`, `Grid`, `Canvas`, `Dock`, `View`, …) arrange their
  children into the border+padding-deflated box, so any control with
  `BorderThickness` set now insets its children under the border automatically —
  exactly what `Border` did. A container with the default zero border is
  unchanged (deflating zero is a no-op).
- **Leaf controls** render into `ContentBounds`, which is computed from `Bounds`
  (the full border box) and already deflates border+padding — so their drawing
  is unaffected by the `ArrangeOverride`-bounds change.

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

Delete `Shadow`. Migrate `new Shadow { Child = x, Mode = m, Offset = o, Glyph = g }`
to setting `x.HasShadow = true` (plus `x.ShadowMode`/`x.ShadowOffset`/
`x.ShadowGlyph`/`x.ShadowAttributes` when customized). The target control already
draws the shadow and expands its `VisualBounds`; the extra wrapper node is
removed with no visual change.

### 4. Reserve border exactly once (reconciliation)

The base change reserves border for **every** control, so any control that
already reserves it itself must stop doing so. The audit:

- **`Button`** — has a `BorderThickness = Thickness(1)` class default and
  arranges its content via `FaceContentBounds` in `ArrangeOverride`, then
  re-arranges it during `OnRender` using `ContentBounds`. With the base reserving
  border, the `ArrangeOverride` bounds already exclude the border, so
  `FaceContentBounds` must only apply the pressed-shadow shift (not re-deflate
  border), and the redundant `OnRender`-time re-arrange should be simplified. Net
  content position is unchanged; the double-arrange smell is removed. This is the
  primary reconciliation.
- **Leaf controls** (`Text`, `RichText`, `FigletText`, `ScrollBar`, `TextInput`)
  — draw into `ContentBounds` derived from `Bounds`; unaffected.
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
- `new Border { Child = x, Glyphs = g }` → set `x.BorderThickness`
  (e.g. `new Thickness(1)`) and `x.BorderGlyphs = g` (per-side thickness and
  glyph families already exist). When the bordered subject must stay a distinct
  node, set the border on a single-child container (`Dock`/`Grid`/`Stack`) that
  wraps it.
- `Border` exposed a `Glyphs` alias for `BorderGlyphs` and drew a per-side
  partial border; both are already provided intrinsically by `BorderGlyphs` and
  `ControlChrome.DrawPartialBorder`.

## Error handling

- All border/shadow property setters already validate (theme-value validators
  in `Control.StyleProperties.cs`); unchanged.
- The layout change adds no new exceptions; deflating a `Thickness` that exceeds
  the slot already saturates to a non-negative rect (existing `Thickness.Deflate`
  behavior), matching how `Padding` is handled today.

## Testing

- **Reserve-once:** for a container with `BorderThickness` set, children arrange
  inside the border (content `Bounds` inset by border+padding); with zero border,
  layout is byte-for-byte unchanged (regression).
- **Border draws + reserves together:** a `Stack`/`Grid` with `BorderThickness`
  set renders the per-side border (via `Frame`/`FrameOracle`) and insets content
  — the case that previously required a `Border` wrapper.
- **Leaf unaffected:** `Text`/`Button`/`TextInput` content position and rendered
  frames are unchanged by the base reservation (they already used `ContentBounds`).
- **Button reserve-once:** content position unchanged after the `FaceContentBounds`
  reconciliation, in normal and pressed-with-shadow states.
- **Shadow intrinsic:** setting `HasShadow` on any control produces the shadow
  cells and expanded `VisualBounds` that `Shadow` produced (port `ShadowTests`).
- **Border contract:** port `BorderTests`' per-side/glyph/partial-border cases to
  assertions on an intrinsic-bordered control.
- **Migration:** representative screens that used `Border`/`Shadow` render
  identically after migration.

Follow the repo test rules (watch new tests fail first; assert final frames and
committed geometry).

## Documentation to update in the same change

- `docs/controls/*` — remove the `Border` and `Shadow` control specs; document
  border/shadow as intrinsic `Control` properties.
- `docs/concepts/layout.md` — note that `BorderThickness` reserves layout (with
  `Padding`) and insets children.
- `docs/concepts/styling.md` / chrome docs — border/shadow are set on any control.
- `AGENTS.md` — note there is no `Border`/`Shadow` control; border/shadow are
  intrinsic `Control` properties (mirroring the scrolling note).
- Showcase inventory — the `Border` and `Shadow` pages are removed (border and
  shadow are properties every control has, not controls in their own right, so
  they get no dedicated page — the same treatment the `ScrollView` page got).
  Border/shadow remain visible incidentally on other panes that set them.

## Risks

- **Heavy showcase usage.** `Border` is used across many showcase panes as a
  framing helper (`GridPane`, `WindowPane`, `MenuPane`, `CanvasPane`, `ButtonPane`,
  `DockPane`, `RichTextPane`, `OverlayPane`, `StackPane`, `Doc.cs`, plus
  `BorderPane`/`ShadowPane`). This is a large migration and — like the ScrollView
  removal — collides with the concurrent showcase-rewrite effort; sequence it when
  the showcase is quiescent or coordinate closely.
- **Inventory change.** Removing the `Border`/`Shadow` pages changes the showcase
  control inventory that several showcase tests assert (`GalleryTests`,
  `GalleryRenderingTests`, `TmuxSmokeTests`, …) — the same edits the `ScrollView`
  removal made. Update those inventory assertions in the same change.
- **Double-inset regressions.** The base reservation affects any control with a
  non-zero `BorderThickness`; `Button` is the known case, but the plan must audit
  every control (all 17 `OnRender` overriders and any container that sets a border)
  to confirm the border is reserved exactly once.
- **Non-chrome `OnRender` containers.** A container that overrides `OnRender`
  without calling `RenderChrome` will reserve a border but not draw one if
  `BorderThickness` is set on it. Document that setting a border on such a control
  requires it to render chrome (or is unsupported).

## Proposed phasing (for the implementation plan)

1. Reserve `BorderThickness` in `Control.Arrange` and `CreateContentConstraint`
   (with a regression proving zero-border layout is unchanged).
2. Reconcile `Button` (and any other control that reserves border itself) so it
   is reserved exactly once; verify content position across states.
3. Delete `Shadow`; migrate `Shadow` usages to `HasShadow`; port `ShadowTests`.
4. Delete `Border`; migrate `Border` usages to intrinsic `BorderThickness`/
   `BorderGlyphs`; port `BorderTests`.
5. Showcase migration + inventory (coordinate with the concurrent showcase
   effort); docs + `AGENTS.md`; full quality gate.
