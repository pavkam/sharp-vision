# Grid

## Grid contract

`Grid` arranges managed children in fixed, percentage, automatic, and
proportional row/column tracks with spacing and spans.

## API

- `Rows` and `Columns` are permanent non-null `TrackCollection` values. Empty
  definitions mean one implicit automatic track.
- Immutable `Track` stores `Length`, `Minimum`, and `Maximum` and provides
  `Auto`, `Cells`, `Percent`, and `Star` factories. Limits are non-negative and
  maximum cannot be below minimum.
- `RowSpacing` and `ColumnSpacing` default zero and are non-negative cells.
- Attached `Row`, `Column`, `RowSpan`, and `ColumnSpan` require in-range origins
  and positive spans after definitions are committed. Defaults are row/column
  zero and spans one.
- `Children` follows managed ownership.

Track collection and placement mutation validates dispatcher affinity before
observable state changes, then invalidates measure once after a real change.

Measure first asks each child for its unbounded intrinsic size. Non-spanning
children contribute the maximum margin-inclusive request to their track;
spanning children then expand their track range deterministically after
subtracting only the gaps inside that span. Fixed, automatic, percentage, and
proportional tracks are resolved with their limits against the bounded track
area after saturated outer spacing is reserved.

Every child is measured again with its resolved spanned slot. Grid rebuilds the
intrinsic requests once from that result so wrapping on either axis can affect
the other. A child spanning only automatic rows is then measured with its
resolved finite column width and unbounded height, allowing wrapped text to grow
those rows instead of being clipped to their pre-wrap probe height. Arrange
repeats that bounded pass when the final viewport differs from measure, computes
cumulative integer origins, and commits each child to the union of its tracks
and the actual allocated internal gaps.

Rounding uses the shared cumulative-edge allocator. If definitions and spacing
cannot fit, spacing saturates first and tracks shrink deterministically until
all slots remain within the Grid. Empty definitions behave exactly as one
implicit automatic track. Collapsed children contribute no request and receive
empty bounds.

An ancestor with `AutoScroll` may translate a Grid to a negative visual origin;
track extents and gaps remain non-negative while their committed screen
coordinates preserve that signed translation. This is the ordinary scrolling
arrangement defined by the
[scrolling contract](../../concepts/scrolling.md#scrolling-contract), not an
invalid Grid placement.

Shrinking a definition collection validates every owned child's candidate origin
and span before mutation. A failure throws `InvalidOperationException` and
preserves the definitions and placements unchanged.

## Example

```csharp
var grid = new Grid
{
    Columns = { Track.Cells(20), Track.Star(1) },
    ColumnSpacing = 1,
};
```

## Test obligations

Cover all track kinds/mixes, min/max, spacing, spans, competing intrinsic
requirements, rounding/remainders, collapsed children, invalid attached values,
zero/tiny/overflow sizes, wrapping remeasure, resize, ownership, signed origins
under ancestor scrolling, and exact bounds/cells. Seed `0x051A475A` runs 10,000
mixed valid grids twice and proves determinism, containment, non-negative
geometry, ordered shared edges, and exact axis consumption when an uncapped
proportional track absorbs the remainder.
