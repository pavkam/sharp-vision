# Slider

## Overview

`Slider` is a focusable signed-integer range control for direct value selection.
It differs from [`ScrollBar`](../scrolling/scroll-bar.md#overview): a track
press immediately selects the mapped value, the thumb always occupies one cell,
and no viewport extent or paging buttons participate in the geometry.

## API

| Member                        | Default         | Purpose                                                                  |
| ----------------------------- | --------------- | ------------------------------------------------------------------------ |
| `Minimum`, `Maximum`, `Value` | `0`, `100`, `0` | Define and select an inclusive signed-integer range.                     |
| `SmallChange`, `LargeChange`  | `1`, `10`       | Set arrow or wheel and Page Up or Page Down increments; zero is a no-op. |
| `Orientation`                 | `Horizontal`    | Chooses left-to-right or bottom-to-top mapping.                          |
| `Style`                       | `null`          | Optional complete developer-authored `SliderStyle`.                      |
| `ActualStyle`                 | Theme           | The resolved style; always present.                                      |
| `ValueChanged`                | No subscribers  | Reports a committed value after property notification.                   |
| `ChangeBy(int)`               | —               | Applies widened, saturating, endpoint-clamped programmatic change.       |

`Style`/`ActualStyle` (`SliderStyle`) own the rail presentation, on top of the
inherited `Face`/`Border`/`Shadow`:

| Member                                  | Type           | Description                                                        |
| --------------------------------------- | -------------- | ------------------------------------------------------------------ |
| `FillColor`, `TrackColor`, `ThumbColor` | `ControlColor` | The semantic or concrete rail-part colors. Required, not nullable. |
| `Glyphs`                                | `SliderGlyphs` | The validated one-cell runes for each rail part.                   |

A `with` expression creates a validated member-wise copy of
`SliderStyle.Default`; assigning `null` to `Style` restores the Theme-owned
presentation.

## Behavior

- `Minimum`, `Maximum`, and `Value` are inclusive signed integers. The default
  range is 0 through 100 with value 0. An endpoint setter throws only when it
  would invert the range; one that would exclude `Value` instead commits and
  auto-clamps `Value` to the new endpoint, raising `ValueChanged`. Direct
  `Value` assignment outside the current range throws.
- `SmallChange` defaults to 1 and `LargeChange` defaults to 10. Both accept
  non-negative integers; zero is a valid no-op.
- `ChangeBy(int)` adds with widened arithmetic, clamps to the endpoints, and
  returns whether a changed value committed.
- `ValueChanged` runs after the value and its property notification commit. Its
  immutable `SliderValueChangedEventArgs` exposes `PreviousValue` and `Value`. A
  no-op raises no event.
- `Orientation.Horizontal` is the default and measures 5 by 1 cells;
  `Orientation.Vertical` measures 1 by 5 cells. Explicit layout may safely
  stretch or shrink either axis.

Horizontal geometry maps the minimum to the left edge and the maximum to the
right; vertical geometry maps the minimum to the bottom and the maximum to the
top. Mapping uses the complete signed range in widened arithmetic with inclusive
endpoint rounding, and it stays correct across `int.MinValue` through
`int.MaxValue`.

## Input and visual states

A primary press focuses the control, selects the nearest mapped value, and takes
pointer capture. Captured movement keeps selecting against the geometry captured
at press time. Release, leave, focus transfer, terminal focus loss, disabling,
hiding, detachment, or disposal ends the drag without committing another value.

Left/Right operate a horizontal slider and Down/Up a vertical one: the
decreasing key subtracts `SmallChange` and the increasing key adds it. Page Down
subtracts `LargeChange`, Page Up adds it, and Home/End select the minimum and
maximum. Key press and repeat are accepted; release is ignored. A wheel gesture
applies `SmallChange` and is handled only when the value actually changes, which
leaves endpoint gestures available to an enclosing scroll surface. Keys outside
the slider command set remain available to inherited routed input.

The rail renders its filled, thumb, and unfilled cells with the `Accent`,
`Accent`, and `Muted` foregrounds respectively. `SliderStyle`'s `FillColor`,
`ThumbColor`, and `TrackColor` are authoritative when a local `Style` is
assigned. A theme document may otherwise author a `styles.slider` section with
`fillColor`/`trackColor`/`thumbColor` string members (accepting a
`SemanticColor` name, a `#RGB`/`#RRGGBB` literal, a palette key, or
`"transparent"`/`"default"`); an active theme's section supplies those colors
ahead of the code-owned defaults whenever no local `Style` is assigned (see
[themes.md](../../concepts/themes.md#style-types)). Background, attributes, and
the normal, pointer-over, focused, pressed, and disabled appearances follow the
shared [styling contract](../../concepts/styling.md#visual-states). Zero and
tiny bounds stay contained, and ambiguous-width glyphs fall back to one-cell
ASCII.

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

Validation runs before any mutation, and the signed extremes are safe. Events
arrive in committed order, and horizontal and vertical rails render their exact
cells. Zero and tiny bounds stay contained, keyboard and wheel semantics behave
as documented, and endpoint gestures bubble to ancestors. Pointer presses map
directly to values, capture and its cancellation end drags safely, focus and
disabled states render correctly, resize is handled, and the final mounted
semantic output is correct.
