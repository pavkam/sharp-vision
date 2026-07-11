# ScrollBar

## ScrollBar contract

`ScrollBar` is a focusable range control used independently or by `ScrollView`.
It supports vertical/horizontal orientation, decrement/increment buttons, track,
and draggable thumb.

## API

- `Minimum`, `Maximum`, `ViewportSize`, `Value`, `SmallChange`, and
  `LargeChange` are finite non-negative range values with `Minimum <= Maximum`.
- Invalid direct `Value` throws; command-driven changes clamp.
- `ValueChanged` reports old/new values and input cause after commit.
- `Orientation` changes measurement and keyboard mapping.

Thumb length represents `viewport / (range + viewport)` with a one-cell minimum
when scrolling is possible; zero range fills the track. Position uses stable
cumulative rounding and never exceeds available track.

## Interaction

Arrows/buttons apply small change, Page keys/track apply large change, Home/End
reach bounds, and thumb dragging uses pointer capture with cell or pixel delta.
Disable/detach/cancel ends dragging without a spurious value change.

## Test obligations

Cover range validation/changes, zero/huge values, thumb math/rounding, every
input path, pixel drag, capture cancellation, orientation, focus, disabled and
visual states, tiny/no-track bounds, events, and exact cells.
