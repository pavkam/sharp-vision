# Scrolling

## Scrolling contract

A scroll view owns content, viewport, extent, offsets, and independent
horizontal/vertical policies: automatic, always, or hidden. Hidden suppresses a
bar but does not by itself forbid programmatic scrolling.

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

## Interaction

Line/page/home/end commands, wheel and pixel deltas, buttons, track clicks,
thumb dragging, and programmatic bring-into-view all use typed scroll commands.
Unused delta propagates to the nearest scrollable ancestor. Pointer capture owns
thumb dragging and is released on disable, detach, close, or cancellation.

Horizontal clipping is grapheme-safe. Hit testing uses viewport coordinates
after offset and never targets clipped content.

## Test contract

Cover no/one/both bars, one bar inducing the other, all visibility policies,
exact fit, zero/tiny viewport, resize appearance/removal, content changes,
offset clamping, thumb math, every input method, nested propagation, capture,
focus, disabled state, Unicode clipping, and final frames.
