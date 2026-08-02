# Scrolling

## Overview

Scrolling is not a dedicated control. `AutoScroll` is an intrinsic, opt-in
property (default `false`) of every
[`Container`](../controls/container.md#overview), following the VCL/WinForms
lineage of `ScrollableControl.AutoScroll`/`TWinControl`: any panel becomes
scrollable by turning on one flag, rather than by wrapping its content in a
dedicated scroll-view control. An armed container owns its content, viewport,
extent, offsets, and independent horizontal and vertical policies: automatic,
always, or hidden. Hidden suppresses the bar but does not by itself forbid
programmatic scrolling.

`ScrollBars` selects which axes are eligible to scroll and defaults to
`Vertical`. Eligible axes are measured unbounded so children can report their
natural, intrinsic extent — the WinForms `DisplayRectangle` model — rather than
being clamped to the current viewport. The `ResolveMeasureAxis` step in
`Control.MeasureOverride` clamps `DesiredSize` to the incoming constraint, so
any axis not selected by `ScrollBars` stays bounded and cannot overflow
silently.

The allocation-free
[`ScrollRange`](../../src/SharpVision/Scrolling/ScrollRange.cs) value stores
non-negative inclusive `Minimum` and `Maximum` endpoints, a `Value` contained
between them, and a non-negative `Viewport`. Its constructor rejects an
unordered or out-of-range state before anything is assigned. `Clamp` contains an
arbitrary value, and `Move` applies a signed delta with `long` saturation, so
even `int.MinValue` and `int.MaxValue` commands cannot wrap around.

[`ScrollCause`](../../src/SharpVision/Scrolling/ScrollCause.cs) records whether
a committed change came from the programmatic API, keyboard, pointer, wheel,
bring-into-view, content mutation, or resize. Controls carry that typed cause in
their change events instead of inferring intent from the resulting value.

## Automatic scrollbar algorithm

1. Begin with always-visible bars and no automatic bars.
2. Compute the candidate viewport after visible bars.
3. Measure content; scrollable axes may be unbounded for intrinsic content, but
   percentage bases remain the candidate viewport.
4. Add an automatic bar when `extent > viewport` on its axis.
5. Recompute because one bar consumes space and may require the other.
6. Stop when visibility is stable; bars only grow during this probe.
7. Clamp offsets to `0..max(0, extent - viewport)` and arrange once.

An exact fit does not count as overflow. A zero extent produces a full-size,
stationary thumb.

## Scrollbar presentation

`ScrollBar.Style` is a nullable, complete `ScrollBarStyle`. Hosts that generate
their own bars publish the same nullable `ScrollBarStyle` plus an always-present
`ActualScrollBarStyle`. Null resolves to the library scrollbar mechanics, with
the generated scrollbar receiving the active semantic control profile; a local
complete style wins. A container copies its effective value to the horizontal
and vertical rails it owns, and composite controls such as `TextInput`
synchronize the same value to their editor rails. Popup lists, page viewports,
tables, text editors, and standalone ScrollBars therefore share one themed
presentation by default.

The normal precedence applies: a local complete style wins over the Theme, which
wins over the code-owned fallback. The partial `ScrollBarStyleSet` type composes
overlay values onto a complete style; it is the composition input a themed
section will supply once the registrable style-section mechanism tracked by
[#155](https://github.com/pavkam/sharp-vision/issues/155) lands.

## Thumb geometry

[`ScrollThumb.Resolve`](../../src/SharpVision/Scrolling/ScrollThumb.cs) maps a
range onto a non-negative track using integer arithmetic only. A zero-length
track yields an empty thumb, a stationary range fills the track, and a scrolling
range always gets at least one cell. The proportional length is
`round(track * viewport / (range span + viewport))`, clamped to the track, and
the start is `round(travel * value offset / range span)`. Every product is
computed in `long`, so even an `int.MaxValue` range and viewport are safe.

[`ScrollThumb.ValueAt`](../../src/SharpVision/Scrolling/ScrollThumb.cs) is the
inverse mapping used by dragging. It clamps a requested start to the available
travel, maps both endpoints exactly whenever the track can represent movement,
and rounds to the nearest range value. Resizing always recomputes geometry from
the immutable range; it never rescales a previously rounded thumb.

`ScrollBarVisibility.Hidden`, `Auto`, and `Always` define the layout policy:
`Hidden` reserves no space, `Auto` participates in the stability probe described
above, and `Always` reserves space even when the range is stationary.

## Interaction

Line, page, home, and end commands, wheel and pixel deltas, buttons, track
clicks, thumb dragging, and programmatic bring-into-view all go through typed
scroll commands. Unused delta walks up `Control.Parent` through any owner role
and lands on the nearest ancestor whose runtime type is `Container` and whose
`AutoScroll` is true; a non-container composition or presentation owner never
interrupts that search. Pointer capture owns thumb dragging and is released on
disable, detach, close, or cancellation.

An armed [`Container`](../controls/container.md#overview) implements the
automatic algorithm with two privately owned framework-part
[`ScrollBar`](../../src/SharpVision/Controls/Scrolling/ScrollBar.cs) controls,
configured through their public orientation, chrome, and fill APIs. The
reservation probe runs against `ScrollBars` and the per-axis
`HorizontalBarVisibility`/`VerticalBarVisibility`: an automatic bar on one axis
can consume space that pushes the other axis over its threshold, so both bars
can induce each other before the probe stabilizes. Once the viewport settles,
content is translated by the committed offsets and rendered through a canvas
hard-clipped to the viewport. Intrinsic shadow overflow never contributes to
`Extent`, changes scrollbar visibility, enlarges an offset range, or escapes
that viewport.

Wheel input first offers the leaf control its normal default behavior, and a
child that actually moves handles the event. Once that child reaches an
endpoint, it leaves the next unchanged wheel event unhandled, and the enclosing
container consumes the clamped delta — so wheel scrolling propagates outward
through arbitrary ownership between nested armed containers. This ancestry walk
stops at an active [modal plane](modality.md#modal-route-boundaries); no
remainder may scroll a background container. If no in-plane offset changes, the
wheel event stays unhandled so the plane's Ignore or Dismiss policy can complete
it.

Keyboard arrows, PageUp/PageDown, and Home/End drive the same typed commands and
share the identical ancestry walk: an unconsumed remainder — arrows on an axis
already at its endpoint, or a page/endpoint command with nothing left to move —
propagates outward to the nearest enclosing armed container exactly like wheel
input, and the key is left unhandled only once no container along that walk
moved an offset. PageUp/PageDown and Home/End prefer the vertical axis; on a
container armed for horizontal scrolling only, they drive the horizontal offset
instead, so a horizontal-only container still has a fast-travel key rather than
swallowing all four for no effect. `BringIntoView(Control)` accepts any
descendant reached through owned `Control.Parent` edges and makes the smallest
two-axis offset change that exposes the descendant's arranged bounds. Content
and resize changes clamp offsets before the typed `ScrollChanged` event is
raised (carrying the previous and committed offsets, `Extent`, `Viewport`, and
the typed `ScrollCause`) and before the translated arrangement runs.

Horizontal clipping is grapheme-safe. Hit testing uses viewport coordinates
after the offset is applied and never targets clipped content.

## Expected behavior

Scrolling behaves consistently with no bars, one bar, or both bars, including
the case where one bar induces the other; across all visibility policies, exact
fits, zero and tiny viewports, bars appearing and disappearing on resize,
content changes, offset clamping, thumb math, every input method, nested
propagation, capture, focus, the disabled state, Unicode clipping, and the final
rendered frames.

The pure geometry additionally holds across 20,000 deterministic randomized
cases with seed `0x005C7011`: containment, repeatability, monotonic endpoint
position, exactly invertible endpoints, and a value round-trip error no larger
than one value step representable by the current track.
