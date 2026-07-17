# Slider

## Slider contract

`Slider` is a focusable signed-integer range control for direct value selection.
It is distinct from [`ScrollBar`](../layout/scroll-bar.md#scrollbar-contract): a
track press selects the mapped value immediately, the thumb has a fixed one-cell
extent, and no viewport extent or paging buttons participate in geometry.

## API

- `Minimum`, `Maximum`, and `Value` are inclusive signed integers. The default
  range is 0 through 100 with value 0. An endpoint setter that would invert the
  range or exclude `Value` throws before mutation; direct `Value` assignment
  outside the range also throws.
- `SmallChange` defaults to 1 and `LargeChange` defaults to 10. Both accept
  non-negative integers; zero is a valid no-op.
- `ChangeBy(int)` adds with widened arithmetic, clamps to the endpoints, and
  returns whether a changed value committed.
- `ValueChanged` runs after the value and property notification commit. Its
  immutable `SliderValueChangedEventArgs` exposes `PreviousValue` and `Value`.
  No event is raised for a no-op.
- `Orientation.Horizontal` is the default and measures as 5 by 1 cells.
  `Orientation.Vertical` measures as 1 by 5 cells. Explicit layout may safely
  stretch or shrink either axis.

Horizontal geometry maps minimum to the left edge and maximum to the right.
Vertical geometry maps minimum to the bottom and maximum to the top. Mapping
uses the complete signed range in widened arithmetic and inclusive endpoint
rounding, including `int.MinValue` through `int.MaxValue`.

## Input and visual states

Primary press focuses the control, selects the nearest mapped value, and takes
pointer capture. Captured movement continues selection against the press-time
geometry. Release, leave, focus transfer, terminal focus loss, disable, hide,
detach, or disposal ends the drag without another value commit.

Left/Right operate a horizontal slider; Down/Up operate a vertical slider. The
decreasing key subtracts `SmallChange` and the increasing key adds it. Page Down
subtracts `LargeChange`, Page Up adds it, and Home/End select the
minimum/maximum. Press and repeat are accepted; release is ignored. A wheel
gesture uses `SmallChange` and is handled only when the value changes, leaving
endpoint gestures available to an enclosing scroll surface.

The rail renders filled, thumb, and unfilled semantic cells through the clipped
canvas. Normal, pointer-over, focused, pressed, and disabled appearance follows
the shared [styling contract](../../concepts/styling.md#visual-states). Zero and
tiny bounds remain contained, and ambiguous-width glyphs use one-cell ASCII
fallbacks.

## Example

```csharp
var volume = new Slider
{
    Minimum = 0,
    Maximum = 100,
    Value = 40,
    SmallChange = 2,
    LargeChange = 10,
    HorizontalAlignment = HorizontalAlignment.Stretch,
};

volume.ValueChanged += (_, change) => SaveVolume(change.Value);
```

## Test obligations

Tests cover validation before mutation, signed extremes, event order, exact
horizontal and vertical cells, zero/tiny bounds, keyboard and wheel semantics,
endpoint bubbling, direct pointer mapping, capture and cancellation, focus,
disabled state, resize, and final mounted semantic output.
