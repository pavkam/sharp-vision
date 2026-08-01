# Slider

## Overview

`Slider` is a focusable signed-integer range control for direct value selection.
It is distinct from [`ScrollBar`](../scrolling/scroll-bar.md#overview): a track
press selects the mapped value immediately, the thumb has a fixed one-cell
extent, and no viewport extent or paging buttons participate in geometry.

## API

| Member                                  | Default               | Purpose                                                                  |
| --------------------------------------- | --------------------- | ------------------------------------------------------------------------ |
| `Minimum`, `Maximum`, `Value`           | `0`, `100`, `0`       | Define and select an inclusive signed-integer range.                     |
| `SmallChange`, `LargeChange`            | `1`, `10`             | Set arrow or wheel and Page Up or Page Down increments; zero is a no-op. |
| `Orientation`                           | `Horizontal`          | Chooses left-to-right or bottom-to-top mapping.                          |
| `FillColor`, `TrackColor`, `ThumbColor` | Accent, Muted, Accent | Set semantic or concrete rail-part colors.                               |
| `ValueChanged`                          | No subscribers        | Reports a committed value after property notification.                   |
| `ChangeBy(int)`                         | —                     | Applies widened, saturating, endpoint-clamped programmatic change.       |

## Behavior

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

The rail renders filled, thumb, and unfilled cells with `Accent`, `Accent`, and
`Muted` foregrounds respectively. `FillColor`, `ThumbColor`, and `TrackColor`
are authoritative local overrides. Background, attributes, and normal,
pointer-over, focused, pressed, and disabled appearance follow the shared
[styling contract](../../concepts/styling.md#visual-states). Zero and tiny
bounds remain contained, and ambiguous-width glyphs use one-cell ASCII
fallbacks.

## Example

![The Slider control rendered in the live showcase](../../images/controls/slider.png)

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

## Expected behavior

Tests cover validation before mutation, signed extremes, event order, exact
horizontal and vertical cells, zero/tiny bounds, keyboard and wheel semantics,
endpoint bubbling, direct pointer mapping, capture and cancellation, focus,
disabled state, resize, and final mounted semantic output.
