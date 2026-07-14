# Intrinsic container scrolling and grow/shrink

## Status

Design approved 2026-07-14. Ready for an implementation plan. This is the first
of two sibling specs in one initiative — *WinForms/Delphi-aligned intrinsic
capabilities* — that folds dedicated wrapper controls into intrinsic
`Control`/`Container` behavior. This spec covers scrolling and grow/shrink and
removes `ScrollView`. A separate spec covers intrinsic border/shadow and removes
`Border`/`Shadow`.

## Problem

Scrolling in SharpVision is a dedicated container. `ScrollView` is the only
thing that scrolls: it owns one `Content` child, measures it with unbounded
scroll axes to discover an extent, runs an automatic scrollbar-reservation
probe, translates content by an offset, clips to a viewport, owns two
`ScrollBar` chrome controls, and handles wheel, keyboard, and bring-into-view.
Every scrollable region must be wrapped in one, and `List` composes a
`ScrollView` around a `Stack` internally to get overflow behavior.

Every other container silently clips overflow. A `Stack`, `Grid`, `Canvas`, or
`Dock` whose children exceed its box just loses the overflow — there is no way
to reach it without introducing a `ScrollView`. This is the opposite of the
mature desktop-UI model, where scrolling is an intrinsic capability of a
scrollable container, toggled by a single property.

## Goals

- Make overflow scrolling an intrinsic, opt-in capability of any `Container`,
  configured by properties rather than by wrapping in a dedicated control.
- Follow the Delphi VCL / Windows Forms model for container + children +
  scrolling + grow/shrink, using their vocabulary (`AutoScroll`, `AutoSize`,
  `AutoSizeMode`).
- Remove `ScrollView` and refactor `List`/`Table` onto the intrinsic mechanism.
- Preserve every scrolling correctness guarantee already proven in
  `docs/concepts/scrolling.md` (thumb math, reservation probe, clamping, input,
  nested propagation, capture, Unicode clipping, randomized geometry).

## Non-goals

- No reactive rendering, virtual trees, or hook-style state. Controls stay
  traditional mutable objects (`AGENTS.md`).
- No content virtualization (realizing only visible children). Content is fully
  realized, exactly as `ScrollView` behaves today.
- No `AutoScrollMinSize`/`AutoScrollMargin` equivalents in v1. The scroll extent
  is the natural content size; a minimum virtual size can be added later if a
  caller needs one.
- No border/shadow changes. Those belong to the sibling spec.

## Framework model (VCL / WinForms)

The design mirrors how the desktop frameworks model this, because that model is
mature and predictable:

- **Scrolling is a container capability toggled by one switch.** WinForms
  `ScrollableControl.AutoScroll`; Delphi `TScrollingWinControl.AutoScroll` with
  `HorzScrollBar`/`VertScrollBar`. A `Button` is not a scrollable control; a
  `Panel`/`Form`/`TScrollBox` is.
- **The scroll extent is the natural content size** — WinForms `DisplayRectangle`
  (the virtual/scroll area) versus `ClientRectangle` (the visible viewport). It
  is derived from where children actually lay out, *not* from re-measuring
  children against infinity. Docked/anchored/filled children fit the client and
  do not enlarge the scroll region; only content that is naturally larger than
  the client does.
- **Grow/shrink is a separate, explicit concern.** `AutoSize` +
  `AutoSizeMode { GrowOnly, GrowAndShrink }`. A container that grows to fit its
  content does not need to scroll; a container with a determinate size that
  cannot grow scrolls (when `AutoScroll`) or clips (when not).

## Decisions (locked)

| #   | Decision                    | Choice                                                                                                                       |
| --- | --------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| 1   | Where the capability lives  | The `Container` base (the `ScrollableControl` role); all subclasses inherit it                                               |
| 2   | Default                     | WinForms-faithful **opt-in**: `AutoScroll` defaults to `false` (clip); scrolling is turned on per container                  |
| 3   | Extent model                | Per axis, driven by `ScrollBars` (default `Vertical`): eligible axes are measured **unbounded** (natural extent, content-first); non-eligible axes stay bounded (**fill-first**: star/percent/stretch fill the client) |
| 4   | Width behavior              | Default `ScrollBars = Vertical` leaves width bounded, so text wraps and prose scrolls vertically; `ScrollBars = Both`/`Horizontal` opts an axis into unbounded measure for horizontal scrolling of incompressible content |
| 5   | Vocabulary                  | Adopt `AutoScroll`, `AutoSize`, `AutoSizeMode { GrowOnly, GrowAndShrink }`; keep the existing scrollbar-chrome enums          |
| 6   | Public scroll surface       | Hoist `ScrollView`'s surface onto `Container`; inert while `AutoScroll` is off                                               |
| 7   | `ScrollView`                | Deleted; `List`/`Table` refactored onto the base mechanism                                                                    |

## Design

### 1. `Container` gains the scrollable-container role

The scroll layer is implemented once at the `Control`/`Container` boundary and
transparently wraps each subclass's existing `MeasureOverride`,
`ArrangeOverride`, and `OnRender`. Subclasses keep measuring and arranging their
children exactly as they do now, into a *content box* the base sizes to the
content extent and shifts by the scroll offset. The base clips children to the
viewport and owns the bar chrome. This is `ScrollView`'s mechanism moved down to
the base and armed per instance.

All 16 `Container` subclasses inherit the capability. It stays off unless armed,
so `Button`, `TextInput`, `Menu`, `ComboBox`, `Popup`, `Overlay`, `Window`, and
the layout panels are unchanged until a caller sets `AutoScroll = true`.

### 2. Public surface on `Container`

```csharp
// Master switch (WinForms ScrollableControl.AutoScroll). Default false.
public bool AutoScroll { get; set; }

// Grow/shrink to content (WinForms AutoSize/AutoSizeMode).
public bool AutoSize { get; set; }                 // default false
public AutoSizeMode AutoSizeMode { get; set; }     // GrowOnly | GrowAndShrink; default GrowAndShrink

// Which axes AutoScroll may use (also the axes measured unbounded; see §4),
// and how bars reserve space (existing enums).
public ScrollBars ScrollBars { get; set; }                 // default Vertical
public ShowScrollBars ShowScrollBars { get; set; }         // default WhenNeeded
public ScrollBarVisibility HorizontalBarVisibility { get; set; }
public ScrollBarVisibility VerticalBarVisibility { get; set; }
public ScrollBarChrome ScrollBarChrome { get; set; }       // default Full
public ScrollBarFill ScrollBarFill { get; set; }           // default Block

// Scroll state and commands, hoisted from ScrollView.
public int HorizontalOffset { get; set; }
public int VerticalOffset { get; set; }
public Size Extent { get; }        // DisplayRectangle: natural content size
public Size Viewport { get; }      // ClientRectangle: visible content size
public int LineSize { get; set; }  // default 1
public int PageOverlap { get; set; }
public bool ScrollBy(int x, int y, Cause cause = Cause.Programmatic);
public bool BringIntoView(Control descendant);
public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;
```

`AutoScroll` is the enable; `ScrollBars` selects axes; `ShowScrollBars` (and the
per-axis `ScrollBarVisibility`) select reservation policy. While `AutoScroll` is
`false` the scroll surface is inert: offsets are `0`, `Extent == Viewport`,
`ScrollBy` returns `false`, `BringIntoView` is a no-op, and no bar chrome is
created. `ScrollView.ConstrainContentToViewport` is **not** carried over; see
§4.

### 3. Grow/shrink (`AutoSize` / `AutoSizeMode`)

`AutoSize` and `AutoSizeMode` are the WinForms-named surface over one sizing
model — the existing `Length`, `MinWidth/MaxWidth`, `MinHeight/MaxHeight`, and
alignment system. They do not introduce a competing sizing system:

- `AutoSize = true` fits the container's border box to its natural content size
  on both axes, overriding `Stretch` alignment and `Star`/`Percent` self-sizing
  while still honoring the min/max clamps. It is equivalent to forcing the
  container's own `Width`/`Height` to `Auto`.
- `AutoSizeMode.GrowAndShrink` fits exactly to content. `AutoSizeMode.GrowOnly`
  uses the greater of the content size and the explicit `Width`/`Height`, so the
  container never shrinks below its design size.

**Scroll interaction (the unifying rule).** `AutoScroll` acts only on an axis
that has a determinate viewport smaller than the content. An `AutoSize` axis
grows to fit content — so there is no overflow and no scroll — until it reaches
`MaxWidth`/`MaxHeight`; beyond the cap the axis is clamped and, if `AutoScroll`,
scrolls the remainder. The canonical "fit to content, then cap and scroll"
pattern is therefore `AutoSize = true`, a `MaxHeight`, and `AutoScroll = true`.

### 4. Mechanism

**Extent is the natural content size, discovered per axis via `ScrollBars`.**
SharpVision's `ResolveMeasureAxis` clamps each control's `DesiredSize` to its
constraint, so a bounded measure erases the overflow signal — the natural extent
on an axis is only recoverable by measuring *that axis* unbounded. The scroll
layer therefore nulls the `ScrollBars`-eligible axes in the content constraint
handed to `MeasureOverride`, captures the (now unclamped) result as the extent,
and leaves non-eligible axes bounded. With the default `ScrollBars = Vertical`:

- **Height is eligible → measured unbounded.** The container reports its natural
  content height; when it exceeds the viewport, a vertical bar appears. On this
  axis `Star`/`Percent`/`Stretch` size to content (you cannot fill an unbounded
  axis) — content-first, as in a WPF `ScrollViewer`.
- **Width is not eligible → measured bounded.** Text and other content **wrap**
  to the client width, and `Star`/`Percent`/`Stretch` **fill the client**
  (fill-first, like `Dock=Fill` and `TableLayoutPanel` percent columns). No
  horizontal bar appears; wide content wraps or clips.
- **Opt into horizontal scrolling** with `ScrollBars = Both`/`Horizontal`, which
  makes width eligible (measured unbounded); wide, incompressible content (a
  `Table`, a fixed width, a non-wrapping child) then grows a horizontal bar.
  This replaces `ScrollView.ConstrainContentToViewport`.

An armed container **must report its natural content extent** from measure on the
eligible axes. `Stack` already measures its stacking axis unbounded and `Canvas`
takes the union of absolute positions, so both work directly. `Grid` and `Table`
resolve tracks against the constraint; with the eligible axis nulled they must
let fixed/auto tracks report their full requested size rather than clamping it,
so the overflow is visible to the scroll layer. This is verified per container.

**Reservation, translation, clipping, chrome.** When armed and overflowing, the
base:

1. Runs `ScrollView`'s reservation probe against `ScrollBars` and the per-axis
   visibility: begin with always-visible bars, compute the candidate viewport,
   add an automatic bar when `extent > viewport` on its axis, recompute because
   one bar consumes space and can require the other, and stop when stable. Bars
   only grow during the probe. Percentage bases remain the candidate viewport.
2. Commits `Extent` and `Viewport`, clamps offsets to `0..max(0, extent -
   viewport)` per the enabled axes, and raises `ScrollChanged`.
3. Calls the subclass `ArrangeOverride` with a content box positioned at
   `origin - offset` and sized `max(extent, viewport)`, so children arrange
   normally and the offset scrolls them.
4. Owns two `ScrollBar` controls (created lazily on first arm) in a private
   chrome collection — not in `Children` — configured through their public
   orientation/chrome/fill API, exactly as `ScrollView` does now.

**Rendering.** `Container.RenderChildren` clips children to the viewport
(`Bounds` minus the reserved bar gutters) and then renders the bars over the
content. Horizontal clipping is grapheme-safe. Hit testing uses viewport
coordinates after the offset and never targets clipped content or a hidden bar.

**Input.** Arrow/page/home/end keys, wheel and pixel deltas, track clicks, thumb
dragging, and programmatic `BringIntoView` all use the typed scroll commands and
`Cause`. Unused wheel delta propagates to the nearest ancestor container that is
armed (`AutoScroll` with the matching axis enabled) — generalizing `ScrollView`'s
current ancestor walk. Pointer capture owns thumb dragging and is released on
disable, detach, close, or cancellation.

### 5. Defaults and arming

Every container defaults to `AutoScroll = false` (clip), matching WinForms/Delphi
and leaving current screens visually unchanged. The exceptions are the two
controls that scroll today and must keep doing so:

- `List` sets `AutoScroll = true` in its constructor (keeping the default
  `ScrollBars = Vertical`), drops its internal `ScrollView`, and enables
  scrolling directly on its item `Stack`.
- `Table` sets `AutoScroll = true` with `ScrollBars = Both` (wide grids scroll
  horizontally too) and scrolls through the base mechanism.

No other control scrolls unless a caller opts in. `Canvas` with
`ClipToBounds = false` cannot clip and therefore cannot scroll; arming it is
documented as ineffective (equivalent to `AutoScroll = false`) rather than a
silent partial behavior.

### 6. `ScrollView` removal and migration

- Delete `ScrollView` and `ScrollViewShowcasePane`.
- `new ScrollView { Content = x }` becomes `new Stack/Grid/Dock/... { AutoScroll
  = true }` around the same content, or `AutoScroll = true` on the content
  container itself. A view that relied on the old both-axis `ScrollView` sets
  `ScrollBars = Both`; the default `Vertical` gives wrap + vertical scroll.
- The Gallery's scrolling content host sets `AutoScroll = true`.
- `List` drops the composed `ScrollView`; its `VerticalOffset`, `Viewport`, and
  `BringIntoView` usages retarget to the inherited base members.
- `ScrollView.ConstrainContentToViewport = false` (old horizontal-scroll
  behavior) migrates to `ScrollBars = Both` on the container.
- The API-conformance showcase inventory drops the `ScrollView` entry.

## Error handling

- Offsets validate against the current extent; out-of-range assignment throws
  `ArgumentOutOfRangeException`, exactly as `ScrollView` does today.
- `BringIntoView` requires a descendant of this container's content and throws
  `ArgumentException` otherwise.
- All existing container validation (dispatcher affinity, disposed access, child
  ownership, capacity) is unchanged. Arming, offsets, `ScrollBy`, and
  `BringIntoView` verify mutability and dispatcher access like other mutations.
- Enum setters (`ScrollBars`, `ShowScrollBars`, visibility, chrome, fill,
  `AutoSizeMode`) reject unknown values; `LineSize`/`PageOverlap` reject
  negatives.

## Testing

Reframe `ScrollViewTests`, `RandomizedScrollViewTests`, and `ScrollingTests` as
`Container` scrolling tests and keep the full contract from
`docs/concepts/scrolling.md`: no/one/both bars, one bar inducing the other, all
visibility policies, exact fit, zero/tiny viewport, resize appearance and
removal, content changes, offset clamping, thumb math, every input method,
nested propagation, capture, focus, disabled state, Unicode clipping, and final
frames. Keep the 20,000-case randomized geometry suite (seed `0x005C7011`).

Add tests for the intrinsic model:

- **Opt-in default:** an unarmed container clips overflow and exposes inert
  scroll state; arming it produces bars and reachable content.
- **Fill-first:** `Star`/`Percent`/`Stretch` children fill the client and do not
  by themselves produce a bar.
- **Wrap versus horizontal scroll:** wrapping content scrolls vertically only; an
  incompressible child (fixed width, `Table`, `AutoSize`) produces a horizontal
  bar.
- **Natural-extent reporting:** `Grid`/`Table` with fixed tracks exceeding the
  client overflow and scroll rather than clamp.
- **Grow/shrink:** `GrowAndShrink` versus `GrowOnly`; `AutoSize` + `MaxHeight` +
  `AutoScroll` grows to content then caps and scrolls.
- **Migration:** `List`/`Table` keep their current scrolling behavior on the base
  mechanism; representative screens that used `ScrollView` behave identically
  after migration.

Follow the repository test rules: watch each new test fail for the expected
reason first; assert observable output and final frames; add property-style
tests where geometry is involved.

## Documentation to update in the same change

- `docs/concepts/scrolling.md` — recast around `AutoScroll` on `Container`; note
  the VCL/WinForms lineage and the natural-extent (`DisplayRectangle`) model.
- `docs/concepts/layout.md` — add grow/shrink (`AutoSize`/`AutoSizeMode`) and how
  a determinate axis scrolls while an auto-sizing axis grows.
- `docs/controls/*` — remove the `ScrollView` control spec; document the scroll
  surface on `Container`.
- `docs/architecture/showcase.md` and the showcase inventory — drop `ScrollView`.
- `AGENTS.md` — note that scrolling and grow/shrink are intrinsic `Container`
  properties (`AutoScroll`, `AutoSize`, `AutoSizeMode`) and that there is no
  dedicated scroll container.

## Risks

- **Bespoke child/chrome containers.** `List`, `Table`, `Menu`, and `ComboBox`
  override `RenderChildren`/`VisitChildren`/`HitTest`/`NavigationCount` and keep
  private chrome collections. The base now owns the scroll-bar chrome slot they
  hand-roll today, so their overrides must cooperate with base-owned bars even
  when unarmed. Their existing tests are the guardrail.
- **Natural-extent reporting.** Containers that clamp their content size to the
  constraint hide overflow from the scroll layer. `Grid`/`Table` need their
  track computation to expose the full requested size when smaller; verify per
  container.
- **Migration surface.** Every current `ScrollView` usage (showcase, tests,
  `List`) must move to `AutoScroll`. The showcase and screen tests surface any
  behavioral drift.
- **Three related switches.** `AutoScroll` (enable), `ScrollBars` (axes), and
  `ShowScrollBars`/visibility (reservation) are orthogonal but adjacent; keep
  each role distinct in docs, and prune if real usage shows redundancy.

## Proposed phasing (for the implementation plan)

1. Capture the natural-content extent at the `Control`/`Container` boundary and
   add `AutoSize`/`AutoSizeMode` grow/shrink, verified with no bars yet.
2. Arm scrolling: bar chrome ownership, the reservation probe, arrange
   translation, and viewport clipping in `Container`.
3. Input: wheel, keyboard, `BringIntoView`, nested propagation, and
   `ScrollChanged`.
4. Delete `ScrollView`; refactor `List`/`Table`; migrate the showcase and
   Gallery.
5. Documentation, `AGENTS.md`, and inventory sync; migrate and extend the test
   suite; full quality gate (`make format && make lint && make build && make
   test`).
