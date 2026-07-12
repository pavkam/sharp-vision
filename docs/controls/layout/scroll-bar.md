# ScrollBar

## ScrollBar contract

`ScrollBar` is a focusable range control used independently or by an overflow host.
It supports vertical/horizontal orientation, decrement/increment buttons, track,
and draggable thumb. The implementation uses the allocation-free
[`Range` and `Thumb` geometry](../../concepts/scrolling.md#thumb-geometry) for
every rendered and interactive mapping.

## API

- `Minimum` and `Maximum` are non-negative inclusive endpoints. A setter that
  would exclude the current `Value` throws before mutation.
- `ViewportSize`, `SmallChange`, and `LargeChange` are non-negative integers.
  Zero changes are valid no-ops.
- Direct `Value` assignment must be inside the range. `ScrollBy(int, Cause)`
  uses saturating arithmetic and clamps at the endpoints.
- `ValueChanged` receives immutable `ScrollEventArgs` containing
  `PreviousValue`, committed `Value`, and the typed `Cause`. It runs after the
  property commit and is not raised for a no-op.
- `Orientation.Vertical` is the default and measures as `1x3` cells;
  `Orientation.Horizontal` measures as `3x1`. Explicit sizing may stretch or
  shrink either orientation.
- `DecrementGlyph`, `IncrementGlyph`, `TrackGlyph`, and `ThumbGlyph` accept any
  printable `Rune` whose measured width is exactly one cell. Their safe ASCII
  defaults are `-`, `+`, `.`, and `#`.

Thumb length represents `viewport / (range + viewport)` with a one-cell minimum
when scrolling is possible; zero range fills the track. Position uses stable
cumulative rounding and never exceeds available track. A one-cell control
renders only the thumb; a two-cell control renders only its buttons; zero cells
render nothing. This deterministic fallback never writes outside the arranged
bounds.

## Interaction

Axis arrows and buttons apply `SmallChange`; Page keys and track presses apply
`LargeChange`; Home and End reach the bounds. Wheel deltas use `SmallChange` on
the matching orientation. Recognized keyboard press and repeat transitions are
handled, while releases do not scroll.

Thumb press takes pointer capture and preserves the original range, track,
thumb, cell, and optional pixel baselines for the entire drag. Pixel protocols
reach the same mapping through the decoder's inferred cell coordinates, as
described in
[pixel and cell input routing](../../concepts/input-routing.md#pointer-capture-and-coordinates).
The committed result is still clamped against the live range if an event handler
changes it during a drag. Release, terminal focus loss, disable, detach, hide,
dispose, or focus transfer ends capture without a spurious value change.

All cells use the control's resolved normal, hovered, pressed, focused, and
disabled appearance. Input is ignored while effectively hidden or disabled.

## Test obligations

Tests cover range validation and pre-mutation failure, zero and `int.MaxValue`
ranges, thumb rounding, keyboard, wheel, buttons, track, cell and inferred-pixel
dragging, live range mutation, capture cancellation, detach/disable,
orientation, focus/pressed styling, glyph validation, tiny tracks, event order,
and exact semantic cells. The shared
[geometry proof](../../concepts/scrolling.md#test-contract) supplies 20,000 more
fixed-seed containment and inversion cases.
