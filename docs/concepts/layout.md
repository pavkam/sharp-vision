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

## Passes and rounding

Measure receives available size and returns desired size without assigning
coordinates. Arrange receives the final slot, resolves deferred/percentage and
proportional lengths, and commits bounds. Invalidation during either pass queues
another pass; it never recursively re-enters layout.

Fractional percentage/proportional boundaries use cumulative edge rounding so
adjacent tracks share one boundary and the final track receives the remainder.

## Panels

- Stack measures along one axis and aligns on the cross axis.
- Grid supports fixed, percent, auto, proportional tracks, spacing, and spans.
- Dock consumes remaining edges in child order.
- Overlay shares the content box and uses deterministic z-order.
- Canvas positions children explicitly and clips by policy.

## Test contract

Cover every length combination, nested percentages, min/max, zero/tiny sizes,
margins/padding, alignment, visibility, wrapping remeasure, rounding sums,
spans, cache invalidation, resize, and overflow.
