# ScrollView

## ScrollView contract

`ScrollView` contains zero or one child and composes a viewport with automatic,
always, or hidden horizontal and vertical
[ScrollBar](scroll-bar.md#scrollbar-contract) controls.

## API

- `Content` uses managed parent ownership.
- `HorizontalBarVisibility` and `VerticalBarVisibility` select policy.
- `HorizontalOffset`, `VerticalOffset`, `Extent`, and `Viewport` expose
  committed geometry; invalid direct offsets throw, while scrolling methods
  clamp.
- `ScrollChanged` reports old/new offsets, extent, viewport, and cause.
- `LineSize`, `PageOverlap`, and `BringIntoView` configure commands.

Layout follows the
[two-axis automatic algorithm](../../concepts/scrolling.md#automatic-scrollbar-algorithm).
Offsets clamp after every content/viewport change before events and rendering.

## Interaction

Wheel/pixel delta, arrows, PageUp/Down, Home/End, bars, and programmatic
commands share one scroll pipeline. Unused delta bubbles to an ancestor scroll
view. Focus bring-into-view uses the smallest offset change.

## Example

```csharp
var page = new ScrollView
{
    Content = form,
    VerticalBarVisibility = ScrollBarVisibility.Auto,
};
```

## Test obligations

Cover no/one/both bars, induced second bar, exact fit, policies, offsets/events,
every interaction, nested propagation, focus bring-into-view, content/resize
changes, clipping/hit testing, Unicode horizontal scroll, and final cells.
