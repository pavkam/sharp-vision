# Scrolling

## Scrolling contract

A scroll view owns content, viewport, extent, offsets, and independent
horizontal/vertical policies: automatic, always, or hidden. Hidden suppresses a
bar but does not by itself forbid programmatic scrolling.

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
Unused delta propagates to the nearest scrollable ancestor. Pointer capture owns
thumb dragging and is released on disable, detach, close, or cancellation.

[`ScrollView`](../../src/SharpVision/Controls/ScrollView.cs) implements the
automatic algorithm with private composed bars. Wheel input bubbles from
content, each view consumes a clamped portion, and the exact remaining cell
delta continues outward. Content and resize changes clamp offsets before the
typed change event and before translated arrangement.

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
