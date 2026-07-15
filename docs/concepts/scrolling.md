# Scrolling

## Scrolling contract

Scrolling is not a dedicated control. `AutoScroll` is an intrinsic, opt-in-only
(default `false`) property of every
[`Container`](../../src/SharpVision/Controls/Container.cs), following the
VCL/WinForms lineage of `ScrollableControl.AutoScroll`/`TWinControl`: any panel
can become scrollable by turning on one flag rather than by wrapping content in
a dedicated scroll-view control. An armed container owns content, viewport,
extent, offsets, and independent horizontal/vertical policies: automatic,
always, or hidden. Hidden suppresses a bar but does not by itself forbid
programmatic scrolling.

`ScrollBars` selects which axes are eligible to scroll and defaults to
`Vertical`. Eligible axes are measured unbounded so children can report their
natural, intrinsic extent — the WinForms `DisplayRectangle` model — rather than
being clamped to the current viewport. `Control.MeasureOverride`'s
`ResolveMeasureAxis` step clamps `DesiredSize` to the incoming constraint, so
any axis not selected by `ScrollBars` remains bounded and cannot overflow
silently.

The allocation-free [`Range`](../../src/SharpVision/Scrolling/Range.cs) value
stores non-negative inclusive `Minimum` and `Maximum` endpoints, a contained
`Value`, and a non-negative `Viewport`. Its constructor rejects an unordered or
out-of-range state before assignment. `Clamp` contains an arbitrary value and
`Move` applies a signed delta with `long` saturation, so even `int.MinValue` and
`int.MaxValue` commands cannot wrap.

[`Cause`](../../src/SharpVision/Scrolling/Cause.cs) records whether a committed
change came from the programmatic API, keyboard, pointer, wheel,
bring-into-view, content mutation, or resize. Controls use that typed cause in
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

Exact fit does not overflow. Zero extents produce a full-size stationary thumb.

## Thumb geometry

[`Thumb.Resolve`](../../src/SharpVision/Scrolling/Thumb.cs) maps a range to a
non-negative track using integer arithmetic only. A zero-length track yields an
empty thumb; a stationary range fills the track; a scrolling range gets at least
one cell. The proportional length is
`round(track * viewport / (range span + viewport))`, clamped to the track, and
the start is `round(travel * value offset / range span)`. Every product uses
`long`, including an `int.MaxValue` range and viewport.

[`Thumb.ValueAt`](../../src/SharpVision/Scrolling/Thumb.cs) is the inverse used
by dragging. It clamps a requested start to available travel, maps both
endpoints exactly whenever the track can represent movement, and rounds to the
nearest range value. Resizing always recomputes geometry from the immutable
range; it never scales a previously rounded thumb.

`ScrollBarVisibility.Hidden`, `Auto`, and `Always` define layout policy.
`Hidden` suppresses reserved space, `Auto` participates in the stable probe
below, and `Always` reserves space even when the range is stationary.

## Interaction

Line/page/home/end commands, wheel and pixel deltas, buttons, track clicks,
thumb dragging, and programmatic bring-into-view all use typed scroll commands.
Unused delta propagates to the nearest scrollable ancestor container. Pointer
capture owns thumb dragging and is released on disable, detach, close, or
cancellation.

An armed [`Container`](../../src/SharpVision/Controls/Container.cs) implements
the automatic algorithm with two ordinary owned
[`ScrollBar`](../../src/SharpVision/Controls/ScrollBar.cs) controls configured
through their public orientation, chrome, and fill APIs. The reservation probe
runs against `ScrollBars` and the per-axis `HorizontalBarVisibility`/
`VerticalBarVisibility`: an automatic bar on one axis can consume space that
forces the other axis over its threshold too, so both bars can induce each other
before the probe stabilizes. Once the viewport is settled, content is translated
by the committed offsets and rendered through a canvas clipped to the viewport.

Wheel input first offers the leaf control its normal default behavior; a child
that moves handles the event. Once that child reaches an endpoint, it leaves the
next unchanged wheel event unhandled and the enclosing container consumes the
clamped delta, so wheel scrolling propagates outward through nested armed
containers. Keyboard arrows, PageUp/PageDown, and Home/End drive the same typed
commands. `BringIntoView(Control)` accepts only an owned content descendant and
makes the smallest two-axis offset change that exposes its arranged bounds.
Content and resize changes clamp offsets before the typed `ScrollChanged` event
(carrying the previous/committed offset, `Extent`, `Viewport`, and typed
`Cause`) and before translated arrangement.

Horizontal clipping is grapheme-safe. Hit testing uses viewport coordinates
after offset and never targets clipped content.

## Test contract

Cover no/one/both bars, one bar inducing the other, all visibility policies,
exact fit, zero/tiny viewport, resize appearance/removal, content changes,
offset clamping, thumb math, every input method, nested propagation, capture,
focus, disabled state, Unicode clipping, and final frames.

Pure geometry additionally runs 20,000 deterministic randomized cases with seed
`0x005C7011`. They prove containment, repeatability, monotonic endpoint
position, exact invertible endpoints, and a value round-trip error no larger
than one value step representable by the current track.
