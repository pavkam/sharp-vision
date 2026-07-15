# Intrinsic border and shadow

## Status

Design approved 2026-07-14 and implemented 2026-07-15. The locked endpoint was
"delete both, fully intrinsic." This is the second of two sibling specs in the
_WinForms/Delphi-aligned intrinsic capabilities_ initiative. The first
([intrinsic container scrolling](2026-07-14-intrinsic-container-scrolling-design.md))
is implemented; this spec folds the `Border` and `Shadow` wrapper controls into
intrinsic `Control` behavior and removes both.

## Problem

Before implementation, `Border` and `Shadow` were dedicated single-child wrapper
controls even though `Control` already owned their entire surface:

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

The retired shadow wrapper duplicated intrinsic rendering and visual overflow.
Its migration-sensitive behavior was its class default: offset `(2, 1)` with dim
attributes, which callers preserve explicitly when that exact appearance
matters. The retired border wrapper filled one real gap: layout reservation.
Before this design, the base layout pipeline reserved `Padding` but not
`BorderThickness`:

- `Control.Arrange` called `ArrangeOverride(Padding.Deflate(bounds))` — padding
  only.
- `CreateContentConstraint` deflated `Padding` only.

Each control compensated individually: `Button` inset via
`FaceContentBounds`/`ContentBounds`, leaf controls (`Text`, `RichText`,
`FigletText`, `ScrollBar`, `TextInput`) render into `ContentBounds`, `Window`
deflated a hardcoded `Thickness(1)`, and the retired border wrapper deflated
`BorderThickness` for its child. A general-purpose container (`Stack`, `Grid`,
`Dock`) reserved nothing — the only reason the wrapper had been needed.

This is the opposite of the desktop-UI idiom, where a border is a _property_
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
- No redesign of `Window`'s bespoke title-bar frame (it hardcodes its own 1-cell
  frame and does not use `BorderThickness`; see §4).
- No change to the shadow model beyond deleting the `Shadow` wrapper.

## Decisions (locked)

| #   | Decision                | Choice                                                                                                                                                                                                          |
| --- | ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `Shadow`                | Deleted — `HasShadow` + the shadow style properties already do everything                                                                                                                                       |
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

// CreateContentConstraint and ResolveDesiredSize reserve the saturated sum of
// BorderThickness and Padding on each axis.
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

### 3. Shadow migration uses intrinsic chrome

Set `HasShadow = true` on the chrome-rendering subject and configure
`ShadowMode`, `ShadowOffset`, `ShadowGlyph`, and `ShadowAttributes` directly.
Migrations that preserve the retired wrapper's default appearance set
`ShadowOffset = new Point(2, 1)` and `ShadowAttributes = Attributes.Dim`
explicitly. If the subject fully overrides `OnRender` without calling
`RenderChrome`, use an ordinary container such as `Dock` as the distinct chrome
node. The intrinsic node draws the shadow and expands its `VisualBounds` without
adding a purpose-built wrapper type.

### 4. Reserve border exactly once (reconciliation)

The base change reserves border for **every** control, so any control that
already reserves it itself must stop doing so. The audit:

- **`Button`** — keeps its `BorderThickness = Thickness(1)` class default but
  removes the `Padding = Thickness(1)` default that previously stood in for its
  border inset. Released content therefore stays one cell inside the frame.
  `OnPressedChanged` intentionally performs the immediate content arrangement
  needed by the translated pressed face. Pressed content now follows that face
  from the correctly deflated content box instead of retaining the former
  border-plus-padding double inset and tiny-height collapse.
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
- Set `BorderThickness` and `BorderGlyphs` on the chrome-rendering subject;
  per-side thickness and glyph families already exist. When the bordered subject
  must stay a distinct node, set the border on an ordinary container (`Dock`,
  `Grid`, or `Stack`) that owns the subject.
- `Border` exposed a `Glyphs` alias for `BorderGlyphs` and drew a per-side
  partial border; both are already provided intrinsically by `BorderGlyphs` and
  `ControlChrome.DrawPartialBorder`.

## Error handling

- All border/shadow property setters already validate (theme-value validators in
  `Control.StyleProperties.cs`); unchanged.
- The layout change adds no new exceptions. The combined border-plus-padding
  reservation saturates at `int.MaxValue`, and deflation saturates resulting
  extents at zero, so integer overflow and tiny slots cannot create negative
  geometry.

## Testing

- **Reserve-once:** for a container with `BorderThickness` set, children arrange
  inside the border (content `Bounds` inset by border+padding); with zero
  border, layout is byte-for-byte unchanged (regression).
- **Border draws + reserves together:** a `Stack`/`Grid` with `BorderThickness`
  set renders the per-side border (via `Frame`/`FrameOracle`) and insets content
  — the case that previously required a `Border` wrapper.
- **Leaf unaffected:** `Text`/`Button`/`TextInput` content position and rendered
  frames are unchanged by the base reservation (they already used
  `ContentBounds`).
- **Button reserve-once:** released content position remains unchanged;
  pressed-with-shadow content follows the translated face without the former
  double inset or tiny-height collapse.
- **Shadow intrinsic:** setting `HasShadow` on an ordinary control produces the
  retired wrapper's cells and expanded `VisualBounds`.
- **Border contract:** the retired suite's per-side, glyph, and partial-border
  cases run against an intrinsic-bordered control.
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

## Implementation record and residual risks

- **Showcase migration.** Framing helpers were migrated to intrinsic chrome on
  ordinary `Dock` nodes, and the retired pages were removed from inventory and
  navigation tests.
- **Inventory change.** Gallery, rendering, and tmux expectations track the
  reduced catalog; border and shadow remain visible on other panes.
- **Double-inset regressions.** Focused base-layout, Button, and intrinsic
  chrome tests prove border reservation occurs exactly once.
- **Non-chrome `OnRender` containers.** A container that overrides `OnRender`
  without calling `RenderChrome` will reserve a border but not draw one if
  `BorderThickness` is set on it. The control and styling contracts document
  that such a control must call `RenderChrome` or use an ordinary container as
  its chrome node.

## Implemented phasing

1. Reserved `BorderThickness` in base measure and arrange, with saturated edge
   sums and zero-border regressions.
2. Removed Button's redundant padding default and proved released and pressed
   content geometry.
3. Deleted the shadow wrapper and ported its observable contract to intrinsic
   chrome tests.
4. Deleted the border wrapper, migrated usages to ordinary intrinsic frame
   nodes, and ported its observable contract.
5. Updated normative docs, showcase inventory, and repository guidance, then ran
   the complete quality gate recorded in the implementation plan.
