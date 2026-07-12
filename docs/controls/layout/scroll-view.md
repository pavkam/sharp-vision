# ScrollView

## ScrollView contract

`ScrollView` contains zero or one child and composes a viewport with automatic,
always, or hidden horizontal and vertical
[ScrollBar](scroll-bar.md#scrollbar-contract) controls.

The bars are private owned controls: they participate in dispatcher, focus,
capture, rendering, hit testing, navigation, and disposal exactly like public
children, but callers cannot detach them or violate synchronization. `Content`
is the only public child.

## API

- `Content` atomically transfers managed parent ownership and accepts null.
- `HorizontalBarVisibility` and `VerticalBarVisibility` default to `Auto` and
  accept `Hidden`, `Auto`, or `Always`.
- `ConstrainContentToViewport` defaults to `false`. When enabled, the child
  receives the finite available width during measure so word-wrapping reading
  content reflows rather than growing an intrinsic horizontal extent.
- `Extent` is the measured margin-inclusive content size. `Viewport` is the
  final visible size after stable bar reservation.
- Direct `HorizontalOffset` and `VerticalOffset` assignments must fall inside
  `0..max(0, extent - viewport)`. `ScrollBy(int, int, Cause)` uses saturating
  arithmetic and clamps both axes.
- `ScrollChanged` receives one immutable `ScrollChangedEventArgs` after either
  offset changes. It includes `PreviousOffset`, committed `Offset`, `Extent`,
  `Viewport`, and typed `Cause`; no-op commands raise nothing.
- `LineSize` controls arrow and wheel distance. `PageOverlap` retains cells
  between PageUp/PageDown commands. Both are non-negative.
- `BringIntoView(Control)` accepts only an owned content descendant and makes
  the smallest two-axis offset change that exposes its arranged bounds.

Layout follows the
[two-axis automatic algorithm](../../concepts/scrolling.md#automatic-scrollbar-algorithm).
Content is measured intrinsically on both scrollable axes unless
`ConstrainContentToViewport` supplies its finite available width to
reflow-capable content. The probe begins with `Always` bars, adds overflowing
`Auto` bars monotonically, and recomputes after each addition because one
consumed row or column can induce the other bar. At most two additions are
possible. Exact fit does not overflow; zero and tiny viewports remain
non-negative.

Offsets clamp after every content or viewport change before child arrangement,
events, bar synchronization, hit testing, or rendering. Content is translated by
the committed offsets and rendered through a canvas clipped to the viewport.
Bars render afterward along the bottom and right edges; their shared corner is
left blank.

## Interaction

Wheel deltas, arrows, PageUp/Down, Home/End, composed bars, bring-into-view, and
programmatic commands share one scroll pipeline. Pointer wheel routing listens
on the bubble path, so wheel input targeting content still reaches its viewport.
An inner view consumes what it can and passes exact unused cell delta to each
nearest scrollable ancestor until exhausted. Home and End address the vertical
range; Left/Right address horizontal movement.

`Hidden` consumes no layout cells but keeps programmatic and input scrolling
enabled. `Auto` uses strict `extent > viewport`; `Always` reserves one cell even
for a stationary range. Wide graphemes crossing a horizontal clip edge are
removed as complete semantic cell owners—never split into orphan continuations.
Every defined non-scroll key remains unhandled so focused descendants and
ancestor controls can apply their own behavior without a viewport exception.

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

The randomized layout proof runs 10,000 cases with seed `0x005C701E`, varying
both policies and zero-to-large viewport sizes. Every case lays out twice and
asserts stable visibility-derived viewport geometry, non-negative containment,
and offsets inside the final extent.
