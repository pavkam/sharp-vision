# Layout

## Layout contract

Layout uses measure then arrange over integer terminal cells. Width and height
describe the border box. Margin is external, padding internal, and neither
collapses. Deflation saturates at zero.

## Lengths

Lengths are fixed cells, percentage, automatic content size, or proportional
remaining space. Values reject negative, NaN, and infinite inputs. Minimum and
maximum constraints clamp the resolved border box and validate `min <= max`.

During unbounded measure, a percentage dimension behaves as automatic/intrinsic
for desired size. During arrange it resolves against the final containing
content box after padding and reserved scrollbars. If the effective constraint
changes, content such as wrapped text is remeasured before final arrangement.

## Primitive API

`Length.Auto`, `Length.Cells(int)`, `Length.Percent(double)`, and
`Length.Star(double)` are immutable requests. Fixed cells are non-negative
integers, percentages are finite values from 0 through 100, and proportional
weights are finite and positive. The public `Length(Kind, double)` constructor
applies the same validation, so callers cannot bypass factory invariants.

`Constraint` represents each measure axis as a nullable non-negative integer;
null is unbounded and zero is a real bound. `Thickness` stores physical
left/top/right/bottom cell edges, rejects negative or overflowing opposing
edges, and deflates `Size` or `Rect` with extents saturated at zero. Horizontal
and vertical alignment and visible/hidden/collapsed participation use the
corresponding enums in `SharpVision.Layout`.

## Passes and rounding

Measure receives available size and returns desired size without assigning
coordinates. Arrange receives the final slot, resolves deferred/percentage and
proportional lengths, and commits bounds. Invalidation during either pass queues
another pass; it never recursively re-enters layout.

`Engine.Layout(Control, Size)` runs both phases in a zero-origin viewport. It
validates dispatcher affinity, caches unchanged constraints and slots, and
rejects nested transactions. A changed viewport remeasures even when no property
is dirty.

`Control.MeasureCore(Constraint)` receives the content-box constraint after
margin, the resolved border-box request, and padding are removed. It returns an
intrinsic content size. `Control.ArrangeCore(Rect)` receives the final content
rectangle after the border box is aligned and padding is removed. Both extension
points run only for hidden or visible controls; collapsed controls desire zero,
commit empty bounds, and skip both callbacks.

Fixed and percentage dimensions override alignment. Horizontal controls default
to `Left`, so an automatic width uses the measured desired size; applications
must opt into `HorizontalAlignment.Stretch` when a control should consume the
available row. An automatic dimension with stretch consumes the available axis;
otherwise automatic layout uses the measured desired size. Minimum and maximum
constraints are applied before the result is capped to the margin-deflated slot,
so tiny viewports always produce contained non-negative rectangles.

Layout surfaces such as `Stack`, `Grid`, `Dock`, `Border`, `Overlay`, and
`ScrollView` opt into horizontal stretch because they own a viewport or shared
slot. Their ordinary child controls remain content-sized unless the surface's
layout contract explicitly resolves that child to its slot.

Fractional percentage/proportional boundaries use cumulative edge rounding so
adjacent tracks share one boundary and the final track receives the remainder.

## Track allocation

`Tracks.Resolve` is the common integer allocator for Grid rows and columns.
Fixed and automatic tracks reserve their clamped requests first. Percentage
tracks use cumulative edges against the complete final axis rather than a
shrinking remainder. Star tracks then divide the non-negative remainder by
weight, redistributing cells when a maximum clips a share.

The convenience overload returns an array. The full overload accepts
`ReadOnlySpan<T>` inputs and caller-owned `Span<int>` output and performs no
managed allocation. It validates every length, intrinsic request, limit, and
destination size before writing output. During unbounded measure, percentage and
star tracks use their intrinsic automatic requests.

When bounded requests exceed the axis, percentage, automatic, fixed, then star
requests shrink in that order while respecting feasible minimums. If the sum of
minimums itself cannot fit, containment wins and extents shrink below minimums
instead of overflowing the terminal viewport.

`Tracks.Satisfy` expands a contiguous set of tracks for a spanning intrinsic
request. It distributes only the missing cells through cumulative integer edges,
so the final combined extent is exact.

## Panels

`Stack` uses the common track allocator along its sequential axis and the base
box model across it. Reverse order affects geometry, rendering, and default
focus traversal together.

`Grid` supports fixed, percent, auto, proportional tracks, spacing, spans, and
an implicit automatic track when definitions are empty. `Dock` consumes
remaining physical edges in child order. `Overlay` shares the content box and
uses stable attached z-order for render and hit testing. `Canvas` positions
children through cells or deferred percentages and clips by policy. `Border`
adds validated zero-or-one physical edges around one atomically owned child.

## Test contract

Cover every length combination, nested percentages, min/max, zero/tiny sizes,
margins/padding, alignment, visibility, wrapping remeasure, rounding sums,
spans, cache invalidation, resize, and overflow.

The base box-model suite also runs 10,000 fixed-seed combinations twice and
requires identical geometry, non-negative extents, and containment in the
saturated margin-deflated viewport.
