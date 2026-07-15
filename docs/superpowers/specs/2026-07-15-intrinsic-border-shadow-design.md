# Intrinsic border and shadow

## Status

Implemented 2026-07-15. The locked endpoint from the intrinsic-capabilities
design—delete both wrappers and make chrome fully intrinsic—is now the shipped
contract. The executable
[implementation plan](../plans/2026-07-15-intrinsic-border-shadow.md) delivered
base border reservation, Button reserve-once geometry, intrinsic validation and
rendering proof, migrated integration/showcase surfaces, and atomic removal of
both wrapper types and pages. This is the second of two sibling specs in the
_WinForms/Delphi-aligned intrinsic capabilities_ initiative. The first
([intrinsic container scrolling](2026-07-14-intrinsic-container-scrolling-design.md))
is also implemented.

## Problem

Before this implementation, `Border` and `Shadow` were dedicated single-child
wrapper controls even though `Control` already owned their visual surface:

- **Properties.** `BorderThickness`, `BorderGlyphs`, `BorderColor`,
  `BorderAttributes`, `HasShadow`, `ShadowMode`, `ShadowOffset`, `ShadowGlyph`,
  `ShadowForeground`, `ShadowBackground`, and `ShadowAttributes` are all style
  properties on `Control` (`Control.StyleProperties.cs`); `Background` and
  `FillMode` complete the shared body-fill surface.
- **Rendering.** The base `Control.OnRender` calls `RenderChrome` →
  `ControlChrome.Render`, which draws the shadow, body fill, and per-side border
  (`DrawPartialBorder`, gated on `BorderThickness != default`) for the current
  visual state. Custom and sealed bespoke renderers do so only when they call
  that path or a specialized equivalent.
- **Visual overflow.** `Control.VisualBounds` already expands for the shadow
  (`ControlChrome.ExpandVisualBounds(Bounds, HasShadow, ShadowOffset)`).
- **Content inset.** `Control.ContentBounds` is already
  `Padding.Deflate(BorderThickness.Deflate(Bounds))`.

`Shadow` therefore duplicated intrinsic drawing and overflow behavior, while its
wrapper node could still carry distinct bounds, margins, styling, ownership, and
routed ancestry. The migration collapsed that node only when those distinctions
were unused; otherwise an ordinary chrome-rendering `Dock` preserved it.
`Border` had filled one additional historical gap: before this change, the base
layout pipeline reserved `Padding` but not `BorderThickness`:

- `Control.Arrange` calls `ArrangeOverride(Padding.Deflate(bounds))` — padding
  only.
- `CreateContentConstraint` deflates `Padding` only.

Before migration, controls compensated individually: `Button` used a one-cell
padding class default; content-drawing controls (`Text`, `FigletText`,
`ScrollBar`) rendered into `ContentBounds`; `TextInput` derived its editor and
private-rail geometry from `ContentBounds`; `Window` deflated a hardcoded
`Thickness(1)`; and the old border wrapper deflated for its child. A
general-purpose container (`Stack`, `Grid`, `Dock`) reserved nothing, which was
the only reason that wrapper had been needed. The implementation moved that
reservation into the base measure/arrange pipeline. There is no shipped
`RichText` type.

This is the opposite of the desktop-UI idiom, where a border is a _property_
(`BorderStyle` in WinForms, `BorderStyle`/`BevelKind` in Delphi VCL), never a
wrapper control.

## Implemented outcomes

- Border is a layout-reserved intrinsic property of every `Control`, so
  `BorderThickness` both draws and reserves space.
- `Border` and `Shadow` are deleted; every live usage uses intrinsic properties.
- The border is reserved **exactly once**; controls no longer repeat base
  reservation, so nothing double-insets.
- Established border/shadow geometry, clipping, overflow, and terminal cells are
  preserved on the intrinsic property contract; wrapper-only aliases and
  ownership semantics are retired.
- Distinct styling/layout nodes use ordinary `Dock` containers rather than
  collapsing into arbitrary custom-rendering children.

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
| 4   | Reserve exactly once    | Base reservation replaces Button's padding workaround; content-drawing controls and specialized TextInput/Window paths remain correct                                                                           |
| 5   | Idiom                   | Border/shadow as `Control` properties, matching WinForms `BorderStyle` / VCL `BorderStyle`/`BevelKind`                                                                                                          |
| 6   | Showcase pages          | The `Border` and `Shadow` pages are **removed** (they document controls that no longer exist); border/shadow are demonstrated incidentally as properties on other panes, matching the removed `ScrollView` page |

## Design

### 1. Reserve `BorderThickness` in the base layout pipeline

The implemented `Control` pipeline deflates border together with padding in the
same border-then-padding order used by `ContentBounds`:

```csharp
var content = Padding.Deflate(BorderThickness.Deflate(bounds));
ArrangeOverride(ResolveContentSlot(content));

// CreateContentConstraint and ResolveDesiredSize combine border plus padding
// with saturating addition before resolving either axis.
```

Measure combines border and padding with saturating arithmetic before passing
the inset to its axis helpers. Arrange deflates border and padding sequentially.
`Container.OnMeasuredDesired` includes the same saturated inset when `AutoSize`
replaces the base desired size, so auto-sized containers include their border.

Effect:

- **Containers** (`Stack`, `Grid`, `Canvas`, `Dock`, `View`, …) arrange their
  children into the border+padding-deflated box, so any control with
  `BorderThickness` set insets its children automatically. A container with the
  default zero border is unchanged (deflating zero is a no-op).
- **Content-drawing controls** render into `ContentBounds`, which is computed
  from `Bounds` (the full border box) and already deflates border+padding — so
  their drawing is unaffected by the `ArrangeOverride`-bounds change.
- **`TextInput`** remains a container rather than a leaf. Its private editor and
  scrollbar geometry uses the common content rectangle exactly once, as proved
  by direct editor, caret, and rail-position tests.

`ContentBounds` and `VisualBounds` are unchanged.

### 2. Rendering is already intrinsic

No rendering change was required. The base `OnRender` → `RenderChrome` →
`ControlChrome.Render` path draws per-side border and shadow. Containers that do
**not** override `OnRender` (`Stack`, `Grid`, `Canvas`, `Dock`, `View`)
therefore draw configured chrome. Base chrome draws shadow, optional opaque
body, then partial border; opacity comes from `FillMode.Opaque` or a
`Background` resolved from any cascade layer. Composite shadow restyles
destination cells, while block mode replaces the non-body footprint.
Ambiguous-wide glyphs repair to portable ASCII. A custom renderer that wants
intrinsic chrome calls `RenderChrome` before custom content; `ShowcasePanel` is
the audited example.

Button keeps a specialized `ControlChrome` call for pressed-face translation and
shadow-gap behavior. Window keeps its bespoke titled uniform frame and explicit
shadow path; it neither sets `BorderThickness` for that frame nor calls base
`RenderChrome`.

Sealed bespoke renderers such as `Text`, `FigletText`, and `TextInput` still
receive base border layout reservation but do not automatically paint the frame
or shadow. Callers compose an ordinary chrome-rendering `Dock` when those
controls need visible chrome.

### 3. `Shadow` → `HasShadow`

`Shadow` is deleted. Where its child used standard chrome, shared the old
wrapper bounds, and did not rely on wrapper margin/style/ancestry, the migration
set `HasShadow` and the corresponding intrinsic properties on that child. It
preserved the old wrapper's non-base appearance explicitly where applicable:
`ShadowOffset = new Point(2, 1)` and
`ShadowAttributes = TerminalAttributes.Dim`.

Where those equivalence conditions were false, an ordinary chrome-rendering
`Dock` preserves the distinct node and owns the intrinsic shadow properties. A
custom `OnRender` that does not call `RenderChrome` neither draws a border nor a
shadow automatically.

### 4. Reserve border exactly once (reconciliation)

The base now reserves border for **every** control. The implementation removed
the only redundant reservation and audited specialized paths:

- **`Button`** — now registers a one-cell `BorderThickness` class default and no
  padding class default. Base reservation supplies the one-cell content inset.
  The intentional immediate `OnPressedChanged` arrangement remains so pressed
  feedback does not wait for a later layout drain; `FaceContentBounds`
  translates the already-deflated `ContentBounds`, making immediate and
  post-layout pressed content the same one-`ShadowOffset` translation.
- **Content-drawing controls** (`Text`, `FigletText`, `ScrollBar`) — draw into
  `ContentBounds` derived from `Bounds`; unaffected. `RichText` is not a shipped
  type.
- **`TextInput`** — is a `Container` with private rails, not a leaf. Its
  rendered editor, caret, and scrollbar positions remain exactly once inside the
  border; focused tests prove that geometry.
- **`Window`** — draws a bespoke title+frame and reserves it by deflating a
  hardcoded `Thickness(1)` in its own measure/arrange. It does **not** set the
  `BorderThickness` property (it stays zero), so the base deflates nothing for
  it; `Window` keeps its bespoke frame unchanged. (Whether `Window` should later
  express its frame through `BorderThickness` is out of scope.)
- **Every other container** — none repeats `BorderThickness` in its own
  `ArrangeOverride`; with a default zero border there is nothing to reserve.

Focused tests verify "reserved exactly once" through committed content positions
and `Bounds`.

### 5. Deleted controls and migrated usages

- `Border.cs`, `Shadow.cs`, their showcase panes, dedicated unit suites, and
  control specifications are deleted.
- Each usage was evaluated for node equivalence. A genuinely equivalent case
  could place properties on the existing subject; the many showcase and
  integration frames that required distinct layout/styling identity became
  ordinary `Dock` surfaces with `BorderThickness`, `BorderGlyphs`, `HasShadow`,
  and the related properties.
- The old border `Glyphs` alias is retired. Intrinsic `BorderGlyphs` and
  `ControlChrome.DrawPartialBorder` provide the glyph and partial-edge contract.
- The Gallery has no Border or Shadow page. Border and shadow remain visible as
  orthogonal properties on Button, Window, Canvas specimens, the Dock sidebar,
  and the unprivileged showcase-authored `ShowcasePanel`.

## Error handling

- `BorderThickness` validates every physical edge as zero or one before
  mutation. `BorderGlyphs` reconstructs the value through `Glyphs` validation,
  closing the default-struct bypass and rejecting control or non-one-cell Runes.
- `ShadowMode` rejects undefined enum values. `ShadowGlyph` rejects control,
  zero-width, or wide Runes. Border/shadow terminal attributes use the common
  decoration validator. Failed local or theme assignments preserve prior state.
- The layout change adds no new exceptions. Sequential arrange deflation and
  saturated measure inset addition handle valid near-`int.MaxValue` padding plus
  border without overflow, and oversized insets produce non-negative geometry.

## Implemented proof

- `ControlBorderReservationTests` proves complete, partial, zero, and
  border-plus-padding reservation as well as saturated near-`int.MaxValue`
  measure insets.
- `ContainerAutoSizeTests` and `ContainerScrollGeometryTests` prove AutoSize and
  AutoScroll keep border, padding, viewports, and rails inside one committed
  border box.
- `StyleTests` proves a theme-resolved `BorderThickness` change remeasures and
  rearranges content. `SharpVision.Consumer.Tests` proves an unfriended external
  `FlowPanel` receives the same base inset without custom plumbing.
- `IntrinsicBorderTests` proves preset and custom glyph validation,
  validation-before-mutation, exact complete/partial/tiny cells, Unicode
  continuation ownership, and border-plus-padding layout.
- `IntrinsicShadowTests` proves composite and block modes, explicit appearance,
  wide-grapheme styling, signed visual overflow, unchanged layout/hit targets,
  and ancestor clipping.
- `ButtonTests` proves zero default padding, the one-cell border inset, and
  immediate/post-layout pressed parity at exactly one `ShadowOffset`
  translation. `TextInputTests` proves editor, caret, and private scrollbar
  rails remain inset exactly once.
- `DisplayPanelTests`, performance tests, Showcase rendering tests, and the live
  dashboard path prove migrated distinct `Dock` nodes and incidental
  border/shadow examples. The Theming-page frame test proves custom
  `ShowcasePanel.OnRender` calls `RenderChrome` before custom content.

## Documentation result

- The dedicated Border and Shadow control specifications and catalog entries are
  removed. The base control, layout, styling, rendering, ownership, custom
  component, and testing contracts describe the intrinsic surface.
- `AGENTS.md` locks the no-wrapper rule, the ordinary-container distinct-node
  pattern, and the custom-renderer `RenderChrome` obligation.
- The Showcase has 19 pages, starts on Button, and demonstrates border/shadow as
  orthogonal properties rather than navigation entries.

## Risks addressed

- Showcase wrapper frames migrated in compile-green slices, preserving attached
  layout metadata, order, geometry, style, and background on ordinary `Dock`
  nodes.
- Catalog-sensitive tests and the capture script use the Button-first inventory
  and live navigation bounds instead of obsolete hardcoded wrapper-page rows.
- The border audit covered every custom `OnRender` and every non-zero
  `BorderThickness`; `ShowcasePanel` now explicitly renders chrome, while
  specialized Button and Window paths retain their intentional rendering.
- Overlapping intrinsic and compatibility tests kept behavior covered until each
  wrapper, page, suite, and specification was deleted atomically.

## Delivered phasing

1. The base measure/arrange pipeline, AutoSize, Button defaults, and pressed
   parity landed together while compatibility remained green.
2. Intrinsic shadow proof and call-site migration landed before atomic Shadow
   wrapper/page/spec retirement.
3. Intrinsic border proof and integration/showcase migration landed before
   atomic Border wrapper/page/spec retirement.
4. Final normative documentation, regenerated dashboard evidence, and the full
   repository quality gate close the implementation.
